using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class ConsoleMessage : MessageBase {
        public String String { get; set; }
        public override MessageType Type => MessageType.Console;

        public ConsoleMessage() { }

        public ConsoleMessage(
            Int32 messageId,
            Int32 sourceClientId,
            String @string) :
            base(messageId, sourceClientId) {

            this.String = @string;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.String;
        }
    }
}
