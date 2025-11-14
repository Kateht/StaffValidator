using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace StaffValidator.Core.Services
{
    /// Hybrid validation service: try a Regex match first (with timeout),
    /// and if that fails (invalid regex, exception or timeout) fall back
    /// to a pragmatic DFA/NFA implementation for known patterns (email, phone).
    /// Returns tuple (ok, errors).
    public class HybridValidatorService : ValidatorService
    {
        private readonly ValidatorService _regexService = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SimpleNfa> _dfaCache = new();

        private readonly int _maxConcurrentRegexMatches;

        // Default timeout for regex matching in milliseconds 
        public int RegexTimeoutMs { get; set; } = 200;

        private readonly System.Threading.SemaphoreSlim _semaphore;
        private readonly ILogger<HybridValidatorService> _logger;
        private readonly bool _enableDfaFallback = true;

        public HybridValidatorService(IOptions<HybridValidationOptions> options, ILogger<HybridValidatorService> logger)
        {
            _logger = logger;
            if (options?.Value != null)
            {
                if (options.Value.RegexTimeoutMs > 0)
                {
                    RegexTimeoutMs = options.Value.RegexTimeoutMs;
                }

                var max = options.Value.MaxConcurrentRegexMatches > 0 ? options.Value.MaxConcurrentRegexMatches : 4;
                _maxConcurrentRegexMatches = max;
                _semaphore = new System.Threading.SemaphoreSlim(max, max);
                _enableDfaFallback = options.Value.EnableDfaFallback;
            }
            else
            {
                _maxConcurrentRegexMatches = 4;
                _semaphore = new System.Threading.SemaphoreSlim(4, 4);
            }
        }

        // Backwards-compatible constructor used by tests or callers that don't provide a logger
        public HybridValidatorService(IOptions<HybridValidationOptions> options)
            : this(options, Microsoft.Extensions.Logging.Abstractions.NullLogger<HybridValidatorService>.Instance)
        {
        }

        public override (bool ok, List<string> errors) ValidateAll(object obj)
        {
            var errors = new List<string>();

            foreach (var p in obj.GetType().GetProperties())
            {
                var attr = p.GetCustomAttribute<Attributes.RegexCheckAttribute>();
                if (attr == null)
                {
                    continue;
                }

                var value = p.GetValue(obj)?.ToString() ?? string.Empty;
                var pattern = attr.Pattern;
                if (!pattern.StartsWith("^"))
                {
                    pattern = "^" + pattern;
                }
                if (!pattern.EndsWith("$"))
                {
                    pattern += "$";
                }

                try
                {
                    // Use the Regex constructor that accepts a match timeout so the engine itself
                    // will throw RegexMatchTimeoutException on pathological inputs instead of
                    // relying on Task cancellation. This avoids leaving worker threads blocked.
                    // Attempt to acquire a slot for running a potentially expensive regex match.
                    // If we cannot acquire immediately, fall back to automata to avoid queueing expensive work.
                    var entered = _semaphore.Wait(0);
                    if (!entered)
                    {
                        // fallback immediately if concurrency limit reached
                        _logger?.LogWarning("Regex concurrency limit reached ({Max}); falling back to DFA for property {Property} pattern {Pattern}", _maxConcurrentRegexMatches, p.Name, attr.Pattern);
                        bool fallbackOk = TryDfaFallback(attr, value);
                        if (!fallbackOk)
                        {
                            errors.Add($"{p.Name}: validation skipped regex due to concurrency limit ({attr.Pattern})");
                        }
                        continue;
                    }

                    try
                    {
                        var matchTimeout = TimeSpan.FromMilliseconds(RegexTimeoutMs > 0 ? RegexTimeoutMs : 200);
                        var regex = new Regex(pattern, RegexOptions.CultureInvariant, matchTimeout);

                        if (regex.IsMatch(value))
                        {
                            // matched by regex
                            continue;
                        }

                        // regex did not match; try DFA fallback for known types if enabled
                        bool fallbackOk = false;
                        if (_enableDfaFallback)
                        {
                            fallbackOk = TryDfaFallback(attr, value);
                        }

                        if (!fallbackOk)
                        {
                            errors.Add($"{p.Name}: invalid format ({attr.Pattern})");
                        }
                    }
                    finally
                    {
                        try { _semaphore.Release(); } catch { }
                    }
                }
                catch (ArgumentException)
                {
                    // invalid regex - try DFA fallback
                    _logger?.LogWarning("Invalid regex for property {Property}: {Pattern}.", p.Name, attr.Pattern);
                    if (_enableDfaFallback)
                    {
                        bool fallbackOk = TryDfaFallback(attr, value);
                        if (!fallbackOk)
                        {
                            errors.Add($"{p.Name}: invalid regex/format ({attr.Pattern})");
                        }
                    }
                    else
                    {
                        errors.Add($"{p.Name}: invalid regex ({attr.Pattern})");
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Timeout occurred while evaluating the regex; fallback to DFA
                    _logger?.LogWarning("Regex match timeout ({Timeout}ms) for property {Property} pattern {Pattern}.", RegexTimeoutMs, p.Name, attr.Pattern);
                    if (_enableDfaFallback)
                    {
                        bool fallbackOk = TryDfaFallback(attr, value);
                        if (!fallbackOk)
                        {
                            errors.Add($"{p.Name}: regex timeout/validation failed ({attr.Pattern})");
                        }
                    }
                    else
                    {
                        errors.Add($"{p.Name}: regex timeout ({attr.Pattern})");
                    }
                }
            }

            return (errors.Count == 0, errors);
        }

        private bool TryDfaFallback(Attributes.RegexCheckAttribute attr, string value)
        {
            var pattern = attr.Pattern;
            var inputLength = value?.Length ?? 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Only pragmatic fallbacks supported for email and phone attributes
            if (attr is Attributes.EmailCheckAttribute)
            {
                try
                {
                    var nfa = _dfaCache.GetOrAdd("email", _ => AutomataFactory.BuildEmailNfa());
                    var result = nfa.Simulate(value ?? string.Empty);
                    sw.Stop();
                    _logger?.LogWarning("⚠️ DFA fallback result | pattern={Pattern} | inputLength={Len} | elapsedMs={Ms} | fallbackResult={Result}", pattern, inputLength, sw.ElapsedMilliseconds, result);
                    return result;
                }
                catch
                {
                    sw.Stop();
                    _logger?.LogWarning("⚠️ DFA fallback failed for pattern={Pattern} | inputLength={Len} | elapsedMs={Ms}", pattern, inputLength, sw.ElapsedMilliseconds);
                    return false;
                }
            }

            if (attr is Attributes.PhoneCheckAttribute)
            {
                try
                {
                    var nfa = _dfaCache.GetOrAdd("phone", _ => AutomataFactory.BuildPhoneNfa());
                    var result = nfa.Simulate(value ?? string.Empty);
                    sw.Stop();
                    _logger?.LogWarning("⚠️ DFA fallback result | pattern={Pattern} | inputLength={Len} | elapsedMs={Ms} | fallbackResult={Result}", pattern, inputLength, sw.ElapsedMilliseconds, result);
                    return result;
                }
                catch
                {
                    sw.Stop();
                    _logger?.LogWarning("⚠️ DFA fallback failed for pattern={Pattern} | inputLength={Len} | elapsedMs={Ms}", pattern, inputLength, sw.ElapsedMilliseconds);
                    return false;
                }
            }

            // no fallback available
            return false;
        }
    }
}
