

using System;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityWs.Controllers;
using IdentityWs.Jobs;
using IdentityWs.Models;
using IdentityWs.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests
{
    [TestClass]
    public class EmailQueueProcessorTests : EfTestBase
    {
        static readonly ILogger<EmailQueueProcessor> dummyLog = new Mock<ILogger<EmailQueueProcessor>>().Object;
        static readonly IUtcNow now;

        static EmailQueueProcessorTests()
        {
            Mock<IUtcNow> mock = new Mock<IUtcNow>();
            mock.Setup(u => u.UtcNow).Returns(DateTime.Parse("2018-03-12 08:30:52Z"));
            now = mock.Object;
        }

        [TestMethod]
        public async Task ProhibitedUnconfirmed_Run_FailsPermanently()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Email email;
                ef.Emails.Add(email = new Email
                {
                    To = new Alias()
                });
                await ef.SaveChangesAsync();
                EmailQueueProcessor patient = new EmailQueueProcessor(dummyLog, now);
                bool email_sent = false;
                Mock<IEmailSender> mock_sender = new Mock<IEmailSender>();
                mock_sender.Setup(m => m.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string, string, string, string>((a, b, c, d, e, f) => email_sent = true);
                IServiceProvider services = MakeServices(mock_sender.Object, ef);

                patient.Run(services, null);

                email.Should().Match<Email>(e =>
                        e.ProcessingCount == 10
                        && e.DateLastProcessed == now.UtcNow
                        && e.LastProcessingError == "Unconfirmed",
                    "the alias is unconfirmed and Email.SendIfUnconfirmed is false");
                email_sent.Should().BeFalse("an error ocurred");
            }
        }

        [TestMethod]
        public async Task AllowedUnconfirmed_Run_SendsEmail()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Email email;
                ef.Emails.Add(email = new Email
                {
                    To = new Alias(),
                    SendIfUnconfirmed = true
                });
                await ef.SaveChangesAsync();
                EmailQueueProcessor patient = new EmailQueueProcessor(dummyLog, now);
                bool email_sent = false;
                Mock<IEmailSender> mock_sender = new Mock<IEmailSender>();
                mock_sender.Setup(m => m.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string, string, string, string>((a, b, c, d, e, f) => email_sent = true);
                IServiceProvider services = MakeServices(mock_sender.Object, ef);

                patient.Run(services, null);

                email.Should().Match<Email>(e =>
                        e.ProcessingCount == 1
                        && e.DateLastProcessed == now.UtcNow
                        && string.IsNullOrEmpty(e.LastProcessingError),
                    "although the alias is unconfirmed, Email.SendIfUnconfirmed is true");
                email_sent.Should().BeTrue("the email can be sent");
            }
        }

        [TestMethod]
        public async Task Confirmed_Run_SendsEmail()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Email email;
                ef.Emails.Add(email = new Email
                {
                    To = new Alias
                    {
                        DateConfirmed = now.UtcNow
                    }
                });
                await ef.SaveChangesAsync();
                EmailQueueProcessor patient = new EmailQueueProcessor(dummyLog, now);
                bool email_sent = false;
                Mock<IEmailSender> mock_sender = new Mock<IEmailSender>();
                mock_sender.Setup(m => m.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string, string, string, string>((a, b, c, d, e, f) => email_sent = true);
                IServiceProvider services = MakeServices(mock_sender.Object, ef);

                patient.Run(services, null);

                email.Should().Match<Email>(e =>
                        e.ProcessingCount == 1
                        && e.DateLastProcessed == now.UtcNow
                        && string.IsNullOrEmpty(e.LastProcessingError),
                    "the alias is confirmed");
                email_sent.Should().BeTrue("the email can be sent");
            }
        }

        [TestMethod]
        public async Task WithinBackoffPeriod_Run_DoesNotProcess()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                DateTime last_processed = now.UtcNow.AddMinutes(-16).AddMilliseconds(10);
                Email email;
                ef.Emails.Add(email = new Email
                {
                    To = new Alias
                    {
                        DateConfirmed = now.UtcNow
                    },
                    ProcessingCount = 4, // Processing should be allowed 16 mins after error (2^^4).
                    LastProcessingError = "some weird error",
                    DateLastProcessed = last_processed
                });
                await ef.SaveChangesAsync();
                EmailQueueProcessor patient = new EmailQueueProcessor(dummyLog, now);
                bool email_sent = false;
                Mock<IEmailSender> mock_sender = new Mock<IEmailSender>();
                mock_sender.Setup(m => m.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string, string, string, string>((a, b, c, d, e, f) => email_sent = true);
                IServiceProvider services = MakeServices(mock_sender.Object, ef);

                patient.Run(services, null);

                email.Should().Match<Email>(e =>
                        e.ProcessingCount == 4
                        && e.DateLastProcessed == last_processed,
                    "the last processing date is within the back-off period");
                email_sent.Should().BeFalse("the email was not processed");
            }
        }

        [TestMethod]
        public async Task OutsideBackoffPeriod_Run_SendsEmail()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                DateTime last_processed = now.UtcNow.AddMinutes(-16);
                Email email;
                ef.Emails.Add(email = new Email
                {
                    To = new Alias
                    {
                        DateConfirmed = now.UtcNow
                    },
                    ProcessingCount = 4, // Processing should be allowed 16 mins after error (2^^4).
                    LastProcessingError = "some weird error",
                    DateLastProcessed = last_processed
                });
                await ef.SaveChangesAsync();
                EmailQueueProcessor patient = new EmailQueueProcessor(dummyLog, now);
                bool email_sent = false;
                Mock<IEmailSender> mock_sender = new Mock<IEmailSender>();
                mock_sender.Setup(m => m.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string, string, string, string>((a, b, c, d, e, f) => email_sent = true);
                IServiceProvider services = MakeServices(mock_sender.Object, ef);

                patient.Run(services, null);

                email.Should().Match<Email>(e =>
                        e.ProcessingCount == 5
                        && e.DateLastProcessed == now.UtcNow
                        && string.IsNullOrEmpty(e.LastProcessingError),
                    "the last processing date is (just) outside the back-off period");
                email_sent.Should().BeTrue("the email can be sent");
            }
        }

        [TestMethod]
        public async Task ProcessedUnerrored_Run_DoesNotResend()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Email email;
                ef.Emails.Add(email = new Email
                {
                    To = new Alias
                    {
                        DateConfirmed = now.UtcNow
                    },
                    ProcessingCount = 1,
                    DateLastProcessed = now.UtcNow
                });
                await ef.SaveChangesAsync();
                EmailQueueProcessor patient = new EmailQueueProcessor(dummyLog, now);
                bool email_sent = false;
                Mock<IEmailSender> mock_sender = new Mock<IEmailSender>();
                mock_sender.Setup(m => m.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string, string, string, string>((a, b, c, d, e, f) => email_sent = true);
                IServiceProvider services = MakeServices(mock_sender.Object, ef);

                patient.Run(services, null);

                email.Should().Match<Email>(e =>
                        e.ProcessingCount == 1
                        && e.DateLastProcessed == now.UtcNow
                        && string.IsNullOrEmpty(e.LastProcessingError),
                    "the email was already sent successfully in the past");
                email_sent.Should().BeFalse("the email should not be re-sent");
            }
        }

        [TestMethod]
        public async Task ExceptionDuringSend_Run_SavesMessageAndContinues()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Email email1, email2;
                ef.Emails.AddRange(new[] {
                    email1 = new Email
                    {
                        To = new Alias
                        {
                            DateConfirmed = now.UtcNow
                        },
                        BodyText = "Hello"
                    }, email2 = new Email
                    {
                        To = new Alias
                        {
                            DateConfirmed = now.UtcNow
                        },
                        BodyText = "World"
                    }
                });
                await ef.SaveChangesAsync();
                EmailQueueProcessor patient = new EmailQueueProcessor(dummyLog, now);
                string emailed_text = null;
                bool called_back = false;
                Mock<IEmailSender> mock_sender = new Mock<IEmailSender>();
                mock_sender.Setup(m => m.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Callback<string, string, string, string, string, string>((a, b, c, d, e, f) => {
                        if (!called_back) {
                            called_back = true;
                            throw new Exception("Exceptional!");
                        }
                        emailed_text = e;
                    });
                IServiceProvider services = MakeServices(mock_sender.Object, ef);

                patient.Run(services, null);

                email1.Should().Match<Email>(e =>
                        e.ProcessingCount == 1
                        && e.DateLastProcessed == now.UtcNow
                        && e.LastProcessingError == "Exceptional!",
                    "an exception was thrown during sending");
                email2.Should().Match<Email>(e =>
                        e.ProcessingCount == 1
                        && e.DateLastProcessed == now.UtcNow
                        && string.IsNullOrEmpty(e.LastProcessingError),
                    "sending should succeed");
                emailed_text.Should().Be("World", "that's the subject of email2");
            }
        }

        IServiceProvider MakeServices(IEmailSender sender, IdentityWsDbContext ef)
        {
            Mock<IServiceProvider> mock_provider = new Mock<IServiceProvider>();
            mock_provider.Setup(m => m.GetService(It.IsAny<Type>()))
                .Returns<Type>(t => t.IsAssignableFrom(ef.GetType()) ? (object)ef : sender);
            return mock_provider.Object;
        }
    }
}
