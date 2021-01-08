using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class ConstraintMessage : MessageBase {
        public override MessageType Type => MessageType.Constraint;

        public Int32 ConstraintId { get; set; }

        public ConstraintMessage() { }

        public ConstraintMessage(
            Int32 messageId,
            Int32 sourceClientId,
            Int32 constraintId) :
            base(messageId, sourceClientId) {

            this.ConstraintId = constraintId;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.ConstraintId;
        }
    }
}
