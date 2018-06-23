using System;
using System.Linq;
using System.Net.Mail;
using IdentityWs.Models;
using IdentityWs.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityWs.Jobs
{
    public class EmailQueueProcessor : IBackgroundJob
    {
        const int MAX_PROCESSING_COUNT = 9;

        ILogger<EmailQueueProcessor> log;
        IUtcNow now;

        public EmailQueueProcessor(ILogger<EmailQueueProcessor> log, IUtcNow now)
        {
            this.log = log;
            this.now = now;
        }

        public void Run(IServiceProvider services, IConfigurationSection section)
        {
            IEmailSender sender = services.GetRequiredService<IEmailSender>();
            IdentityWsDbContext ef = services.GetRequiredService<IdentityWsDbContext>();
            foreach (Email email in ef.Emails
                        .Include(e => e.To)
                        .Where(e => !e.DateLastProcessed.HasValue
                            || (!string.IsNullOrEmpty(e.LastProcessingError) && e.ProcessingCount <= MAX_PROCESSING_COUNT))
                        .ToList()) {
                if (ShouldBackOff(email)) {
                    log.LogInformation("Not processing email {EmailID} due to back-off policy", email.EmailID);
                    continue;
                }

                if (!email.To.DateConfirmed.HasValue && !email.SendIfUnconfirmed) {
                    log.LogInformation("Not sending email {EmailID} as the alias is unconfirmed", email.EmailID);
                    email.LastProcessingError = "Unconfirmed";
                    // Make the failure permanent.
                    email.ProcessingCount = MAX_PROCESSING_COUNT;
                } else {
                    try {
                        sender.Send(email.From, email.ReplyTo, email.To.EmailAddress, email.Subject, email.BodyText, email.BodyHTML);
                        email.LastProcessingError = null;
                    } catch (Exception e) {
                        log.LogError(e, "Exception while sending email {EmailID}", email.EmailID);
                        email.LastProcessingError = e.Message;
                    }
                }
                email.DateLastProcessed = now.UtcNow;
                email.ProcessingCount++;
                ef.SaveChanges();
            }
        }

        // Implement an exponential back-off policy for failures.
        bool ShouldBackOff(Email email)
        {
            if (!email.DateLastProcessed.HasValue)
                // Never processed
                return false;
            DateTime earliest_ok = email.DateLastProcessed.Value.AddMinutes(Math.Pow(2, email.ProcessingCount));
            return now.UtcNow < earliest_ok;
        }
    }
}
