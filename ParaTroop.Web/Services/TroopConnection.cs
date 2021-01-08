using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ParaTroop.Web.Data;
using ParaTroop.Web.Hubs;
using ParaTroop.Web.Internal;
using ParaTroop.Web.Messages;
using TaskExtensions = ParaTroop.Web.Internal.TaskExtensions;

namespace ParaTroop.Web.Services {
    public sealed class TroopConnection : IDisposable {
        private const Int32 ConnectionTimeoutSeconds = 5;
        private const Int32 ReceiveTimeoutSeconds = 1800;

        private static readonly UTF8Encoding messageEncoding =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly Int32 clientIdLength = messageEncoding.GetByteCount("0000");

        private readonly TroopConnectionManager manager;
        private readonly IHubContext<ParaTroopHub, IParaTroopClient> hubContext;
        private readonly String hubConnectionId;
        private readonly TcpClient tcpConnection;
        private readonly CancellationTokenSource cancellationSource;

        private Task receiver;
        private IPEndPoint remoteEndpoint;
        private NetworkStream stream;
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(initialCount: 1);

        public Troop Troop { get; }
        public Int32 ClientId { get; private set; }

        public TroopConnection(
            TroopConnectionManager manager,
            IHubContext<ParaTroopHub, IParaTroopClient> hubContext,
            Troop troop,
            String hubConnectionId) {

            this.manager = manager;
            this.hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            this.Troop = troop ?? throw new ArgumentNullException(nameof(troop));
            this.hubConnectionId = hubConnectionId ?? throw new ArgumentNullException(nameof(hubConnectionId));
            this.tcpConnection = new TcpClient(AddressFamily.InterNetwork);
            this.cancellationSource = new CancellationTokenSource();
        }

        public async Task<TroopConnectionStatus> Connect(
            String username,
            String passwordHash,
            CancellationToken cancellationToken) {
            // Authenticate
            var hostAddress =
                (await Dns.GetHostAddressesAsync(this.Troop.Hostname))
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            if (hostAddress == null) {
                return TroopConnectionStatus.HostNotFound;
            }

            Console.WriteLine($"Setting remote endpoint to: {hostAddress}:{this.Troop.Port}");
            this.remoteEndpoint =
                new IPEndPoint(
                    hostAddress,
                    this.Troop.Port
                );

            await this.tcpConnection.ConnectAsync(this.remoteEndpoint.Address, this.remoteEndpoint.Port);
            Console.WriteLine($"Local endpoint is: {this.tcpConnection.Client.LocalEndPoint}");
            this.stream = this.tcpConnection.GetStream();

            Console.WriteLine("Sending authentication message.");
            await this.SendMessage(new AuthenticateMessage(passwordHash, username), cancellationToken);
            var clientIdBuffer = new Byte[clientIdLength];
            var readBytes = await TaskExtensions.WhenAny(
                this.stream.ReadAsync(clientIdBuffer, cancellationToken).AsTask(),
                Task.Delay(TimeSpan.FromSeconds(ConnectionTimeoutSeconds), cancellationToken).Then(() => -1)
            );
            if (readBytes == -1) {
                Console.WriteLine("Connection timeout");
                return TroopConnectionStatus.ConnectionTimeout;
            } else if (readBytes != clientIdLength) {
                Console.WriteLine("Invalid Authentication response");
                return TroopConnectionStatus.InvalidData;
            }

            this.ClientId = Int32.Parse(messageEncoding.GetString(clientIdBuffer));
            Console.WriteLine($"Client id received: {this.ClientId}");

            switch (this.ClientId) {
                case -1:
                    return TroopConnectionStatus.LoginFailed;
                case -2:
                    return TroopConnectionStatus.MaxLoginsReached;
                case -3:
                    return TroopConnectionStatus.NameTaken;
                case -4:
                    return TroopConnectionStatus.VersionMismatch;
                default:
                    if (this.ClientId < 0) {
                        return TroopConnectionStatus.InvalidData;
                    }

                    break;
            }

            await this.SendMessage(
                new ConnectMessage(
                    messageId: 0,
                    sourceClientId: this.ClientId,
                    name: username,
                    hostname: this.Troop.Hostname,
                    port: (UInt16)this.Troop.Port,
                    dummy: true),
                cancellationToken
            );

            this.StartReceiver();

            return TroopConnectionStatus.Success;
        }

        public async Task SendMessage(MessageBase message, CancellationToken cancellationToken) {
            Console.WriteLine($"Sending: {message}");

            var messageData =
                messageEncoding.GetBytes(message.ToString());

            await this.sendLock.WaitAsync(cancellationToken);
            try {
                await this.stream.WriteAsync(messageData, 0, messageData.Length, cancellationToken);
            } finally {
                this.sendLock.Release();
            }
        }

        private void StartReceiver() {
            // Schedule receiver loop on the thread pool
            this.receiver = Task.Run(
                () => this.ReceiveLoop(this.cancellationSource.Token),
                this.cancellationSource.Token
            );
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken) {
            using var textReader = new StreamReader(this.stream, messageEncoding);
            using var pushbackReader = new PushbackTextReader(textReader);
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                MessageBase message;
                try {
                    message = await TaskExtensions.WhenAny(
                        MessageBase.ReadMessage(pushbackReader, cancellationToken),
                        Task.Delay(TimeSpan.FromSeconds(ReceiveTimeoutSeconds), cancellationToken)
                            .Then(() => (MessageBase)null)
                    );
                } catch (JsonReaderException ex) {
                    Console.WriteLine(
                        $"Error reading message, aborting receive loop: {ex.Message}"
                    );

                    this.Shutdown();
                    return;
                }

                if (message == null) {
                    Console.WriteLine("Receive Timeout!");
                    // a timeout occurred!
                    this.Shutdown();
                    return;
                }

                var hubClient = this.hubContext.Clients.Client(this.hubConnectionId);

                if (hubClient == null) {
                    Console.WriteLine("Browser Disconnected!");
                    // Our client disconnected!
                    this.Shutdown();
                    return;
                }

                Console.WriteLine($"Message received: {message}");
                await hubClient.ReceiveMessage(message, cancellationToken);
            }
        }

        private void Shutdown() {
            this.manager.CloseConnection(this.hubConnectionId);
        }

        public void Dispose() {
            var stack = new StackTrace();
            Console.WriteLine($"Disposing TroopConnection {this.ClientId} at: {stack}");

            this.cancellationSource?.Cancel();
            this.receiver?.Dispose();
            this.stream?.Dispose();
            this.tcpConnection?.Dispose();

            this.receiver = null;
            this.stream = null;
        }

        public static async Task<TroopConnectionStatus> TestConnection(
            String hostname,
            UInt16 port,
            String passwordHash,
            CancellationToken cancellationToken) {

            // Authenticate
            var hostAddress =
                (await Dns.GetHostAddressesAsync(hostname))
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            if (hostAddress == null) {
                return TroopConnectionStatus.HostNotFound;
            }

            var remoteEndpoint =
                new IPEndPoint(
                    hostAddress,
                    port
                );

            try {
                using var tcpConnection = new TcpClient(AddressFamily.InterNetwork);

                var connected = await TaskExtensions.WhenAny(
                    tcpConnection.ConnectAsync(remoteEndpoint.Address, remoteEndpoint.Port).Then(() => true),
                    Task.Delay(TimeSpan.FromSeconds(ConnectionTimeoutSeconds), cancellationToken).Then(() => false)
                );

                if (!connected) {
                    return TroopConnectionStatus.ConnectionTimeout;
                }

                await using var stream = tcpConnection.GetStream();

                var messageData =
                    messageEncoding.GetBytes(new AuthenticateMessage(passwordHash, $"Test_{Guid.NewGuid()}").ToString());

                await stream.WriteAsync(messageData, 0, messageData.Length, cancellationToken);

                var clientIdBuffer = new Byte[clientIdLength];
                var readBytes = await TaskExtensions.WhenAny(
                    stream.ReadAsync(clientIdBuffer, cancellationToken).AsTask(),
                    Task.Delay(TimeSpan.FromSeconds(ConnectionTimeoutSeconds), cancellationToken).Then(() => -1)
                );

                if (readBytes == -1) {
                    return TroopConnectionStatus.ConnectionTimeout;
                } else if (readBytes != clientIdLength) {
                    return TroopConnectionStatus.InvalidData;
                }

                var clientId = Int32.Parse(messageEncoding.GetString(clientIdBuffer));

                switch (clientId) {
                    case -1:
                        return TroopConnectionStatus.LoginFailed;
                    case -2:
                        return TroopConnectionStatus.MaxLoginsReached;
                    case -3:
                        return TroopConnectionStatus.NameTaken;
                    case -4:
                        return TroopConnectionStatus.VersionMismatch;
                    default:
                        if (clientId < 0) {
                            return TroopConnectionStatus.InvalidData;
                        }

                        break;
                }

                return TroopConnectionStatus.Success;
            } catch (SocketException ex) {
                if (ex.SocketErrorCode == SocketError.ConnectionRefused) {
                    return TroopConnectionStatus.ConnectionRefused;
                } else {
                    return TroopConnectionStatus.UnknownError;
                }
            }
        }
    }
}
