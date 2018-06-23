using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using IdentityWs.Jobs;
using IdentityWs.Models;
using IdentityWs.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Tests
{
    [TestClass]
    public class TableCleanerTests : EfTestBase
    {
        static readonly IUtcNow now;
        static readonly IConfigurationSection jobSection;

        static TableCleanerTests()
        {
            Mock<IUtcNow> mock = new Mock<IUtcNow>();
            mock.Setup(u => u.UtcNow).Returns(DateTime.Parse("2018-03-12 08:30:52Z"));
            now = mock.Object;

            IConfigurationRoot root = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["TableCleaner:DeleteCreatedBeforeDays:LoginAttempt"] = "2"
                })
                .Build();
            jobSection = root.GetSection("TableCleaner");
        }

        [TestMethod]
        public async Task OldAndNewRecords_Run_DeletesOld()
        {
            using (IdentityWsDbContext ef = CreateEf()) {
                LoginAttempt retire, retire_boundary, keep_boundary, keep;
                retire = new LoginAttempt
                {
                    ClientName = "test",
                    DateCreated = now.UtcNow.AddDays(-3)
                };
                retire_boundary = new LoginAttempt
                {
                    ClientName = "test",
                    DateCreated = now.UtcNow.AddDays(-2).AddMilliseconds(-100)
                };
                keep_boundary = new LoginAttempt
                {
                    ClientName = "test",
                    DateCreated = now.UtcNow.AddDays(-2)
                };
                keep = new LoginAttempt
                {
                    ClientName = "test",
                    DateCreated = now.UtcNow.AddDays(-1)
                };
                ef.Aliases.Add(new Alias
                {
                    LoginAttempts = new HashSet<LoginAttempt>(new[] { retire, retire_boundary, keep_boundary, keep })
                });
                await ef.SaveChangesAsync();
                Mock<IServiceProvider> mock_provider = new Mock<IServiceProvider>();
                mock_provider.Setup(m => m.GetService(typeof(IdentityWsDbContext))).Returns(ef);
                TableCleaner<LoginAttempt> patient = new TableCleaner<LoginAttempt>(now);

                patient.Run(mock_provider.Object, jobSection);

                ef.LoginAttempts.ShouldBeEquivalentTo(new[] { keep_boundary, keep },
                    "records older than two days should have been deleted");
            }
        }
    }
}
