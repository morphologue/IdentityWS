using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace IdentityWs.Jobs
{
    // Send an email.
    public class EmailSender : IEmailSender
    {
        IConfiguration config;
        SmtpClient smtp;

        public EmailSender(IConfiguration config)
        {
            this.config = config;
        }

        public void Send(string from, string reply_to, string to, string subject, string text, string html)
        {
            // Connect lazily: this instance might not actually be used.
            if (smtp == null) {
                this.smtp = new SmtpClient();
                smtp.Connect(config["SmtpHost"], options: SecureSocketOptions.None);
            }

            MimeMessage msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(from));
            if (!string.IsNullOrEmpty(reply_to))
                msg.ReplyTo.Add(new MailboxAddress(reply_to));
            msg.To.Add(new MailboxAddress(to));
            msg.Subject = subject;
            BodyBuilder builder = new BodyBuilder();
            builder.TextBody = text;
            builder.HtmlBody = html;
            msg.Body = builder.ToMessageBody();
            smtp.Send(msg);
        }

        public void Dispose()
        {
            if (smtp != null) {
                smtp.Disconnect(true);
                smtp.Dispose();
                smtp = null;
            }
        }
    }
}
