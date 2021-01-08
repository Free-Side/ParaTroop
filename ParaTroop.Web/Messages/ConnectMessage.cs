using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class ConnectMessage : MessageBase {
        public override MessageType Type => MessageType.Connect;

        public String Name { get; }
        public String Hostname { get; }
        public UInt16 Port { get; }
        public Boolean Dummy { get; }

        public ConnectMessage(
            Int32 messageId,
            Int32 sourceClientId,
            String name,
            String hostname,
            UInt16 port,
            Boolean dummy) :
            base(messageId, sourceClientId) {

            this.Name = name;
            this.Hostname = hostname;
            this.Port = port;
            this.Dummy = dummy;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.Name;
            yield return this.Hostname;
            yield return this.Port;
            yield return (this.Dummy ? 1 : 0);
        }
    }
}
