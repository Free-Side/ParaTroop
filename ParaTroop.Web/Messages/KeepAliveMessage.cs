using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class KeepAliveMessage : MessageBase {
        public override MessageType Type => MessageType.KeepAlive;

        public KeepAliveMessage() { }

        public KeepAliveMessage(Int32 messageId, Int32 sourceClientId) :
            base(messageId, sourceClientId) {
        }

        protected override IEnumerable<object> GetData() {
            yield break;
        }
    }
}
