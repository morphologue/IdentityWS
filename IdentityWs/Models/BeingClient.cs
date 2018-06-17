using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityWS.Models
{
    public class BeingClient
    {
        public BeingClient() => this.DateCreated = DateTime.UtcNow;
        public int BeingClientID { get; set; }
        public int BeingID { get; set; }
        [Required]
        [MaxLength(20)]
        public string ClientName { get; set; }
        public DateTime DateCreated { get; set; }
        public Being Being { get; set; }
        public ICollection<BeingClientDatum> Data { get; set; }
    }
}
