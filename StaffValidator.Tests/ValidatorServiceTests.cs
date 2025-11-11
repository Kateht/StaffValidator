using StaffValidator.Core.Models;
using StaffValidator.Core.Services;
using System;
using Xunit;

namespace StaffValidator.Tests
{
    public class ValidatorServiceTests
    {
        [Fact]
        public void ValidateAll_ReturnsTrue_ForValidStaff()
        {
            var s = new Staff
            {
                StaffID = 1,
                StaffName = "Alice",
                Email = "alice@example.com",
                PhoneNumber = "+44 1234 567890",
                StartingDate = DateTime.UtcNow
            };

            var v = new ValidatorService();
            var (ok, errors) = v.ValidateAll(s);

            Assert.True(ok);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateAll_ReturnsFalse_ForInvalidEmail()
        {
            var s = new Staff
            {
                StaffID = 2,
                StaffName = "Bob",
                Email = "not-an-email",
                PhoneNumber = "+44 1234 567890",
                StartingDate = DateTime.UtcNow
            };

            var v = new ValidatorService();
            var (ok, errors) = v.ValidateAll(s);

            Assert.False(ok);
            Assert.Contains(errors, e => e.Contains("Email:"));
        }

        [Fact]
        public void ValidateAll_ReturnsFalse_ForInvalidPhone()
        {
            var s = new Staff
            {
                StaffID = 3,
                StaffName = "Eve",
                Email = "eve@example.com",
                PhoneNumber = "abc-not-phone",
                StartingDate = DateTime.UtcNow
            };

            var v = new ValidatorService();
            var (ok, errors) = v.ValidateAll(s);

            Assert.False(ok);
            Assert.Contains(errors, e => e.Contains("PhoneNumber:") || e.Contains("Phone:"));
        }
    }
}
