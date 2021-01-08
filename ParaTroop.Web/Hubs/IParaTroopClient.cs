using System;
using System.Threading;
using System.Threading.Tasks;
using ParaTroop.Web.Messages;

namespace ParaTroop.Web.Hubs {
    public interface IParaTroopClient {
        Task ReceiveMessage(MessageBase message, CancellationToken cancellationToken);

        Task Joined(Int32 clientId, CancellationToken cancellationToken);

        Task Error(String errorMessage, CancellationToken cancellationToken);
    }
}
