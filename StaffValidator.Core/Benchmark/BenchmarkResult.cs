using System.Collections.Generic;

namespace StaffValidator.Core.Benchmark
{
    /// <summary>
    /// Represents the result of a single validation method benchmark run.
    /// </summary>
    public record BenchmarkResult
    {
        /// <summary>
        /// Name of the validation method (e.g., "Regex Cached", "Hybrid", "DFA Only")
        /// </summary>
        public string Method { get; init; } = string.Empty;

        /// <summary>
        /// Average execution time in milliseconds
        /// </summary>
        public double AvgMs { get; init; }

        /// <summary>
        /// Standard deviation of execution times
        /// </summary>
        public double StdDevMs { get; init; }

        /// <summary>
        /// Minimum execution time observed
        /// </summary>
        public double MinMs { get; init; }

        /// <summary>
        /// Maximum execution time observed
        /// </summary>
        public double MaxMs { get; init; }

        /// <summary>
        /// Percentage of validations that used DFA fallback
        /// </summary>
        public double FallbackPercentage { get; init; }

        /// <summary>
        /// Percentage of correct validations (accuracy)
        /// </summary>
        public double AccuracyPercentage { get; init; }

        /// <summary>
        /// Total number of samples tested
        /// </summary>
        public int TotalSamples { get; init; }

        /// <summary>
        /// Number of successful validations
        /// </summary>
        public int SuccessCount { get; init; }

        /// <summary>
        /// Number of failed validations
        /// </summary>
        public int FailureCount { get; init; }

        /// <summary>
        /// Number of times DFA fallback was used
        /// </summary>
        public int FallbackCount { get; init; }

        /// <summary>
        /// Pattern used for validation
        /// </summary>
        public string Pattern { get; init; } = string.Empty;
    }

    /// <summary>
    /// Aggregates results from multiple benchmark methods for comparison.
    /// </summary>
    public record BenchmarkSummary
    {
        /// <summary>
        /// Type of dataset tested (email, phone, redos)
        /// </summary>
        public string DatasetType { get; init; } = string.Empty;

        /// <summary>
        /// Number of samples in the dataset
        /// </summary>
        public int SampleCount { get; init; }

        /// <summary>
        /// Results for each validation method
        /// </summary>
        public List<BenchmarkResult> Results { get; init; } = new();

        /// <summary>
        /// Total execution time for entire benchmark
        /// </summary>
        public double TotalDurationMs { get; init; }

        /// <summary>
        /// Timestamp when benchmark was run
        /// </summary>
        public System.DateTime Timestamp { get; init; } = System.DateTime.UtcNow;

        /// <summary>
        /// Additional metadata about the benchmark run
        /// </summary>
        public Dictionary<string, string> Metadata { get; init; } = new();
    }
}
