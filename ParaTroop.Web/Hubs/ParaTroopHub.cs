using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ParaTroop.Web.Data;
using ParaTroop.Web.Internal;
using ParaTroop.Web.Messages;
using ParaTroop.Web.Services;

namespace ParaTroop.Web.Hubs {
    public class ParaTroopHub : Hub<IParaTroopClient> {
        private readonly TroopDbContext troopDb;
        private readonly TroopConnectionManager troopConnections;

        public ParaTroopHub(
            TroopDbContext troopDb,
            TroopConnectionManager troopConnections) {

            this.troopDb = troopDb ?? throw new ArgumentNullException(nameof(troopDb));
            this.troopConnections =
                troopConnections ?? throw new ArgumentNullException(nameof(troopConnections));
        }

        public async Task JoinTroop(
            JoinTroopMessage message) {

            Console.WriteLine("JoinTroop Received!");
            var cancellationToken = this.Context.ConnectionAborted;

            var troop = await
                this.troopDb.Troops.SingleOrDefaultAsync(
                    t => t.Id == message.TroopId,
                    cancellationToken
                );

            if (troop != null) {
                var result = await this.troopConnections.InitiateConnection(
                    this.Context.ConnectionId,
                    troop,
                    message.Username,
                    message.PasswordHash,
                    cancellationToken
                );

                switch (result.Type) {
                    case EitherType.Left:
                        await this.Clients.Caller.Joined(result.Left.ClientId, cancellationToken);
                        break;
                    case EitherType.Right:
                        await this.Clients.Caller.Error(
                            $"Error attempting to join Troop: {result.Right}",
                            cancellationToken
                        );
                        break;
                }
            } else {
                await this.Clients.Caller.Error(
                    "The troop you are trying to join could not be found.",
                    cancellationToken
                );
            }
        }

        public Task LeaveTroop() {
            // TODO: Gracefully disconnect from the server
            Console.WriteLine($"Hub Client {this.Context.ConnectionId} Requested to Leave the Troop.");
            this.troopConnections.CloseConnection(this.Context.ConnectionId);
            return Task.CompletedTask;
        }

        public Task SendOperationMessage(OperationMessage message) {
            message.Operation = MessageBase.UnWrapObjects(message.Operation);
            return this.SendMessage(message);
        }

        public Task SendSetMarkMessage(SetMarkMessage message) {
            return this.SendMessage(message);
        }

        public Task SendRemoveMessage(RemoveMessage message) {
            return this.SendMessage(message);
        }

        public Task SendEvaluateStringMessage(EvaluateStringMessage message) {
            return this.SendMessage(message);
        }

        public Task SendEvaluateBlockMessage(EvaluateBlockMessage message) {
            return this.SendMessage(message);
        }

        public Task SendGetAllMessage(GetAllMessage message) {
            return this.SendMessage(message);
        }

        public Task SendSetAllMessage(SetAllMessage message) {
            return this.SendMessage(message);
        }

        public Task SendSelectMessage(SelectMessage message) {
            return this.SendMessage(message);
        }

        public Task SendResetMessage(ResetMessage message) {
            return this.SendMessage(message);
        }

        public Task SendKillMessage(KillMessage message) {
            return this.SendMessage(message);
        }

        public Task SendConnectAckMessage(ConnectAckMessage message) {
            return this.SendMessage(message);
        }

        public Task SendConsoleMessage(ConsoleMessage message) {
            return this.SendMessage(message);
        }

        public Task SendStopAllMessage(StopAllMessage message) {
            return this.SendMessage(message);
        }

        public override Task OnDisconnectedAsync(Exception exception) {
            Console.WriteLine($"Hub Client {this.Context.ConnectionId} Disconnected.");
            this.troopConnections.CloseConnection(this.Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        private async Task SendMessage(MessageBase message) {
            var connection = this.troopConnections.GetConnection(this.Context.ConnectionId);

            if (connection == null) {
                await this.Clients.Caller.Error(
                    "Unable to send message: Not Connected.",
                    this.Context.ConnectionAborted
                );
                return;
            }

            await connection.SendMessage(message, this.Context.ConnectionAborted);
        }
    }
}
