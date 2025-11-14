using System;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using StaffValidator.Core.Services;
using StaffValidator.Core.Attributes;
using Xunit;

namespace StaffValidator.Tests
{
    public class HybridValidatorTests
    {
        private class EmailModel
        {
            [EmailCheck("\\S+@\\S+\\.\\S+")]
            public string Email { get; set; } = string.Empty;
        }

        private class PhoneModel
        {
            [PhoneCheck(@"^\+?[0-9\s\-]{6,15}$")]
            public string PhoneNumber { get; set; } = string.Empty;
        }

        private class EmailAndPhoneModel
        {
            [EmailCheck("\\S+@\\S+\\.\\S+")]
            public string Email { get; set; } = string.Empty;

            [PhoneCheck(@"^\+?[0-9\s\-]{6,15}$")]
            public string PhoneNumber { get; set; } = string.Empty;
        }

        private class CatastrophicModel
        {
            // pattern that can cause catastrophic backtracking: (a+)+b
            [EmailCheck("(a+)+b")]
            public string Payload { get; set; } = string.Empty;
        }

        [Fact]
        public void RegexValidInput_Passes()
        {
            var opts = Options.Create(new HybridValidationOptions { RegexTimeoutMs = 200, EnableDfaFallback = true });
            var logger = NullLogger<HybridValidatorService>.Instance;
            var svc = new HybridValidatorService(opts, logger);

            var model = new EmailModel { Email = "alice@example.com" };
            var (ok, errors) = svc.ValidateAll(model);
            Assert.True(ok);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidPhone_Passes()
        {
            var opts = Options.Create(new HybridValidationOptions { RegexTimeoutMs = 200, EnableDfaFallback = true });
            var logger = NullLogger<HybridValidatorService>.Instance;
            var svc = new HybridValidatorService(opts, logger);

            var model = new PhoneModel { PhoneNumber = "+44 7000 000000" };
            var (ok, errors) = svc.ValidateAll(model);
            Assert.True(ok);
            Assert.Empty(errors);
        }

        [Fact]
        public void BothEmailAndPhone_ValidInput_Passes()
        {
            var opts = Options.Create(new HybridValidationOptions { RegexTimeoutMs = 200, EnableDfaFallback = true });
            var logger = NullLogger<HybridValidatorService>.Instance;
            var svc = new HybridValidatorService(opts, logger);

            var model = new EmailAndPhoneModel 
            { 
                Email = "test@example.com",
                PhoneNumber = "+1 555 1234567"
            };
            var (ok, errors) = svc.ValidateAll(model);
            Assert.True(ok);
            Assert.Empty(errors);
        }

        [Fact]
        public void BothEmailAndPhone_InvalidEmail_Fails()
        {
            var opts = Options.Create(new HybridValidationOptions { RegexTimeoutMs = 200, EnableDfaFallback = true });
            var logger = NullLogger<HybridValidatorService>.Instance;
            var svc = new HybridValidatorService(opts, logger);

            var model = new EmailAndPhoneModel 
            { 
                Email = "not-an-email",
                PhoneNumber = "+1 555 1234567"
            };
            var (ok, errors) = svc.ValidateAll(model);
            Assert.False(ok);
            Assert.Contains(errors, e => e.Contains("Email"));
        }

        [Fact]
        public void BothEmailAndPhone_InvalidPhone_Fails()
        {
            var opts = Options.Create(new HybridValidationOptions { RegexTimeoutMs = 200, EnableDfaFallback = true });
            var logger = NullLogger<HybridValidatorService>.Instance;
            var svc = new HybridValidatorService(opts, logger);

            var model = new EmailAndPhoneModel 
            { 
                Email = "valid@example.com",
                PhoneNumber = "invalid-phone"
            };
            var (ok, errors) = svc.ValidateAll(model);
            Assert.False(ok);
            Assert.Contains(errors, e => e.Contains("Phone"));
        }

        [Fact]
        public void BothEmailAndPhone_BothInvalid_Fails()
        {
            var opts = Options.Create(new HybridValidationOptions { RegexTimeoutMs = 200, EnableDfaFallback = true });
            var logger = NullLogger<HybridValidatorService>.Instance;
            var svc = new HybridValidatorService(opts, logger);

            var model = new EmailAndPhoneModel 
            { 
                Email = "not-an-email",
                PhoneNumber = "not-a-phone"
            };
            var (ok, errors) = svc.ValidateAll(model);
            Assert.False(ok);
            Assert.Contains(errors, e => e.Contains("Email"));
            Assert.Contains(errors, e => e.Contains("Phone"));
            Assert.Equal(2, errors.Count); // Both should fail
        }

        [Fact]
        public void TimeoutTriggersFallback_ButValidEmailStillPasses()
        {
            var opts = Options.Create(new HybridValidationOptions { RegexTimeoutMs = 1, EnableDfaFallback = true });
            var logger = NullLogger<HybridValidatorService>.Instance;
            var svc = new HybridValidatorService(opts, logger);

            // valid email should pass even if regex times out because DFA fallback is enabled
            var model = new EmailModel { Email = "user+tag@example.co.uk" };
            var (ok, errors) = svc.ValidateAll(model);
            Assert.True(ok);
        }

        [Fact]
        public void CatastrophicPattern_WithTimeout_UsesFallbackOrFailsGracefully()
        {
            var opts = Options.Create(new HybridValidationOptions { RegexTimeoutMs = 1, EnableDfaFallback = true });
            var logger = NullLogger<HybridValidatorService>.Instance;
            var svc = new HybridValidatorService(opts, logger);

            // create a long 'a' string that would cause backtracking when no 'b' present
            var longInput = new string('a', 3000);
            var model = new CatastrophicModel { Payload = longInput };

            var (ok, errors) = svc.ValidateAll(model);
            // DFA fallback for Email pattern likely does not accept this payload, so it's acceptable to be invalid
            Assert.False(ok);
        }
    }
}
