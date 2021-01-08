using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class StopAllMessage : MessageBase {
        public override MessageType Type => MessageType.StopAll;

        public StopAllMessage() { }

        public StopAllMessage(Int32 messageId, Int32 sourceClientId) :
            base(messageId, sourceClientId) {
        }

        protected override IEnumerable<Object> GetData() {
            yield break;
        }
    }
}
