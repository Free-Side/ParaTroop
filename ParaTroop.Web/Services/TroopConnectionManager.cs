using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ParaTroop.Web.Data;
using ParaTroop.Web.Hubs;
using ParaTroop.Web.Internal;

namespace ParaTroop.Web.Services {
    public class TroopConnectionManager : IDisposable {
        private readonly IHubContext<ParaTroopHub, IParaTroopClient> hubContext;

        // Mapping of SignalR connection ids -> TroopConnections
        private readonly ConcurrentDictionary<String, TroopConnection> troopConnections =
            new ConcurrentDictionary<String, TroopConnection>();

        public TroopConnectionManager(
            IHubContext<ParaTroopHub, IParaTroopClient> hubContext) {

            this.hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public async Task<Either<TroopConnection, TroopConnectionStatus>> InitiateConnection(
            String connectionId,
            Troop troop,
            String username,
            String passwordHash,
            CancellationToken cancellationToken) {

            Console.WriteLine($"Initiating Connection to Troop {troop.Id} for  user {username}");

            // Check if this client already has a connection
            if (this.troopConnections.TryGetValue(connectionId, out var connection)) {
                if (connection.Troop.Id == troop.Id) {
                    // This might be overly optimistic
                    return connection;
                } else {
                    this.troopConnections.TryRemove(connectionId, out _);
                    connection.Dispose();
                }
            }

            var newConnection =
                new TroopConnection(
                    this,
                    this.hubContext,
                    troop,
                    connectionId);

            var status = await newConnection.Connect(username, passwordHash, cancellationToken);

            if (status == TroopConnectionStatus.Success) {
                TroopConnection replaced = null;
                this.troopConnections.AddOrUpdate(
                    connectionId,
                    newConnection,
                    (_, oldConnection) => {
                        replaced = oldConnection;
                        return newConnection;
                    });

                replaced?.Dispose();
                return newConnection;
            } else {
                newConnection.Dispose();
                return status;
            }
        }

        public TroopConnection GetConnection(String connectionId) {
            return this.troopConnections.TryGetValue(connectionId, out var connection) ?
                connection :
                null;
        }

        public void Dispose() {
            Console.WriteLine("TroopConnectionManager Disposed");
            var connections = this.troopConnections.Values.ToList();
            this.troopConnections.Clear();
            foreach (var connection in connections) {
                connection.Dispose();
            }
        }

        public void CloseConnection(String hubConnectionId) {
            if (this.troopConnections.TryRemove(hubConnectionId, out var removed)) {
                Console.WriteLine($"Hub Client {hubConnectionId} disconnecting from Troop {removed.Troop.Id}");
                removed.Dispose();
            }
        }
    }
}
