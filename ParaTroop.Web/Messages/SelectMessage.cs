using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class SelectMessage : MessageBase {
        public override MessageType Type => MessageType.Select;

        public int Start { get; set; }
        public int End { get; set; }
        public int Reply { get; set; }

        public SelectMessage() { }

        public SelectMessage(
            Int32 messageId,
            Int32 sourceClientId,
            Int32 start,
            Int32 end,
            Int32 reply = 0) :
            base(messageId, sourceClientId) {

            this.Start = start;
            this.End = end;
            this.Reply = reply;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.Start;
            yield return this.End;
            yield return this.Reply;
        }
    }
}
