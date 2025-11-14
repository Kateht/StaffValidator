using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StaffValidator.Core.Services;
using StaffValidator.Core.Attributes;

namespace StaffValidator.Core.Benchmark
{
    /// <summary>
    /// Executes benchmark tests for different validation strategies and collects performance metrics.
    /// </summary>
    public class BenchmarkRunner
    {
        private readonly ILogger<BenchmarkRunner>? _logger;

        public BenchmarkRunner(ILogger<BenchmarkRunner>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Runs a comprehensive benchmark comparing multiple validation strategies.
        /// </summary>
        /// <param name="hybridService">Hybrid validator service instance</param>
        /// <param name="dataset">Test dataset</param>
        /// <param name="type">Dataset type (email, phone, redos)</param>
        /// <returns>Benchmark summary with results for all methods</returns>
        public async Task<BenchmarkSummary> RunAsync(
            HybridValidatorService hybridService,
            List<string> dataset,
            string type)
        {
            // Use ReDoS dataset if type is redos
            if (type.ToLowerInvariant() == "redos" && (dataset == null || dataset.Count == 0))
            {
                dataset = RedosDatasetGenerator.GenerateEmailRedosAttacks();
                _logger?.LogInformation("Generated ReDoS attack dataset with {Count} samples", dataset.Count);
            }

            _logger?.LogInformation("🚀 Starting benchmark: Type={Type}, Samples={Count}", type, dataset.Count);

            var totalStopwatch = Stopwatch.StartNew();
            var results = new List<BenchmarkResult>();

            // Get pattern based on type
            var pattern = GetPatternForType(type);

            // Prepare ground-truth expected results per input for Accuracy%
            var expected = ComputeExpected(dataset, type);

            // 1. Regex Uncached
            _logger?.LogInformation("Running: Regex Uncached");
            var regexUncachedResult = await BenchmarkRegexUncached(dataset, pattern, expected);
            results.Add(regexUncachedResult);

            // 2. Regex Cached
            _logger?.LogInformation("Running: Regex Cached");
            var regexCachedResult = await BenchmarkRegexCached(dataset, pattern, expected);
            results.Add(regexCachedResult);

            // 3. Hybrid (Regex → DFA fallback)
            _logger?.LogInformation("Running: Hybrid Validation");
            var hybridResult = await BenchmarkHybrid(hybridService, dataset, type, expected);
            results.Add(hybridResult);

            // 4. DFA Only
            _logger?.LogInformation("Running: DFA Only");
            var dfaResult = await BenchmarkDfaOnly(dataset, type, expected);
            results.Add(dfaResult);

            totalStopwatch.Stop();

            var summary = new BenchmarkSummary
            {
                DatasetType = type,
                SampleCount = dataset.Count,
                Results = results,
                TotalDurationMs = totalStopwatch.Elapsed.TotalMilliseconds,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["Pattern"] = pattern,
                    ["MachineName"] = Environment.MachineName,
                    ["ProcessorCount"] = Environment.ProcessorCount.ToString()
                }
            };

            _logger?.LogInformation(
                "✅ Benchmark completed: Duration={Duration:F2}ms, Methods={MethodCount}",
                summary.TotalDurationMs,
                results.Count);

            return summary;
        }

        private async Task<BenchmarkResult> BenchmarkRegexUncached(List<string> dataset, string pattern, List<bool> expected)
        {
            var times = new List<double>();
            int successCount = 0;
            int correctCount = 0;

            foreach (var input in dataset)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    // Create new regex each time (uncached)
                    var regex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                    var match = regex.IsMatch(input);
                    successCount += match ? 1 : 0;
                    if (expected.Count > 0)
                    {
                        if (match == expected[times.Count]) correctCount++;
                    }
                }
                catch (Exception)
                {
                    // Ignore errors for benchmarking
                }
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }

            return CalculateResult("Regex Uncached", times, successCount, dataset.Count, 0, pattern, correctCount);
        }

        private async Task<BenchmarkResult> BenchmarkRegexCached(List<string> dataset, string pattern, List<bool> expected)
        {
            var times = new List<double>();
            int successCount = 0;
            int correctCount = 0;

            // Pre-compile regex once
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

            // Warm-up: exercise the compiled regex a few times to JIT hot paths
            try
            {
                if (dataset.Count > 0) _ = regex.IsMatch(dataset[0]);
                if (dataset.Count > 1) _ = regex.IsMatch(dataset[1]);
            }
            catch { /* ignore warm-up errors */ }

            foreach (var input in dataset)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var match = regex.IsMatch(input);
                    successCount += match ? 1 : 0;
                    if (expected.Count > 0)
                    {
                        if (match == expected[times.Count]) correctCount++;
                    }
                }
                catch (Exception)
                {
                    // Ignore errors
                }
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }

            return CalculateResult("Regex Cached", times, successCount, dataset.Count, 0, pattern, correctCount);
        }

        private async Task<BenchmarkResult> BenchmarkHybrid(
            HybridValidatorService hybridService,
            List<string> dataset,
            string type,
            List<bool> expected)
        {
            var times = new List<double>();
            int successCount = 0;
            int fallbackCount = 0;
            int correctCount = 0;

            // Create a test model class dynamically
            var testModel = CreateTestModel(type);

            foreach (var input in dataset)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    // Set the property value
                    var property = testModel.GetType().GetProperty("TestValue");
                    property?.SetValue(testModel, input);

                    // Validate using hybrid service
                    // reset per-call fallback counter by consuming before validate
                    hybridService.ConsumeFallbackCount();
                    var (isValid, errors) = hybridService.ValidateAll(testModel);
                    fallbackCount += hybridService.ConsumeFallbackCount();
                    
                    successCount += isValid ? 1 : 0;
                    if (expected.Count > 0)
                    {
                        if (isValid == expected[times.Count]) correctCount++;
                    }
                    
                    // Check if fallback was used (look for warning in errors or log)
                    // Note: This is simplified - in production you'd track this via metrics
                }
                catch (Exception)
                {
                    // Ignore errors
                }
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }

            await Task.CompletedTask; // Make async method happy

            return CalculateResult("Hybrid (Regex→DFA)", times, successCount, dataset.Count, fallbackCount, GetPatternForType(type), correctCount);
        }

        private async Task<BenchmarkResult> BenchmarkDfaOnly(List<string> dataset, string type, List<bool> expected)
        {
            var times = new List<double>();
            int successCount = 0;
            int correctCount = 0;

            var lowered = type.ToLowerInvariant();

            foreach (var input in dataset)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    // Include BOTH build cost and matching cost per input
                    bool result;
                    if (lowered == "email")
                    {
                        var nfa = AutomataFactory.BuildEmailNfa();
                        result = nfa.Simulate(input);
                    }
                    else if (lowered == "phone")
                    {
                        var nfa = AutomataFactory.BuildPhoneNfa();
                        result = nfa.Simulate(input);
                    }
                    else if (lowered == "redos")
                    {
                        // Build a simple NFA for a+ to account for build cost
                        var s0 = new NfaState(0);
                        var s1 = new NfaState(1) { IsAccept = true };
                        s0.AddTransition('a', s1);
                        s1.AddTransition('a', s1);
                        var nfa = new SimpleNfa(s0, new[] { s0, s1 });
                        result = nfa.Simulate(input);
                    }
                    else
                    {
                        // Default: treat as email pattern
                        var nfa = AutomataFactory.BuildEmailNfa();
                        result = nfa.Simulate(input);
                    }
                    successCount += result ? 1 : 0;
                    if (expected.Count > 0)
                    {
                        if (result == expected[times.Count]) correctCount++;
                    }
                }
                catch (Exception)
                {
                    // Ignore errors
                }
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }

            return CalculateResult("DFA Only", times, successCount, dataset.Count, 0, GetPatternForType(type), correctCount);
        }

        private BenchmarkResult CalculateResult(
            string method,
            List<double> times,
            int successCount,
            int totalSamples,
            int fallbackCount,
            string pattern,
            int correctCount)
        {
            var avg = times.Average();
            var variance = times.Select(t => Math.Pow(t - avg, 2)).Average();
            var stdDev = Math.Sqrt(variance);

            return new BenchmarkResult
            {
                Method = method,
                AvgMs = avg,
                StdDevMs = stdDev,
                MinMs = times.Min(),
                MaxMs = times.Max(),
                FallbackPercentage = (fallbackCount / (double)totalSamples) * 100,
                AccuracyPercentage = totalSamples > 0 ? (correctCount / (double)totalSamples) * 100.0 : 0.0,
                TotalSamples = totalSamples,
                SuccessCount = successCount,
                FailureCount = totalSamples - successCount,
                FallbackCount = fallbackCount,
                Pattern = pattern
            };
        }

        private List<bool> ComputeExpected(List<string> dataset, string type)
        {
            var expected = new List<bool>(dataset.Count);
            var lowered = type.ToLowerInvariant();

            if (lowered == "redos")
            {
                foreach (var input in dataset)
                {
                    if (string.IsNullOrEmpty(input)) { expected.Add(false); continue; }
                    bool ok = true;
                    for (int i = 0; i < input.Length; i++) { if (input[i] != 'a') { ok = false; break; } }
                    expected.Add(ok);
                }
                return expected;
            }

            // Treat email-hybrid like email for ground truth
            if (lowered == "email-hybrid") lowered = "email";

            SimpleNfa? nfa = lowered switch
            {
                "email" => AutomataFactory.BuildEmailNfa(),
                "phone" => AutomataFactory.BuildPhoneNfa(),
                _ => null
            };

            if (nfa != null)
            {
                foreach (var input in dataset)
                {
                    expected.Add(nfa.Simulate(input));
                }
                return expected;
            }

            // Fallback: no expected ground truth available
            return new List<bool>();
        }

        /// <summary>
        /// Exports benchmark results to CSV format.
        /// </summary>
        public void ExportToCsv(BenchmarkSummary summary, string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var csv = new StringBuilder();
                csv.AppendLine("Method,Avg(ms),StdDev(ms),Min(ms),Max(ms),Fallback(%),Accuracy(%),Samples,Success,Failures");

                foreach (var result in summary.Results)
                {
                    csv.AppendLine($"{result.Method},{result.AvgMs:F3},{result.StdDevMs:F3}," +
                                 $"{result.MinMs:F3},{result.MaxMs:F3},{result.FallbackPercentage:F2}," +
                                 $"{result.AccuracyPercentage:F2},{result.TotalSamples}," +
                                 $"{result.SuccessCount},{result.FailureCount}");
                }

                File.WriteAllText(filePath, csv.ToString());
                _logger?.LogInformation("📊 Benchmark results exported to: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to export benchmark results to CSV");
            }
        }

        private string GetPatternForType(string type)
        {
            return type.ToLowerInvariant() switch
            {
                "email" => @"^[A-Za-z0-9]+([._%+\-][A-Za-z0-9]+)*@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$",
                "phone" => @"^(\+?\d{1,3}[\s\-]?)?(\(?\d{2,4}\)?[\s\-]?)?[\d\s\-]{6,15}$",
                // For redos, deliberately use a classic catastrophic-backtracking pattern
                // This is for benchmarking only and NOT used in production validation.
                // Pattern: ^(a+)+$ with inputs like aaaaa...X to force exponential backtracking
                "redos" => @"^(a+)+$",
                _ => @"^[A-Za-z0-9]+([._%+\-][A-Za-z0-9]+)*@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$"
            };
        }

        private object CreateTestModel(string type)
        {
            if (type.ToLowerInvariant() == "email")
            {
                return new TestEmailModel();
            }
            else if (type.ToLowerInvariant() == "redos")
            {
                return new TestRedosModel();
            }
            else if (type.ToLowerInvariant() == "phone")
            {
                return new TestPhoneModel();
            }
            return new TestEmailModel();
        }

        // Helper classes for testing
        private class TestEmailModel
        {
            [EmailCheck(@"^[A-Za-z0-9]+([._%+\-][A-Za-z0-9]+)*@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$")]
            public string TestValue { get; set; } = string.Empty;
        }

        private class TestRedosModel
        {
            [RegexCheck(@"^(a+)+$")]
            public string TestValue { get; set; } = string.Empty;
        }

        private class TestPhoneModel
        {
            [PhoneCheck(@"^(\+?\d{1,3}[\s\-]?)?(\(?\d{2,4}\)?[\s\-]?)?[\d\s\-]{6,15}$")]
            public string TestValue { get; set; } = string.Empty;
        }
    }
}
