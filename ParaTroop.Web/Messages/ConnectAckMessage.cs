using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class ConnectAckMessage : MessageBase {
        public override MessageType Type => MessageType.ConnectAck;

        public Int32 Reply { get; set; }

        public ConnectAckMessage() { }

        public ConnectAckMessage(
            Int32 messageId,
            Int32 sourceClientId,
            Int32 reply) :
            base(messageId, sourceClientId) {

            this.Reply = reply;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.Reply;
        }
    }
}
