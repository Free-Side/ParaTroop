using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class SetAllMessage : MessageBase {
        public override MessageType Type => MessageType.SetAll;

        public String Document { get; set; }
        public Int32[][] ClientRanges { get; set; }
        public Dictionary<String, Int32> ClientLocations { get; set; }

        public SetAllMessage() { }

        public SetAllMessage(
            Int32 messageId,
            Int32 sourceClientId,
            String document,
            Int32[][] clientRanges,
            Dictionary<String, Int32> clientLocations) :
            base(messageId, sourceClientId) {

            this.Document = document;
            this.ClientRanges = clientRanges;
            this.ClientLocations = clientLocations;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.Document;
            yield return this.ClientRanges;
            yield return this.ClientLocations;
        }
    }
}
