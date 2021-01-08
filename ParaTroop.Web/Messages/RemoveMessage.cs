using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class RemoveMessage : MessageBase {
        public override MessageType Type => MessageType.Remove;

        public RemoveMessage() { }

        public RemoveMessage(Int32 sourceClientId) : base(0, sourceClientId) {
        }

        protected override IEnumerable<Object> GetData() {
            yield break;
        }
    }
}
