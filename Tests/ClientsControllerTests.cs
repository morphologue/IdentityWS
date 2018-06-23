using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityWs.Controllers;
using IdentityWs.Models;
using IdentityWs.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests
{
    [TestClass]
    public class ClientsControllerTests : EfTestBase
    {
        static readonly ILogger<ClientsController> dummyLog = new Mock<ILogger<ClientsController>>().Object;
        static readonly IUtcNow now;
        static readonly IConfiguration config;

        static ClientsControllerTests() {
            Mock<IUtcNow> mock = new Mock<IUtcNow>();
            mock.Setup(u => u.UtcNow).Returns(DateTime.Parse("2018-03-12 08:30:52Z"));
            now = mock.Object;

            // "LockoutPeriodMins": 15
            // "MaxFailedLoginsBeforeLockout": 2
            Mock<IConfigurationSection> mock_section1 = new Mock<IConfigurationSection>();
            mock_section1.Setup(m => m.Value).Returns("15");
            Mock<IConfigurationSection> mock_section2 = new Mock<IConfigurationSection>();
            mock_section2.Setup(m => m.Value).Returns("2");
            Mock<IConfiguration> mock_config = new Mock<IConfiguration>();
            mock_config.Setup(m => m.GetSection("LockoutPeriodMins")).Returns(mock_section1.Object);
            mock_config.Setup(m => m.GetSection("MaxFailedLoginsBeforeLockout")).Returns(mock_section2.Object);
            config = mock_config.Object;
        }

        [TestMethod]
        public async Task NoAliasNoClient_Index_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.Index("nonexistent@alias.org", "nonexistent@client.org");

                result.Should().BeOfType<NotFoundResult>("the alias does not exist");
            }
        }

        [TestMethod]
        public async Task AliasNoClient_Index_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Aliases.Add(new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being()
                });
                await ef.SaveChangesAsync();
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.Index("email@test.org", "nonexistent@client.org");

                result.Should().BeOfType<NotFoundResult>("the client does not exist");
            }
        }

        [TestMethod]
        public async Task AliasClientWithoutData_Index_EmptyJsonArray()
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
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.Index("email@test.org", "testclient");

                result.Should().BeOfType<JsonResult>()
                    .Which.Value.Should().BeOfType<Dictionary<string, string>>()
                        .Which.Should().HaveCount(0, "there is no data for the client and being");
            }
        }

        [TestMethod]
        public async Task AliasClientWithData_Index_DataAsJsonArray()
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
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.Index("email@test.org", "testclient");

                result.Should().BeOfType<JsonResult>()
                    .Which.Value.Should().BeOfType<Dictionary<string, string>>()
                        .Which.ShouldBeEquivalentTo(new Dictionary<string, string>
                        {
                            ["key1"] = "value1",
                            ["key2"] = "value2"
                        }, "such data are attested for the being and client");
            }
        }

        [TestMethod]
        public async Task NoBeing_IndexPost_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.IndexPost("nonexistent@being.org", "testclient", new Dictionary<string, string>());

                result.Should().BeOfType<NotFoundResult>("the being does not exist");
            }
        }

        [TestMethod]
        public async Task BeingAndClient_IndexPost_Conflict()
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
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.IndexPost("email@test.org", "testclient", new Dictionary<string, string>());

                result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status409Conflict,
                    "the client already exists for the being");
            }
        }

        [TestMethod]
        public async Task Being_IndexPost_NoContentWithNewBeingClientAndData()
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
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.IndexPost("email@test.org", "testclient", expected_data);

                result.Should().BeOfType<NoContentResult>("no such client existed previously for the being");
                Dictionary<string, string> actual_data = new Dictionary<string, string>();
                await ef.BeingClientData
                    .Where(d => d.BeingClient.Being.Aliases.Any(a => a.EmailAddress == "email@test.org"))
                    .ForEachAsync(d => actual_data.Add(d.Key, d.Value));
                actual_data.ShouldBeEquivalentTo(expected_data, "such data were provided with the POST request");
            }
        }

        [TestMethod]
        public async Task OneClient_IndexDelete_NoContentWithCascadingDeleteOfBeing()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Beings.Add(new Being
                {
                    Aliases = new HashSet<Alias>
                    {
                        new Alias
                        {
                            EmailAddress = "test@test.org"
                            // Cascading from the Alias is tested in AliasesControllerTests.
                        },
                    },
                    Clients = new HashSet<BeingClient>
                    {
                        new BeingClient
                        {
                            ClientName = "testclient",
                            Data = new HashSet<BeingClientDatum>
                            {
                                new BeingClientDatum
                                {
                                    Key = "key",
                                    Value = "value"
                                }
                            }
                        }
                    }
                });
                await ef.SaveChangesAsync();
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.IndexDelete("test@test.org", "testclient");

                result.Should().BeOfType<NoContentResult>("the being may be deleted");
                (await ef.Aliases.AnyAsync()).Should().Be(false, "the deletion should cascade to aliases");
                (await ef.BeingClients.AnyAsync()).Should().Be(false, "the deletion should cascade to clients");
                (await ef.BeingClientData.AnyAsync()).Should().Be(false, "the deletion should cascade to client data");
            }
        }

        [TestMethod]
        public async Task TwoClients_IndexDelete_NoContentWithCascadingDeleteOfClient()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                ef.Beings.Add(new Being
                {
                    Aliases = new HashSet<Alias>
                    {
                        new Alias
                        {
                            EmailAddress = "test@test.org"
                            // Cascading from the Alias is tested in AliasesControllerTests.
                        },
                    },
                    Clients = new HashSet<BeingClient>
                    {
                        new BeingClient
                        {
                            ClientName = "testclient1"
                        },
                        new BeingClient
                        {
                            ClientName = "testclient2"
                        }
                    }
                });
                await ef.SaveChangesAsync();
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.IndexDelete("test@test.org", "testclient1");

                result.Should().BeOfType<NoContentResult>("the client may be deleted");
                (await ef.Beings.CountAsync()).Should().Be(1, "the deletion should not have affected the being");
                (await ef.BeingClients.CountAsync()).Should().Be(1, "one of the two clients should have been deleted");
            }
        }

        [TestMethod]
        public async Task WrongAlias_Login_NotFound()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                Alias alias;
                ef.Aliases.Add(alias = new Alias
                {
                    EmailAddress = "email@test.org"
                });
                await ef.SaveChangesAsync();
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.Login("wrong@test.org", "testing", new ClientsController.LoginRequestBody());

                result.Should().BeOfType<NotFoundResult>("the email address doesn't match");
            }
        }

        [TestMethod]
        public async Task WrongClient_Login_NotFound()
        {
            const string PASSWORD = "abracadabra", CLIENT = "testing";
            using (IdentityWsDbContext ef = CreateEf()) {
                Alias alias;
                ef.Aliases.Add(alias = new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword(PASSWORD),
                        Clients = new HashSet<BeingClient>
                        {
                            new BeingClient { ClientName = CLIENT }
                        }
                    }
                });
                await ef.SaveChangesAsync();
                ClientsController patient = new ClientsController(ef, dummyLog, now, null);

                IActionResult result = await patient.Login("email@test.org", "wrong", new ClientsController.LoginRequestBody
                {
                    password = PASSWORD
                });

                result.Should().BeOfType<NotFoundResult>("the client does not match");
            }
        }

        [TestMethod]
        public async Task WrongPassword_Login_UnauthorizedAndDbFalse()
        {
            const string PASSWORD = "abracadabra", CLIENT = "testing";
            using (IdentityWsDbContext ef = CreateEf()) {
                Alias alias;
                ef.Aliases.Add(alias = new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword(PASSWORD),
                        Clients = new HashSet<BeingClient>
                        {
                            new BeingClient { ClientName = CLIENT }
                        }
                    },
                    LoginAttempts = new List<LoginAttempt>
                    {
                        new LoginAttempt
                        {
                            // This is too old to count.
                            DateCreated = now.UtcNow.AddMinutes(-15).AddMilliseconds(-100)
                        },
                        new LoginAttempt
                        {
                            DateCreated = now.UtcNow.AddMinutes(-5)
                        }
                    }
                });
                await ef.SaveChangesAsync();
                ClientsController patient = new ClientsController(ef, dummyLog, now, config);

                IActionResult result = await patient.Login("email@test.org", CLIENT, new ClientsController.LoginRequestBody
                {
                    password = "wrong"
                });
                LoginAttempt attempt = await ef.LoginAttempts.FirstAsync(a => a.AliasID == alias.AliasID);

                result.Should().BeOfType<UnauthorizedResult>("the password doesn't match");
                (await ef.LoginAttempts.CountAsync()).Should().Be(3, "a new record should be added");
                (await ef.LoginAttempts.LastAsync()).Should().Match<LoginAttempt>(a => !a.Success && a.ClientName == CLIENT,
                    "such a client was supplied");
            }
        }

        [TestMethod]
        public async Task RightPassword_Login_NoContentAndDbTrue()
        {
            const string PASSWORD = "abracadabra", CLIENT = "testing";
            using (IdentityWsDbContext ef = CreateEf()) {
                Alias alias;
                ef.Aliases.Add(alias = new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword(PASSWORD),
                        Clients = new HashSet<BeingClient>
                        {
                            new BeingClient { ClientName = CLIENT }
                        }
                    },
                    LoginAttempts = new List<LoginAttempt>
                    {
                        new LoginAttempt
                        {
                            DateCreated = now.UtcNow.AddMinutes(-7)
                        },
                        new LoginAttempt
                        {
                            DateCreated = now.UtcNow.AddMinutes(-6)
                        },
                        new LoginAttempt
                        {
                            DateCreated = now.UtcNow.AddMinutes(-5),
                            // The account is not locked because this success breaks the sequence.
                            Success = true
                        },
                        new LoginAttempt
                        {
                            DateCreated = now.UtcNow.AddMinutes(-4)
                        }
                    }
                });
                await ef.SaveChangesAsync();
                ClientsController patient = new ClientsController(ef, dummyLog, now, config);

                IActionResult result = await patient.Login("email@test.org", CLIENT, new ClientsController.LoginRequestBody
                {
                    password = PASSWORD
                });

                result.Should().BeOfType<NoContentResult>("the password matches");
                (await ef.LoginAttempts.CountAsync()).Should().Be(5, "a new record should be added");
                (await ef.LoginAttempts.LastAsync()).Should().Match<LoginAttempt>(a => a.Success && a.ClientName == CLIENT,
                    "such a client was supplied");
            }
        }

        [TestMethod]
        public async Task WrongPasswordLockedOut_Login_ServiceUnavailable()
        {
            const string PASSWORD = "abracadabra", CLIENT = "testing";
            using (IdentityWsDbContext ef = CreateEf()) {
                Alias alias;
                ef.Aliases.Add(alias = new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword(PASSWORD),
                        Clients = new HashSet<BeingClient>
                        {
                            new BeingClient { ClientName = CLIENT }
                        }
                    },
                    LoginAttempts = new List<LoginAttempt>
                    {
                        new LoginAttempt
                        {
                            DateCreated = now.UtcNow.AddMinutes(-5)
                        },
                        new LoginAttempt
                        {
                            DateCreated = now.UtcNow.AddMinutes(-5)
                        },
                    }
                });
                await ef.SaveChangesAsync();
                ClientsController patient = new ClientsController(ef, dummyLog, now, config);

                IActionResult result = await patient.Login("email@test.org", CLIENT, new ClientsController.LoginRequestBody
                {
                    password = "wrong"
                });

                result.Should().BeOfType<StatusCodeResult>()
                    .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable, "the being is locked");
                (await ef.LoginAttempts.CountAsync()).Should().Be(2, "no new record should be added");
            }
        }

        [TestMethod]
        public async Task RightPasswordLockedOut_Login_ServiceUnavailable()
        {
            const string PASSWORD = "abracadabra", CLIENT = "testing";
            using (IdentityWsDbContext ef = CreateEf()) {
                Alias alias;
                ef.Aliases.Add(alias = new Alias
                {
                    EmailAddress = "email@test.org",
                    Being = new Being
                    {
                        SaltedHashedPassword = Sha512Util.SaltAndHashNewPassword(PASSWORD),
                        Clients = new HashSet<BeingClient>
                        {
                            new BeingClient { ClientName = CLIENT }
                        }
                    },
                    LoginAttempts = new List<LoginAttempt>
                    {
                        new LoginAttempt
                        {
                            DateCreated = now.UtcNow.AddMinutes(-15)
                        },
                        new LoginAttempt
                        {
                            DateCreated = now.UtcNow.AddMinutes(-4)
                        }
                    }
                });
                await ef.SaveChangesAsync();
                ClientsController patient = new ClientsController(ef, dummyLog, now, config);

                IActionResult result = await patient.Login("email@test.org", CLIENT, new ClientsController.LoginRequestBody
                {
                    password = PASSWORD
                });

                result.Should().BeOfType<StatusCodeResult>()
                    .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable, "the being is locked");
                (await ef.LoginAttempts.CountAsync()).Should().Be(2, "no new record should be added");
            }
        }
    }
}
