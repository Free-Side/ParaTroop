using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class OperationMessage : MessageBase {
        public override MessageType Type => MessageType.Operation;

        /// <summary>
        /// A mix of numbers and strings representing the Operational Transform operation.
        /// </summary>
        public Object[] Operation { get; set; }
        public Int32 Revision { get; set; }

        public OperationMessage() { }

        public OperationMessage(
            Int32 messageId,
            Int32 sourceClientId,
            Object[] operation,
            Int32 revision) :
            base(messageId, sourceClientId) {

            this.Operation = operation;
            this.Revision = revision;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.Operation;
            yield return this.Revision;
        }
    }
}
