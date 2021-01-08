using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class EvaluateBlockMessage : MessageBase {
        public override MessageType Type => MessageType.EvaluateBlock;

        public Int32 Start { get; set; }
        public Int32 End { get; set; }
        public Int32 Reply { get; set; }

        public EvaluateBlockMessage() { }

        public EvaluateBlockMessage(
            Int32 messageId,
            Int32 sourceClientId,
            Int32 start,
            Int32 end,
            Int32 reply) :
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
