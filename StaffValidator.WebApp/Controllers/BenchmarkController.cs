using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StaffValidator.Core.Benchmark;
using StaffValidator.Core.Services;

namespace StaffValidator.WebApp.Controllers
{
    /// <summary>
    /// API controller for running validation benchmarks and collecting performance metrics.
    /// Provides endpoints to measure and compare different validation strategies.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BenchmarkController : ControllerBase
    {
        private readonly HybridValidatorService _hybridService;
        private readonly ILogger<BenchmarkController> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public BenchmarkController(
            HybridValidatorService hybridService,
            ILogger<BenchmarkController> logger,
            ILoggerFactory loggerFactory)
        {
            _hybridService = hybridService;
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Runs a comprehensive benchmark comparing multiple validation strategies.
        /// </summary>
        /// <param name="type">Dataset type: "email", "phone", "redos", or "mixed"</param>
        /// <param name="samples">Number of test samples to generate (default: 2000)</param>
        /// <param name="export">Whether to export results to CSV (default: false)</param>
        /// <returns>Benchmark summary with performance metrics for all methods</returns>
        /// <response code="200">Returns benchmark results</response>
        /// <response code="400">If parameters are invalid</response>
        [HttpGet("run")]
        [ProducesResponseType(typeof(BenchmarkSummary), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> RunBenchmark(
            [FromQuery] string type = "email",
            [FromQuery] int samples = 2000,
            [FromQuery] bool export = false,
            // For email-hybrid: fraction of adversarial inputs, e.g. 0.05 = 5%
            [FromQuery] double hybridEvilRatio = 0.05)
        {
            try
            {
                // Validate parameters
                if (samples <= 0 || samples > 10000)
                {
                    return BadRequest(new { error = "Samples must be between 1 and 10000" });
                }

                var validTypes = new[] { "email", "email-hybrid", "phone", "redos", "mixed" };
                if (!Array.Exists(validTypes, t => t.Equals(type, StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(new { error = $"Invalid type. Must be one of: {string.Join(", ", validTypes)}" });
                }

                if (!double.IsFinite(hybridEvilRatio) || hybridEvilRatio < 0 || hybridEvilRatio > 1)
                {
                    return BadRequest(new { error = "hybridEvilRatio must be a number between 0.0 and 1.0" });
                }

                _logger.LogInformation(
                    "🚀 Starting benchmark request | Type={Type}, Samples={Samples}, Export={Export}",
                    type, samples, export);

                // Generate dataset
                var lowered = type.ToLowerInvariant();
                var dataset = lowered == "email-hybrid"
                    ? BenchmarkDataset.GenerateEmailHybridDataset(samples, hybridEvilRatio)
                    : BenchmarkDataset.Generate(type, samples);

                // Run benchmark
                var runner = new BenchmarkRunner(_loggerFactory.CreateLogger<BenchmarkRunner>());
                var results = await runner.RunAsync(_hybridService, dataset, type);

                // Export to CSV if requested
                if (export)
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    var fileName = $"benchmark_{type}_{samples}_{timestamp}.csv";
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "data", fileName);

                    runner.ExportToCsv(results, filePath);

                    results.Metadata["ExportPath"] = filePath;
                }

                _logger.LogInformation(
                    "✅ Benchmark completed | Type={Type}, Duration={Duration:F2}ms, Methods={Count}",
                    type, results.TotalDurationMs, results.Results.Count);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Benchmark failed | Type={Type}, Samples={Samples}", type, samples);
                return StatusCode(500, new { error = "Benchmark execution failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Runs a quick benchmark with a small dataset for testing purposes.
        /// </summary>
        /// <param name="type">Dataset type</param>
        /// <returns>Quick benchmark results</returns>
        [HttpGet("quick")]
        [ProducesResponseType(typeof(BenchmarkSummary), 200)]
        public async Task<IActionResult> QuickBenchmark([FromQuery] string type = "email")
        {
            return await RunBenchmark(type, 100, false);
        }

        /// <summary>
        /// Runs a stress test with adversarial ReDoS patterns.
        /// </summary>
        /// <param name="samples">Number of ReDoS test cases (default: 100)</param>
        /// <returns>Stress test results</returns>
        [HttpGet("stress")]
        [ProducesResponseType(typeof(BenchmarkSummary), 200)]
        public async Task<IActionResult> StressTest([FromQuery] int samples = 100)
        {
            try
            {
                _logger.LogWarning("⚠️ Starting stress test with ReDoS patterns | Samples={Samples}", samples);

                var dataset = BenchmarkDataset.Generate("redos", samples);
                var runner = new BenchmarkRunner(_loggerFactory.CreateLogger<BenchmarkRunner>());
                var results = await runner.RunAsync(_hybridService, dataset, "redos");

                _logger.LogInformation("✅ Stress test completed | Duration={Duration:F2}ms", results.TotalDurationMs);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Stress test failed");
                return StatusCode(500, new { error = "Stress test failed", details = ex.Message });
            }
        }

        /// <summary>
        /// Gets information about available benchmark options.
        /// </summary>
        /// <returns>Benchmark configuration and options</returns>
        [HttpGet("info")]
        public IActionResult GetInfo()
        {
                var info = new
            {
                    availableTypes = new[] { "email", "email-hybrid", "phone", "redos", "mixed" },
                defaultSamples = 2000,
                maxSamples = 10000,
                methods = new[]
                {
                    "Regex Uncached - Creates new Regex instance for each validation",
                    "Regex Cached - Uses pre-compiled Regex with caching",
                    "Hybrid (Regex→DFA) - Attempts Regex first, falls back to DFA on timeout",
                    "DFA Only - Uses deterministic finite automaton exclusively"
                },
                regexTimeoutMs = _hybridService.RegexTimeoutMs,
                endpoints = new
                {
                        run = "/api/benchmark/run?type={type}&samples={count}&export={bool}&hybridEvilRatio={0.05..1.0}",
                    quick = "/api/benchmark/quick?type={type}",
                    stress = "/api/benchmark/stress?samples={count}",
                    info = "/api/benchmark/info"
                }
            };

            return Ok(info);
        }

        /// <summary>
        /// Generates a dataset without running the benchmark (for preview).
        /// </summary>
        /// <param name="type">Dataset type</param>
        /// <param name="count">Number of samples (max 50 for preview)</param>
        /// <returns>Sample dataset</returns>
        [HttpGet("preview")]
        public IActionResult PreviewDataset([FromQuery] string type = "email", [FromQuery] int count = 10)
        {
            if (count > 50)
            {
                return BadRequest(new { error = "Preview limited to 50 samples" });
            }

            var dataset = BenchmarkDataset.Generate(type, count);
            
            return Ok(new
            {
                type,
                count = dataset.Count,
                samples = dataset
            });
        }
    }
}
