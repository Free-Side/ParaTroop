using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class ResetMessage : SetAllMessage {
        public override MessageType Type => MessageType.Reset;

        public ResetMessage() { }

        public ResetMessage(
            Int32 messageId,
            Int32 sourceClientId,
            String document,
            Int32[][] clientRanges,
            Dictionary<String, Int32> clientLocations) :
            base(messageId, sourceClientId, document, clientRanges, clientLocations) {
        }
    }
}
