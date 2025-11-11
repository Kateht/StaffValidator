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
    public class HybridValidatorConcurrencyTests
    {
        // Simple test logger that records messages for assertions
        private class TestLogger<T> : ILogger<T>
        {
            public ConcurrentBag<string> Messages { get; } = new ConcurrentBag<string>();

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                try
                {
                    var msg = formatter != null ? formatter(state, exception) : state?.ToString();
                    if (!string.IsNullOrEmpty(msg)) Messages.Add(msg);
                }
                catch { }
            }
        }

        // Local holder with an email attribute using an intentionally pathological pattern
        private class LocalPathologicalEmailHolder
        {
            public LocalPathologicalEmailHolder(string email)
            {
                Email = email;
            }

            // pattern deliberately uses nested quantifiers to increase backtracking risk
            [EmailCheck(@"([A-Za-z0-9]+)+@[A-Za-z0-9\-]+(\.[A-Za-z]{2,})+")]
            public string Email { get; set; }
        }

        [Fact]
        public void ParallelValidations_ExhaustSemaphore_ProduceFallbackLogs()
        {
            // Arrange
            var logger = new TestLogger<HybridValidatorService>();
            var options = Options.Create(new HybridValidationOptions { RegexTimeoutMs = 25, MaxConcurrentRegexMatches = 2 });
            var svc = new HybridValidatorService(options, logger);

            // Create a pathological email with a very long local part to force expensive regex behavior
            var longLocal = new string('a', 3000);
            var email = longLocal + "@example.com";
            var holder = new LocalPathologicalEmailHolder(email);

            var results = new ConcurrentBag<(bool ok, string[] errors)>();

            // Act: run many validations in parallel to try to exceed the semaphore limit
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            {
                var r = svc.ValidateAll(holder);
                results.Add((r.ok, r.errors?.ToArray() ?? Array.Empty<string>()));
            })).ToArray();

            Task.WaitAll(tasks);

            // Assert: at least one fallback log message must have been produced
            var combined = string.Join("\n", logger.Messages);
            var hasConcurrency = logger.Messages.Any(m => m.Contains("Regex concurrency limit reached", StringComparison.OrdinalIgnoreCase));
            var hasTimeout = logger.Messages.Any(m => m.Contains("Regex match timeout", StringComparison.OrdinalIgnoreCase));
            var hasInvalid = logger.Messages.Any(m => m.Contains("Invalid regex", StringComparison.OrdinalIgnoreCase));

            Assert.True(hasConcurrency || hasTimeout || hasInvalid, "Expected at least one fallback log (concurrency/timeout/invalid regex) but none were found. Logs:\n" + combined);

            // And validations should have completed (some or all may succeed via DFA fallback)
            Assert.Equal(10, results.Count);
            Assert.All(results, r => Assert.True(r.ok || r.errors.Length > 0));
        }
    }
}
