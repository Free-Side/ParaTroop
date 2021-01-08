using System;
using System.ComponentModel.DataAnnotations;

namespace ParaTroop.Web.Models {
    public class CreateTroopModel {
        [Required, MaxLength(100), MinLength(3)]
        public String Name { get; set; }

        [Required]
        public String Hostname { get; set; }

        [Required]
        public UInt16 Port { get; set; }

        [Required]
        public String PasswordHash { get; set; }
    }
}
