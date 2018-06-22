using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityWs.Controllers;
using IdentityWs.Jobs;
using IdentityWs.Models;
using IdentityWs.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests
{
    [TestClass]
    public class AliasesControllerTests
    {
        static readonly ILogger<AliasesController> dummyLog = new Mock<ILogger<AliasesController>>().Object;
        static readonly IBackgroundJobRunner<EmailQueueProcessor> dummyRunner = new Mock<IBackgroundJobRunner<EmailQueueProcessor>>().Object;
        static readonly IUtcNow now;

        static AliasesControllerTests() {
            Mock<IUtcNow> mock = new Mock<IUtcNow>();
            mock.Setup(u => u.UtcNow).Returns(DateTime.Parse("2018-03-12 08:30:52Z"));
            now = mock.Object;
        }

        [TestMethod]
        public async Task UnconfirmedAlias_Index_ConfirmationToken()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias { EmailAddress = "email@test.org" });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.Index("email@test.org");

                result.Should().BeOfType<JsonResult>()
                    .Which.Value.Should().BeOfType<Dictionary<string, string>>()
                        .Which["confirmToken"].Should().NotBeNull("the alias has not yet been confirmed");
            }
        }

        [TestMethod]
        public async Task ConfirmedAlias_Index_NullConfirmationToken()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias {
                    EmailAddress = "email@test.org",
                    DateConfirmed = now.UtcNow
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.Index("email@test.org");

                result.Should().BeOfType<JsonResult>()
                    .Which.Value.Should().BeOfType<Dictionary<string, string>>()
                        .Which["confirmToken"].Should().BeNull("the alias has already been confirmed");
            }
        }

        [TestMethod]
        public async Task NoAlias_IndexGet_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.Index("email@test.org");

                result.Should().BeOfType<NotFoundResult>("the alias does not exist in the database");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPostWithExistingEmail_Conflict()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias { EmailAddress = "email@test.org" });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.IndexPost("email@test.org", new AliasesController.IndexPostRequestBody
                {
                    password = "p@ssword1"
                });

                result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict,
                    "the alias exists in the database");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPostWithPassword_NoContentWithNewAliasAndBeing()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias { EmailAddress = "email@test.org" });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.IndexPost("different@email.org", new AliasesController.IndexPostRequestBody
                {
                    password = "p@ssword1"
                });

                result.Should().BeOfType<NoContentResult>("a non-existing email address was supplied along with a password");
                ef.Aliases.Include(a => a.Being).Should().Contain(a => a.EmailAddress == "different@email.org", "a new alias should have been created")
                    .Which.Being.Should().NotBeNull("a new being should have been created");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPostWithPasswordAndOtherEmail_BadRequest()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias { EmailAddress = "email@test.org" });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPostRequestBody body = new AliasesController.IndexPostRequestBody
                {
                    otherEmailAddress = "email@test.org",
                    password = "p@ssword1"
                };
                IActionResult result = await patient.IndexPost("different@email.org", body);

                result.Should().BeOfType<BadRequestResult>($"exactly one of '{nameof(body.otherEmailAddress)}' or '{nameof(body.password)} is required'");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPostWithNeitherPasswordNorEmail_BadRequest()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias { EmailAddress = "email@test.org" });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPostRequestBody body = new AliasesController.IndexPostRequestBody();
                IActionResult result = await patient.IndexPost("different@email.org", body);

                result.Should().BeOfType<BadRequestResult>($"exactly one of '{nameof(body.otherEmailAddress)}' or '{nameof(body.password)} is required'");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPostWithOtherEmail_NoContentWithNewAliasAndLinkedBeing()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being()
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPostRequestBody body = new AliasesController.IndexPostRequestBody
                {
                    otherEmailAddress = "email@test.org"
                };
                IActionResult result = await patient.IndexPost("different@email.org", body);

                result.Should().BeOfType<NoContentResult>("an existing email address (only) was supplied");
                ef.Aliases.Include(a => a.Being).Should().Contain(a => a.EmailAddress == "different@email.org", "a new alias should have been created")
                    .Which.Being.Aliases.Should().HaveCount(2, "the new alias should have been linked to the existing being");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPostWithWrongOtherEmail_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org"
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPostRequestBody body = new AliasesController.IndexPostRequestBody
                {
                    otherEmailAddress = "non@sum.net"
                };
                IActionResult result = await patient.IndexPost("different@email.org", body);

                result.Should().BeOfType<NotFoundResult>($"the '{nameof(body.otherEmailAddress)}' does not exist");
            }
        }

        [TestMethod]
        public async Task NoAlias_IndexPatch_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPatchRequestBody body = new AliasesController.IndexPatchRequestBody
                {
                    password = "password1"
                };
                IActionResult result = await patient.IndexPatch("email@test.org", body);

                result.Should().BeOfType<NotFoundResult>("the alias does not exist in the database");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPatchNoOldPasswordOrResetToken_BadRequest()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being()
                });
                await ef.SaveChangesAsync();

                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPatchRequestBody body = new AliasesController.IndexPatchRequestBody
                {
                    password = "password1"
                };
                IActionResult result = await patient.IndexPatch("email@test.org", body);

                result.Should().BeOfType<BadRequestResult>($"neither {nameof(body.resetToken)} nor {nameof(body.oldPassword)} was provided");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPatchMismatchingOld_NotAuthorized()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword("password1")
                    }
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPatchRequestBody body = new AliasesController.IndexPatchRequestBody
                {
                    oldPassword = "password2",
                    password = "p@ssword1"
                };
                IActionResult result = await patient.IndexPatch("email@test.org", body);

                result.Should().BeOfType<UnauthorizedResult>($"'{nameof(body.oldPassword)}' does not match");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPatchMismatchingReset_NotAuthorized()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        PasswordResetToken = "abracadabra",
                        PasswordResetTokenValidUntil = now.UtcNow.AddHours(1)
                    }
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPatchRequestBody body = new AliasesController.IndexPatchRequestBody
                {
                    resetToken = "wrong",
                    password = "p@ssword1"
                };
                IActionResult result = await patient.IndexPatch("email@test.org", body);

                result.Should().BeOfType<UnauthorizedResult>($"'{nameof(body.resetToken)}' does not match");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPatchExpiredReset_NotAuthorized()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        PasswordResetToken = "abracadabra",
                        PasswordResetTokenValidUntil = now.UtcNow
                    }
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPatchRequestBody body = new AliasesController.IndexPatchRequestBody
                {
                    resetToken = "abracadabra",
                    password = "p@ssword1"
                };
                IActionResult result = await patient.IndexPatch("email@test.org", body);

                result.Should().BeOfType<UnauthorizedResult>($"the reset token is expired");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPatchMatchingReset__NoContentWithChangedPassword()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Being being = new Being
                {
                    PasswordResetToken = "abracadabra",
                    PasswordResetTokenValidUntil = now.UtcNow.AddMilliseconds(1)
                };
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = being
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPatchRequestBody body = new AliasesController.IndexPatchRequestBody
                {
                    resetToken = "abracadabra",
                    password = "p@ssword1"
                };
                IActionResult result = await patient.IndexPatch("email@test.org", body);

                result.Should().BeOfType<NoContentResult>($"'{nameof(body.resetToken)}' matched");
                Sha512Util.TestPassword("p@ssword1", being.SaltedHashedPassword).Should().BeTrue("the password should have been changed");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPatchOldSameAsNew_BadRequest()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword("password1")
                    }
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPatchRequestBody body = new AliasesController.IndexPatchRequestBody
                {
                    oldPassword = "password1",
                    password = "password1"
                };
                IActionResult result = await patient.IndexPatch("email@test.org", body);

                result.Should().BeOfType<BadRequestResult>($"'{nameof(body.password)}' must differ from '{nameof(body.oldPassword)}'");
            }
        }

        [TestMethod]
        public async Task Alias_IndexPatchMatchingOld_NoContentWithChangedPassword()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Being being = new Being
                {
                    SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword("password1")
                };
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = being
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                AliasesController.IndexPatchRequestBody body = new AliasesController.IndexPatchRequestBody
                {
                    oldPassword = "password1",
                    password = "p@ssword1"
                };
                IActionResult result = await patient.IndexPatch("email@test.org", body);

                result.Should().BeOfType<NoContentResult>($"'{nameof(body.oldPassword)}' matched");
                Sha512Util.TestPassword("p@ssword1", being.SaltedHashedPassword).Should().BeTrue("the password should have been changed");
            }
        }

        [TestMethod]
        public async Task NoAliasNoClient_Clients_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.Clients("nonexistent@alias.org", "nonexistent@client.org");

                result.Should().BeOfType<NotFoundResult>("the alias does not exist");
            }
        }

        [TestMethod]
        public async Task AliasNoClient_Clients_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being()
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.Clients("email@test.org", "nonexistent@client.org");

                result.Should().BeOfType<NotFoundResult>("the client does not exist");
            }
        }

        [TestMethod]
        public async Task AliasClientWithoutData_Clients_EmptyJsonArray()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being {
                        Clients = new[] {
                            new BeingClient {
                                ClientName = "testclient"
                            }
                        }
                    }
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.Clients("email@test.org", "testclient");

                result.Should().BeOfType<JsonResult>()
                    .Which.Value.Should().BeOfType<List<BeingClientDatum>>()
                        .Which.Should().HaveCount(0, "there is no data for the client and being");
            }
        }

        [TestMethod]
        public async Task AliasClientWithData_Clients_DataAsJsonArray()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                BeingClientDatum[] data = new[] {
                    new BeingClientDatum {
                        Key = "key1",
                        Value = "value1"
                    },
                    new BeingClientDatum {
                        Key = "key2",
                        Value = "value2"
                    }
                };
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        Clients = new[] {
                            new BeingClient {
                                ClientName = "testclient",
                                Data = data
                            }
                        }
                    }
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.Clients("email@test.org", "testclient");

                result.Should().BeOfType<JsonResult>()
                    .Which.Value.Should().BeOfType<List<BeingClientDatum>>()
                        .Which.ShouldBeEquivalentTo(data, "such data are attested for the being and client");
            }
        }

        [TestMethod]
        public async Task NoBeing_ClientsPost_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.ClientsPost("nonexistent@being.org", "testclient", new Dictionary<string, string>());

                result.Should().BeOfType<NotFoundResult>("the being does not exist");
            }
        }

        [TestMethod]
        public async Task BeingAndClient_ClientsPost_Conflict()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        Clients = new[] {
                            new BeingClient {
                                ClientName = "testclient"
                            }
                        }
                    }
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.ClientsPost("email@test.org", "testclient", new Dictionary<string, string>());

                result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict,
                    "the client already exists for the being");
            }
        }

        [TestMethod]
        public async Task Being_ClientsPost_NoContentWithNewBeingClientAndData()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Dictionary<string, string> expected_data = new Dictionary<string, string>() {
                    ["key1"] = "value1",
                    ["key2"] = "value2",
                };
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being()
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.ClientsPost("email@test.org", "testclient", expected_data);

                result.Should().BeOfType<NoContentResult>("no such client existed previously for the being");
                Dictionary<string, string> actual_data = new Dictionary<string, string>();
                await ef.BeingClientData
                    .Where(d => d.BeingClient.Being.Aliases.Any(a => a.EmailAddress == "email@test.org"))
                    .ForEachAsync(d => actual_data.Add(d.Key, d.Value));
                actual_data.ShouldBeEquivalentTo(expected_data, "such data were provided with the POST request");
            }
        }

        [TestMethod]
        public async Task NoBeing_ResetPost_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.ResetPost("nonexistent@being.org");

                result.Should().BeOfType<NotFoundResult>("the being does not exist");
            }
        }

        [TestMethod]
        public async Task BeingAndResetToken_ResetPost_JsonNewResetToken()
        {
            string old_tok = "Prev";
            using (IdentityWsDbContext ef = CreateEf()) {
                Being being = new Being
                {
                    PasswordResetToken = old_tok,
                    PasswordResetTokenValidUntil = now.UtcNow.AddMinutes(2)
                };
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = being
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.ResetPost("email@test.org");

                result.Should().BeOfType<JsonResult>()
                    .Which.Value.Should().BeOfType<Dictionary<string, string>>()
                        .Which["resetToken"].Should().NotBe(old_tok, "a new token should have been generated");
                being.PasswordResetTokenValidUntil.Should().Be(now.UtcNow.AddHours(1), "the validity period should also reset");
            }
        }

        [TestMethod]
        public async Task NoAlias_Confirm_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.ConfirmPost("email@test.org", new AliasesController.ConfirmPostRequestBody
                {
                    confirmToken = "abc"
                });

                result.Should().BeOfType<NotFoundResult>("the alias does not exist");
            }
        }

        [TestMethod]
        public async Task UnconfirmedAliasWithMismatchingToken_Confirm_Unauthorized()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Alias alias;
                ef.Aliases.Add(alias = new Alias {
                    EmailAddress = "email@test.org",
                    ConfirmationToken = "abc"
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.ConfirmPost("email@test.org", new AliasesController.ConfirmPostRequestBody
                {
                    confirmToken = "αβγ"
                });

                result.Should().BeOfType<UnauthorizedResult>("the confirmation token does not match");
            }
        }

        [TestMethod]
        public async Task UnconfirmedAlias_Confirm_NoContentWithUpdatedDateConfirmed()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Alias alias;
                ef.Aliases.Add(alias = new Alias { EmailAddress = "email@test.org" });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.ConfirmPost("email@test.org", new AliasesController.ConfirmPostRequestBody
                {
                    confirmToken = alias.ConfirmationToken
                });

                result.Should().BeOfType<NoContentResult>("the confirmation token matches");
                alias.DateConfirmed.Should().Be(now.UtcNow, "the alias is newly confirmed");
            }
        }

        [TestMethod]
        public async Task ConfirmedAlias_Confirm_NoContentWithNoUpdate()
        {
            DateTime yesterday = now.UtcNow.AddDays(-1);
            using (IdentityWsDbContext ef = CreateEf()) {
                Alias alias;
                ef.Aliases.Add(alias = new Alias
                {
                    EmailAddress = "email@test.org",
                    DateConfirmed = yesterday
                });
                await ef.SaveChangesAsync();
                AliasesController patient = new AliasesController(ef, dummyLog, now, dummyRunner);

                IActionResult result = await patient.ConfirmPost("email@test.org", new AliasesController.ConfirmPostRequestBody {
                    confirmToken = alias.ConfirmationToken
                });

                result.Should().BeOfType<NoContentResult>("the confirmation token matches");
                alias.DateConfirmed.Should().Be(yesterday, "the previous confirmation date should not be overridden");
            }
        }

        // Return a DB context for an in-memory database which is scoped to the calling method.
        IdentityWsDbContext CreateEf([CallerMemberName] string caller = null) =>
            new IdentityWsDbContext(new DbContextOptionsBuilder<IdentityWsDbContext>()
                .UseInMemoryDatabase($"{GetType().Name}.{caller}")
                .Options);
    }
}
