using System;
using System.ComponentModel.DataAnnotations;
using IdentityWs.Jobs;

namespace IdentityWs.Models
{
    public class LoginAttempt : ICleanable
    {
        public LoginAttempt() => this.DateCreated = DateTime.UtcNow;
        public int LoginAttemptID { get; set; }
        public int AliasID { get; set; }
        public DateTime DateCreated { get; set; }
        [Required]
        [MaxLength(20)]
        public string ClientName { get; set; }
        public bool Success { get; set; }
        public Alias Alias { get; set; }
    }
}
