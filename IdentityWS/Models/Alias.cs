using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityWS.Models
{
    public class Alias
    {
        public Alias() => this.DateCreated = DateTime.UtcNow;
        public int AliasID { get; set; }
        public int BeingID { get; set; }
        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string EmailAddress { get; set; }
        public DateTime DateCreated { get; set; }
        public Being Being { get; set; }
        public ICollection<Email> Emails { get; set; }
        public ICollection<LoginAttempt> LoginAttempts { get; set; }
    }
}
