using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityWs.Models
{
    public class Alias
    {
        public Alias()
        {
            this.DateCreated = DateTime.UtcNow;
            this.ConfirmationToken = Guid.NewGuid().ToString("N");
        }
        public int AliasID { get; set; }
        public int BeingID { get; set; }
        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string EmailAddress { get; set; }
        public string ConfirmationToken { get; set; }
        public DateTime? DateConfirmed { get; set; }
        public DateTime DateCreated { get; set; }
        public Being Being { get; set; }
        public ICollection<Email> Emails { get; set; }
        public ICollection<LoginAttempt> LoginAttempts { get; set; }
    }
}
