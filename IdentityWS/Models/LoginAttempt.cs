using System;

namespace IdentityWS.Models
{
    public class LoginAttempt {
        public LoginAttempt() => this.DateCreated = DateTime.UtcNow;
        public int LoginAttemptID { get; set; }
        public int AliasID { get; set; }
        public DateTime DateCreated { get; set; }
        public string ErrorMessage { get; set; }
        public Alias Alias { get; set; }
    }
}
