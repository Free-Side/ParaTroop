using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class EvaluateStringMessage : MessageBase {
        public override MessageType Type => MessageType.EvaluateString;

        public String String { get; set; }
        public Int32 Reply { get; set; }

        public EvaluateStringMessage() { }

        public EvaluateStringMessage(
            Int32 messageId,
            Int32 sourceClientId,
            String @string,
            Int32 reply) :
            base(messageId, sourceClientId) {

            this.String = @string;
            this.Reply = reply;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.String;
            yield return this.Reply;
        }
    }
}
