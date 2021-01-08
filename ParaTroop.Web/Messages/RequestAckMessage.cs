using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class RequestAckMessage : MessageBase {
        public override MessageType Type => MessageType.RequestAck;

        public Int32 Flag { get; set; }
        public Int32 Reply { get; set; }

        public RequestAckMessage() { }

        public RequestAckMessage(
            Int32 messageId,
            Int32 sourceClientId,
            Int32 flag,
            Int32 reply) :
            base(messageId, sourceClientId) {

            this.Flag = flag;
            this.Reply = reply;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.Flag;
            yield return this.Reply;
        }
    }
}
