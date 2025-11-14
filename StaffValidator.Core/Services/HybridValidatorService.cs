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

        // Cache for compiled regex with timeout - key is "pattern|timeout" to support different timeouts
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Regex> _regexCache = new();

        private readonly int _maxConcurrentRegexMatches;

        // Default timeout for regex matching in milliseconds 
        public int RegexTimeoutMs { get; set; } = 200;
        [ThreadStatic]
        private static int _fallbackCounter;

        // Default timeout for regex matching in milliseconds (can be overridden via options)
        public int RegexTimeoutMs { get; set; } = 50;

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

        /// <summary>
        /// Gets or creates a cached compiled Regex with timeout for the given pattern.
        /// Cache key includes both pattern and timeout to support different timeout values.
        /// </summary>
        private Regex GetCachedRegexWithTimeout(string pattern)
        {
            var matchTimeout = TimeSpan.FromMilliseconds(RegexTimeoutMs > 0 ? RegexTimeoutMs : 50);
            // Cache key includes timeout to handle cases where timeout changes
            var cacheKey = $"{pattern}|{matchTimeout.TotalMilliseconds}";

            return _regexCache.GetOrAdd(cacheKey, _ =>
            {
                try
                {
                    // Compiled regex with timeout for optimal performance + security
                    return new Regex(pattern,
                        RegexOptions.Compiled | RegexOptions.CultureInvariant,
                        matchTimeout);
                }
                catch (ArgumentException)
                {
                    // If pattern is invalid, return a non-compiled version that will fail gracefully
                    return new Regex(pattern, RegexOptions.CultureInvariant, matchTimeout);
                }
            });
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

                    // Guardrail: if we detect a known catastrophic pattern and large input,
                    // skip regex entirely and go straight to safe fallback.
                    if (IsKnownCatastrophicPattern(attr) && (value?.Length ?? 0) > 200)
                    {
                        _logger?.LogWarning("Guardrail: skipping regex for catastrophic pattern {Pattern} with input length {Len}", attr.Pattern, value?.Length ?? 0);
                        _fallbackCounter++;
                        bool guardOk = TryDfaFallback(attr, value);
                        if (!guardOk)
                        {
                            errors.Add($"{p.Name}: validation failed (guardrail fallback) ({attr.Pattern})");
                        }
                        continue;
                    }
                    var entered = _semaphore.Wait(0);
                    if (!entered)
                    {
                        // fallback immediately if concurrency limit reached
                        _logger?.LogWarning("Regex concurrency limit reached ({Max}); falling back to DFA for property {Property} pattern {Pattern}", _maxConcurrentRegexMatches, p.Name, attr.Pattern);
                        _fallbackCounter++;
                        bool fallbackOk = TryDfaFallback(attr, value);
                        if (!fallbackOk)
                        {
                            errors.Add($"{p.Name}: validation skipped regex due to concurrency limit ({attr.Pattern})");
                        }
                        continue;
                    }

                    try
                    {
                        // Use cached compiled regex with timeout for better performance
                        var regex = GetCachedRegexWithTimeout(pattern);

                        if (regex.IsMatch(value))
                        {
                            // matched by regex
                            continue;
                        }

                        // regex did not match; try DFA fallback for known types if enabled
                        bool fallbackOk = false;
                        if (_enableDfaFallback)
                        {
                            _fallbackCounter++;
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
                            _fallbackCounter++;
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
                        _fallbackCounter++;
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

        private static bool IsKnownCatastrophicPattern(Attributes.RegexCheckAttribute attr)
        {
            var p = attr.Pattern ?? string.Empty;
            // Detect classic (a+)+ pattern (anchored or not)
            return p.Contains("(a+)+", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns the number of DFA fallbacks attempted on the current thread since the last consume, and resets the counter.
        /// </summary>
        public int ConsumeFallbackCount()
        {
            var c = _fallbackCounter;
            _fallbackCounter = 0;
            return c;
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

            // ReDoS demo: fallback for known catastrophic pattern (a+)+ -> language a+
            if (attr is Attributes.RegexCheckAttribute rAttr)
            {
                var p = rAttr.Pattern ?? string.Empty;
                if (p.Contains("(a+)+", StringComparison.Ordinal))
                {
                    try
                    {
                        bool result = true;
                        if (string.IsNullOrEmpty(value))
                        {
                            result = false;
                        }
                        else
                        {
                            for (int i = 0; i < value.Length; i++)
                            {
                                if (value[i] != 'a') { result = false; break; }
                            }
                        }
                        sw.Stop();
                        _logger?.LogWarning("⚠️ DFA fallback (redos) | pattern={Pattern} | inputLength={Len} | elapsedMs={Ms} | fallbackResult={Result}", pattern, inputLength, sw.ElapsedMilliseconds, result);
                        return result;
                    }
                    catch
                    {
                        sw.Stop();
                        _logger?.LogWarning("⚠️ DFA fallback (redos) failed | pattern={Pattern} | inputLength={Len} | elapsedMs={Ms}", pattern, inputLength, sw.ElapsedMilliseconds);
                        return false;
                    }
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
