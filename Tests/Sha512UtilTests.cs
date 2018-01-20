using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using IdentityWS.Utils;

namespace Tests
{
    [TestClass]
    public class Sha512UtilTests
    {
        [TestMethod]
        public void MatchingPasswordAndHash_TestPassword_ReturnsTrue()
        {
            Sha512Util.TestPassword("Hello world!", "$6$saltstring$svn8UoSVapNtMuq1ukKS4tPQd8iKwSMHWjl/O817G3uBnIFNjnQJuesI68u4OTLiBFdcbYEdFCoEOfaS35inz1")
                .Should().BeTrue("the hash matches the password");
        }

        [TestMethod]
        public void MismatchingPasswordAndHash_TestPassword_ReturnsFalse()
        {
            Sha512Util.TestPassword("Hello worldx!", "$6$saltstring$svn8UoSVapNtMuq1ukKS4tPQd8iKwSMHWjl/O817G3uBnIFNjnQJuesI68u4OTLiBFdcbYEdFCoEOfaS35inz1")
                .Should().BeFalse("the hash does not match the password");
        }

        [TestMethod]
        public void PasswordAndSalt_Crypt_ReturnsExpectedHash()
        {
            Sha512Util.Crypt("Hello world!", "$6$saltstring").Should()
                .Be("$6$saltstring$svn8UoSVapNtMuq1ukKS4tPQd8iKwSMHWjl/O817G3uBnIFNjnQJuesI68u4OTLiBFdcbYEdFCoEOfaS35inz1",
                    "that's what the PHP test says");
        }

        [TestMethod]
        public void PasswordAndDifferentSalt_Crypt_ReturnsDifferentHash()
        {
            Sha512Util.Crypt("Hello world!", "$6$saltsdiffs").Should()
                .NotEndWith("svn8UoSVapNtMuq1ukKS4tPQd8iKwSMHWjl/O817G3uBnIFNjnQJuesI68u4OTLiBFdcbYEdFCoEOfaS35inz1",
                    "the salt has been changed");
        }

        [TestMethod]
        public void Password_SaltAndHashNewPasswordTwice_ReturnsDifferentHashes() {
            const string PASSWORD = "w0rd";
            Sha512Util.SaltAndHashNewPassword(PASSWORD).Should().NotBe(Sha512Util.SaltAndHashNewPassword(PASSWORD),
                "a random salt should be used for each invocation");
        }
    }
}
