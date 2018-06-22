using System;
using System.ComponentModel.DataAnnotations;

namespace IdentityWs.Models
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
        public bool SendIfUnconfirmed { get; set; }
        public int ProcessingCount { get; set; }
        public string LastProcessingError { get; set; }
        public DateTime? DateLastProcessed { get; set; }
        public DateTime DateCreated { get; set; }
        public Alias To { get; set; }
    }
}
