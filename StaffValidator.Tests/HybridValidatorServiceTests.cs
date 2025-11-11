using StaffValidator.Core.Attributes;
using StaffValidator.Core.Services;
using Microsoft.Extensions.Options;
using System;
using Xunit;

namespace StaffValidator.Tests
{
    public class HybridValidatorServiceTests
    {
        private class EmailHolderValid
        {
            [EmailCheck(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$")]
            public string Email { get; set; } = string.Empty;
        }

        private class EmailHolderInvalidRegex
        {
            // invalid regex will throw ArgumentException inside the regex engine
            [EmailCheck("[")]
            public string Email { get; set; } = string.Empty;
        }

        private class GenericHolderInvalidRegex
        {
            [RegexCheck("[")]
            public string Value { get; set; } = string.Empty;
        }

        [Fact]
        public void ValidateAll_EmailRegexMatches_ReturnsTrue()
        {
            var h = new EmailHolderValid { Email = "alice.johnson@company.com" };
            var svc = new HybridValidatorService(Options.Create(new HybridValidationOptions { RegexTimeoutMs = 200 }));
            var (ok, errors) = svc.ValidateAll(h);
            Assert.True(ok);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateAll_InvalidRegex_FallsBackToEmailAutomata_ReturnsTrue()
        {
            var h = new EmailHolderInvalidRegex { Email = "alice.johnson@company.com" };
            var svc = new HybridValidatorService(Options.Create(new HybridValidationOptions { RegexTimeoutMs = 200 }));
            var (ok, errors) = svc.ValidateAll(h);
            Assert.True(ok);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateAll_InvalidRegex_GenericProperty_NoFallback_ReturnsFalse()
        {
            var h = new GenericHolderInvalidRegex { Value = "some value" };
            var svc = new HybridValidatorService(Options.Create(new HybridValidationOptions { RegexTimeoutMs = 200 }));
            var (ok, errors) = svc.ValidateAll(h);
            Assert.False(ok);
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void ValidateAll_RegexTimesOut_FallsBackToEmailAutomata_ReturnsTrue()
        {
            // Create a deliberately pathological regex for the local-part using nested quantifiers
            // and a very long local part to trigger catastrophic backtracking.
            var pattern = @"([A-Za-z0-9]+)+@[A-Za-z0-9\-]+(\.[A-Za-z]{2,})+";

            var holder = new EmailHolderValid();
            // set a very long local part to make the regex expensive
            holder.Email = new string('a', 2000) + "@example.com";

            var svc = new HybridValidatorService(Options.Create(new HybridValidationOptions { RegexTimeoutMs = 1 }));
            // force a very small timeout so the regex match will likely time out
            svc.RegexTimeoutMs = 1;

            var (ok, errors) = svc.ValidateAll(new
            {
                // anonymous type won't pick up attributes, so create a small runtime type by reuse
            });

            // Instead, validate by creating a small wrapper type at runtime using the EmailCheck attribute
            var wrapper = new
            {
                Email = holder.Email
            };

            // The HybridValidatorService inspects attributes via reflection; so to test properly
            // we use the EmailHolderInvalidRegex pattern but replace its attribute at compile-time is not possible.
            // Instead, directly construct an object of a local class with the desired attribute pattern.
            var local = new LocalEmailTimeoutHolder(holder.Email, pattern);
            // already set via options; run validation
            var (ok2, errors2) = svc.ValidateAll(local);

            Assert.True(ok2);
            Assert.Empty(errors2);
        }

        // Helper local class with an EmailCheck attribute using a custom pattern
        private class LocalEmailTimeoutHolder
        {
            public LocalEmailTimeoutHolder(string email, string pattern)
            {
                Email = email;
                Pattern = pattern;
            }

            // store pattern only so attribute can reference a compile-time constant isn't feasible here;
            // so duplicate the attribute usage with the same pattern literal used above.
            [EmailCheck(@"([A-Za-z0-9]+)+@[A-Za-z0-9\-]+(\.[A-Za-z]{2,})+")]
            public string Email { get; set; }

            // keep pattern available for debugging
            public string Pattern { get; }
        }
    }
}
