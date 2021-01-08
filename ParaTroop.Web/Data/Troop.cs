using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParaTroop.Web.Data {
    public class Troop {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Int64 Id { get; }

        [Required, MaxLength(100), MinLength(3)]
        public String Name { get; }

        [Required]
        public String Hostname { get; }

        [Required]
        public Int32 Port { get; }

        public DateTime Created { get; }

        public Troop(
            Int64 id,
            String name,
            String hostname,
            Int32 port,
            DateTime created) {

            this.Id = id;
            this.Name = name;
            this.Hostname = hostname;
            this.Port = port;
            this.Created = created;
        }
    }
}
