using System;
using System.Collections.Generic;

namespace ParaTroop.Web.Messages {
    public class AuthenticateMessage : MessageBase {
        public override MessageType Type => MessageType.Authenticate;

        public String PasswordHash { get; }
        public String Name { get; }
        public String Version { get; }

        public AuthenticateMessage(
            String passwordHash,
            String name,
            String version = "0.10.3") :
            base(0, -1) {

            this.PasswordHash = passwordHash;
            this.Name = name;
            this.Version = version;
        }

        protected override IEnumerable<Object> GetData() {
            yield return this.PasswordHash;
            yield return this.Name;
            yield return this.Version;
        }
    }
}
