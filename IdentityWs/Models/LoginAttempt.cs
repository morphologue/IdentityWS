using System;

namespace IdentityWs.Models
{
    public class LoginAttempt {
        public LoginAttempt() => this.DateCreated = DateTime.UtcNow;
        public int LoginAttemptID { get; set; }
        public int AliasID { get; set; }
        public DateTime DateCreated { get; set; }
        public bool Success { get; set; }
        public Alias Alias { get; set; }
    }
}
