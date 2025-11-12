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
