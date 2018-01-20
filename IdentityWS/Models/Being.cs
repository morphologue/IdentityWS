using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityWS.Models
{
    public class Being
    {
        public Being() {
            this.DateCreated = DateTime.UtcNow;
            this.ConfirmationToken = Guid.NewGuid().ToString("N");
        }

        public int BeingID { get; set; }
        [Required]
        public string SaltedHashedPassword { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockedOutUntil { get; set; }
        public string PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenValidUntil { get; set; }
        [Required]
        [RegularExpression("[0-9a-f]{32}")]
        public string ConfirmationToken { get; set; }
        public DateTime? DateConfirmed { get; set; }
        public DateTime DateCreated { get; set; }
        public ICollection<Alias> Aliases { get; set; }
        public ICollection<BeingClient> Clients { get; set; }
    }
}