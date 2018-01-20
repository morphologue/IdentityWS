using System;
using System.ComponentModel.DataAnnotations;

namespace IdentityWS.Models
{
    public class Email {
        public Email() => this.DateCreated = DateTime.UtcNow;
        public int EmailID { get; set; }
        public int AliasID { get; set; }
        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string From { get; set; }
        [MaxLength(100)]
        [EmailAddress]
        public string ReplyTo { get; set; }
        [Required]
        [MaxLength(100)]
        public string Subject { get; set; }
        public string BodyText { get; set; }
        public string BodyHTML { get; set; }
        public string ProcessingError { get; set; }
        public DateTime? DateProcessed { get; set; }
        public DateTime DateCreated { get; set; }
        public Alias To { get; set; }
    }
}
