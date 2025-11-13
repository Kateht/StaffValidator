using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StaffValidator.Core.Attributes;
using StaffValidator.Core.Services;
using Xunit;

namespace StaffValidator.Tests
{

    /// Additional focused tests to assert that HybridValidatorService emits the expected
    /// log messages for timeout + DFA fallback and invalid regex scenarios.

    public class HybridValidatorLoggingTests
    {
        private class TestLogger<T> : ILogger<T>
        {
            public ConcurrentBag<string> Messages { get; } = new();
            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                try
                {
                    var msg = formatter != null ? formatter(state, exception) : state?.ToString();
                    if (!string.IsNullOrEmpty(msg))
                    {
                        Messages.Add(msg);
                    }
                }
                catch { }
            }
            private class NullScope : IDisposable { public static readonly NullScope Instance = new(); public void Dispose() { } }
        }

        // Model with catastrophic backtracking prone pattern (missing trailing 'b')
        private class TimeoutEmailModel
        {
            [EmailCheck("(a+)+b")] // Will take long on long 'a' sequences without 'b'
            public string Email { get; set; } = string.Empty;
        }

        private class InvalidRegexEmailModel
        {
            [EmailCheck("[")] // Invalid pattern
            public string Email { get; set; } = string.Empty;
        }

        [Fact]
        public void Timeout_TriggersWarning_And_DfaFallbackLog()
        {
            var logger = new TestLogger<HybridValidatorService>();
            var opts = Options.Create(new HybridValidationOptions
            {
                RegexTimeoutMs = 1,                // extremely small to force timeout
                MaxConcurrentRegexMatches = 2,
                EnableDfaFallback = true
            });
            var svc = new HybridValidatorService(opts, logger);

            var model = new TimeoutEmailModel { Email = new string('a', 4000) }; // long string of 'a'
            var (ok, errors) = svc.ValidateAll(model);

            // We don't assert ok/invalid strictly; focus is logging of timeout path + fallback attempt.
            Assert.Contains(logger.Messages, m => m.Contains("Regex match timeout", StringComparison.OrdinalIgnoreCase));
            Assert.True(logger.Messages.Any(m => m.Contains("DFA fallback", StringComparison.OrdinalIgnoreCase)),
                "Expected a DFA fallback log after timeout but none found. Logs:\n" + string.Join("\n", logger.Messages));
        }

        [Fact]
        public void InvalidRegex_LogsInvalid_And_AttemptsFallback()
        {
            var logger = new TestLogger<HybridValidatorService>();
            var opts = Options.Create(new HybridValidationOptions
            {
                RegexTimeoutMs = 200,
                MaxConcurrentRegexMatches = 2,
                EnableDfaFallback = true
            });
            var svc = new HybridValidatorService(opts, logger);

            var model = new InvalidRegexEmailModel { Email = "valid@example.com" };
            var (ok, errors) = svc.ValidateAll(model);

            // Since pattern is invalid, fallback should validate email via DFA and succeed
            Assert.True(ok);
            Assert.Empty(errors);
            Assert.Contains(logger.Messages, m => m.Contains("Invalid regex for property", StringComparison.OrdinalIgnoreCase));
            Assert.True(logger.Messages.Any(m => m.Contains("DFA fallback", StringComparison.OrdinalIgnoreCase)),
                "Expected a DFA fallback log after invalid regex but none found. Logs:\n" + string.Join("\n", logger.Messages));
        }
    }
}
