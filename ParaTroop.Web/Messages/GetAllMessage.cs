using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class GetAllMessage : MessageBase {
        public override MessageType Type => MessageType.GetAll;

        public GetAllMessage() { }

        public GetAllMessage(
            Int32 messageId,
            Int32 sourceClientId) :
            base(messageId, sourceClientId) {
        }

        protected override IEnumerable<Object> GetData() {
            yield break;
        }
    }
}
