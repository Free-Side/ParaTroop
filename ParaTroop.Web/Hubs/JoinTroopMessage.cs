using System;

namespace ParaTroop.Web.Hubs {
    public class JoinTroopMessage {
        public Int64 TroopId { get; set; }

        public String Username { get; set; }

        public String PasswordHash { get; set; }
    }
}
