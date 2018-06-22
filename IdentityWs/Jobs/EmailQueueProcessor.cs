using System;
using System.Linq;
using System.Net.Mail;
using IdentityWs.Models;
using IdentityWs.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityWs.Jobs
{
    // A job which will be executed periodically in the context of an IServiceScope
    public class EmailQueueProcessor : IBackgroundJob
    {
        ILogger<EmailQueueProcessor> log;
        IUtcNow now;

        EmailQueueProcessor(ILogger<EmailQueueProcessor> log, IUtcNow now)
        {
            this.log = log;
            this.now = now;
        }

        public void Run(IServiceProvider services)
        {
            using (IEmailSender sender = services.GetRequiredService<IEmailSender>())
            using (IdentityWsDbContext ef = services.GetRequiredService<IdentityWsDbContext>()) {
                foreach (Email email in ef.Emails
                        .Include(e => e.To)
                        .Where(e => !e.DateLastProcessed.HasValue || !string.IsNullOrEmpty(e.LastProcessingError))
                        .ToList()) {
                    if (ShouldBackOff(email)) {
                        log.LogInformation("Not processing email {EmailID} due to back-off policy", email.EmailID);
                        continue;
                    }

                    if (!email.To.DateConfirmed.HasValue && !email.SendIfUnconfirmed) {
                        log.LogInformation("Not sending email {EmailID} as the alias is unconfirmed", email.EmailID);
                        email.LastProcessingError = "Unconfirmed";
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
                    ef.SaveChanges();
                }
            }
        }

        // Implement an exponential back-off policy for failures.
        bool ShouldBackOff(Email email)
        {
            if (!email.DateLastProcessed.HasValue)
                // Never processed
                return false;
            if (email.ProcessingCount > 10)
                // Give up.
                return true;
            DateTime earliest_ok = email.DateLastProcessed.Value.AddMinutes(Math.Pow(2, email.ProcessingCount));
            return DateTime.UtcNow < earliest_ok;
        }
    }
}
