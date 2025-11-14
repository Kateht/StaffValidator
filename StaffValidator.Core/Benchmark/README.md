# Hybrid Validation Benchmarking Module

## Overview

This module provides comprehensive benchmarking capabilities for the StaffValidator's hybrid validation system. It compares multiple validation strategies to measure performance, accuracy, and resilience against Regular Expression Denial of Service (ReDoS) attacks.

## Architecture

### Components

1. **BenchmarkDataset** (`Core/Benchmark/BenchmarkDataset.cs`)
   - Generates synthetic test datasets
   - Supports email, phone, ReDoS patterns, and mixed datasets
   - Produces both valid and invalid samples for accuracy testing

2. **BenchmarkRunner** (`Core/Benchmark/BenchmarkRunner.cs`)
   - Executes benchmarks across multiple validation strategies
   - Collects performance metrics (avg, std dev, min, max)
   - Tracks fallback usage rates
   - Exports results to CSV format

3. **BenchmarkController** (`WebApp/Controllers/BenchmarkController.cs`)
   - REST API endpoints for running benchmarks
   - Supports quick tests, full benchmarks, and stress tests
   - Returns JSON results with detailed metrics

## Validation Methods Compared

| Method | Description | Use Case |
|--------|-------------|----------|
| **Regex Uncached** | Creates new Regex instance per validation | Baseline - worst performance |
| **Regex Cached** | Pre-compiled Regex with caching | Standard production use |
| **Hybrid (Regex→DFA)** | Tries Regex first, falls back to DFA on timeout | ReDoS-resistant production |
| **DFA Only** | Uses deterministic finite automaton | Maximum security, slightly slower |

## API Endpoints

### 1. Run Benchmark
```http
GET /api/benchmark/run?type={type}&samples={count}&export={bool}
```

**Parameters:**
- `type`: Dataset type (`email`, `phone`, `redos`, `mixed`)
- `samples`: Number of test samples (1-10000, default: 2000)
- `export`: Export results to CSV (default: false)

**Example:**
```bash
curl "http://localhost:5000/api/benchmark/run?type=email&samples=2000&export=true"
```

**Response:**
```json
{
  "datasetType": "email",
  "sampleCount": 2000,
  "totalDurationMs": 1234.56,
  "timestamp": "2025-11-12T22:23:05Z",
  "results": [
    {
      "method": "Regex Cached",
      "avgMs": 0.32,
      "stdDevMs": 0.05,
      "minMs": 0.15,
      "maxMs": 1.2,
      "fallbackPercentage": 0,
      "accuracyPercentage": 100,
      "totalSamples": 2000
    },
    {
      "method": "Hybrid (Regex→DFA)",
      "avgMs": 0.48,
      "stdDevMs": 0.09,
      "fallbackPercentage": 3.5,
      "accuracyPercentage": 100
    }
  ]
}
```

### 2. Quick Benchmark
```http
GET /api/benchmark/quick?type={type}
```

Runs a quick test with 100 samples for rapid testing.

### 3. Stress Test
```http
GET /api/benchmark/stress?samples={count}
```

Runs ReDoS-focused stress test with adversarial patterns.

### 4. Info
```http
GET /api/benchmark/info
```

Returns configuration and available options.

### 5. Preview Dataset
```http
GET /api/benchmark/preview?type={type}&count={count}
```

Generates and returns sample dataset (max 50 samples).

## Configuration

In `appsettings.json`:

```json
{
  "HybridValidation": {
    "RegexTimeoutMs": 100,
    "EnableDfaFallback": true,
    "MaxConcurrentRegexMatches": 4
  }
}
```

## Usage Examples

### Running Benchmark via API

```bash
# Email validation benchmark
curl "http://localhost:5000/api/benchmark/run?type=email&samples=2000"

# Phone validation with CSV export
curl "http://localhost:5000/api/benchmark/run?type=phone&samples=5000&export=true"

# ReDoS stress test
curl "http://localhost:5000/api/benchmark/stress?samples=100"

# Quick test
curl "http://localhost:5000/api/benchmark/quick?type=email"
```

### Programmatic Usage

```csharp
// Inject services
var hybridService = services.GetRequiredService<HybridValidatorService>();
var logger = services.GetRequiredService<ILogger<BenchmarkRunner>>();

// Generate dataset
var dataset = BenchmarkDataset.Generate("email", 2000);

// Run benchmark
var runner = new BenchmarkRunner(logger);
var results = await runner.RunAsync(hybridService, dataset, "email");

// Export to CSV
runner.ExportToCsv(results, "data/benchmark_results.csv");
```

## Interpreting Results

### Performance Metrics

- **AvgMs**: Average execution time per validation
  - <1ms: Excellent
  - 1-5ms: Good
  - >5ms: Needs optimization

- **StdDevMs**: Standard deviation
  - Low (<0.1ms): Consistent performance
  - High (>1ms): Variable performance, investigate outliers

- **FallbackPercentage**: How often DFA fallback was used
  - 0%: All validations completed via Regex
  - <5%: Normal for complex patterns
  - >10%: Consider reviewing patterns or increasing timeout

### Expected Results

Based on 2000 samples:

| Method | Avg (ms) | StdDev | Fallback | Notes |
|--------|----------|--------|----------|-------|
| Regex Uncached | 0.8-1.2 | 0.15-0.25 | 0% | Baseline |
| Regex Cached | 0.3-0.5 | 0.05-0.10 | 0% | **Recommended** |
| Hybrid | 0.4-0.6 | 0.08-0.12 | 0-5% | **Best for production** |
| DFA Only | 0.35-0.55 | 0.06-0.10 | N/A | Maximum security |

## CSV Export Format

When `export=true`, results are saved to `/data/benchmark_{type}_{samples}_{timestamp}.csv`:

```csv
Method,Avg(ms),StdDev(ms),Min(ms),Max(ms),Fallback(%),Accuracy(%),Samples,Success,Failures
Regex Uncached,0.952,0.187,0.421,3.215,0.00,100.00,2000,1000,1000
Regex Cached,0.328,0.052,0.152,1.284,0.00,100.00,2000,1000,1000
Hybrid (Regex→DFA),0.485,0.091,0.187,2.156,3.50,100.00,2000,1000,1000
DFA Only,0.421,0.067,0.195,1.532,0.00,100.00,2000,1000,1000
```

## ReDoS Testing

The module includes special datasets for testing ReDoS resistance:

```csharp
var dataset = BenchmarkDataset.Generate("redos", 100);
```

These patterns use exponential backtracking (e.g., `(a+)+b`) to stress-test regex engines. The Hybrid validator should:
1. Detect slow regex execution
2. Timeout after configured threshold (default: 100ms)
3. Fall back to DFA for O(n) deterministic validation

## Logging

All benchmark operations are logged with structured logging:

```
🚀 Starting benchmark: Type=email, Samples=2000
✅ Benchmark completed: Duration=1234.56ms, Methods=4
📊 Benchmark results exported to: data/benchmark_results.csv
⚠️ Starting stress test with ReDoS patterns | Samples=100
```

## Performance Tips

1. **Use Hybrid validation in production** - Best balance of speed and security
2. **Monitor fallback rates** - High rates may indicate pattern issues
3. **Increase timeout for complex patterns** - But not beyond 200ms
4. **Cache regex instances** - Provides 2-3x performance improvement
5. **Use DFA-only for untrusted input** - When security is paramount

## Extending the Module

### Adding New Dataset Types

```csharp
public static List<string> Generate(string type, int count)
{
    return type?.ToLowerInvariant() switch
    {
        "email" => GenerateEmailDataset(count),
        "yourtype" => GenerateYourDataset(count), // Add here
        _ => GenerateEmailDataset(count)
    };
}
```

### Custom Validation Methods

Add new benchmarking methods in `BenchmarkRunner.cs`:

```csharp
private async Task<BenchmarkResult> BenchmarkYourMethod(...)
{
    // Your implementation
}
```

## Integration with CI/CD

Run benchmarks as part of your CI pipeline:

```bash
# Quick validation test
dotnet test --filter "Category=Benchmark"

# Or via API
curl "http://localhost:5000/api/benchmark/quick?type=email" | jq .
```

## Troubleshooting

### High Fallback Rates
- Check regex timeout configuration
- Review pattern complexity
- Consider simplifying patterns

### Poor Performance
- Ensure regex caching is enabled
- Check for excessive concurrent validations
- Monitor system resources

### Inconsistent Results
- Run multiple iterations
- Check for system load during tests
- Use fixed seed for reproducibility

## References

- [HybridValidatorService Documentation](../Services/HybridValidatorService.cs)
- [AutomataEngine Documentation](../Services/AutomataEngine.cs)
- [ReDoS Prevention Guide](https://owasp.org/www-community/attacks/Regular_expression_Denial_of_Service_-_ReDoS)
