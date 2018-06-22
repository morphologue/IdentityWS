using System;

namespace IdentityWs.Jobs
{
    // Send an email.
    public interface IEmailSender : IDisposable
    {
        void Send(string from, string reply_to, string to, string subject, string text, string html);
    }
}
