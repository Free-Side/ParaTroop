using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class SetMarkMessage : MessageBase {
        public override MessageType Type => MessageType.SetMark;

        public Int32 Index { get; set; }

        public Int32 Reply { get; set; }

        public SetMarkMessage() { }

        public SetMarkMessage(
            Int32 messageId,
            Int32 sourceClientId,
            Int32 index,
            Int32 reply = 0) :
            base(messageId, sourceClientId) {

            this.Index = index;
            this.Reply = reply;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.Index;
            yield return this.Reply;
        }
    }
}
