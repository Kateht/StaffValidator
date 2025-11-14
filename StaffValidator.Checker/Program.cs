using System;
using System.Linq;
using System.Threading.Tasks;
using StaffValidator.Checker.Verifiers;
using StaffValidator.Checker.Utils;
using StaffValidator.Core.Services;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== StaffValidator Checker ===\n");

        string? globalOutput = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                globalOutput = args[i + 1];
                i++;
            }
        }

        if (args.Length >= 2 && args[0].Equals("--ui-check", StringComparison.OrdinalIgnoreCase))
        {
            return await RunUiCheck(args, globalOutput);
        }

        if (args.Length >= 2 && args[0].Equals("--selenium-ui-check", StringComparison.OrdinalIgnoreCase))
        {
            return await RunSeleniumUiCheck(args, globalOutput);
        }

        if (args.Length >= 2 && args[0].Equals("--http-check", StringComparison.OrdinalIgnoreCase))
        {
            return await RunHttpCheck(args, globalOutput);
        }

        if (args.Length >= 2 && args[0].Equals("--perf", StringComparison.OrdinalIgnoreCase))
        {
            return await RunPerformanceTest(args, globalOutput);
        }

        return RunDataCheck(globalOutput);
    }

    static int RunDataCheck(string? outputPath)
    {
        var logger = new SimpleConsoleLogger<HybridValidatorService>();
        var verifier = new DataVerifier("data/staff_records.json", logger);
        var result = verifier.Verify();

        Console.WriteLine($"\n Completed verification. Total mismatches: {result.Mismatches}");

        if (!string.IsNullOrEmpty(outputPath))
        {
            ReportWriter.WriteReport(outputPath, new
            {
                mode = "data-check",
                result.Mismatches,
                result.Details
            });
        }

        return result.Mismatches > 0 ? 2 : 0;
    }

    static async Task<int> RunHttpCheck(string[] args, string? outputPath)
    {
        var baseUrl = args[1];
        var (username, password, allowUnauth) = ParseHttpCheckArgs(args);

        var verifier = new HttpVerifier(baseUrl, username, password, allowUnauth);
        var result = await verifier.VerifyAsync();

        Console.WriteLine("\n=== HTTP Smoke Check Summary ===");
        if (result.Failures.Count > 0)
        {
            Console.WriteLine(" Failures:");
            foreach (var f in result.Failures)
            {
                Console.WriteLine($"  - {f}");
            }
        }
        else
        {
            Console.WriteLine(" All checks passed");
        }

        if (!string.IsNullOrEmpty(outputPath))
        {
            ReportWriter.WriteReport(outputPath, new
            {
                mode = "http-check",
                result.BaseUrl,
                result.AuthUsed,
                result.Failures
            });
        }

        return result.Failures.Count > 0 ? 3 : 0;
    }

    static async Task<int> RunUiCheck(string[] args, string? outputPath)
    {
        var baseUrl = args[1];
        var (username, password) = ParseUiCheckArgs(args);

        var verifier = new UiVerifier(baseUrl, username, password);
        var result = await verifier.VerifyAsync();

        Console.WriteLine("\n=== UI Layer Verification Summary ===");
        if (result.Failures.Count > 0)
        {
            Console.WriteLine(" Failures detected:");
            foreach (var f in result.Failures)
            {
                Console.WriteLine($"  - {f}");
            }
        }
        else
        {
            Console.WriteLine(" All UI layer checks passed");
        }

        if (!string.IsNullOrEmpty(outputPath))
        {
            ReportWriter.WriteReport(outputPath, new
            {
                mode = "ui-check",
                baseUrl,
                timestamp = DateTime.UtcNow,
                result.Failures,
                success = result.Failures.Count == 0
            });
        }

        return result.Failures.Count > 0 ? 5 : 0;
    }

    static async Task<int> RunSeleniumUiCheck(string[] args, string? outputPath)
    {
        var baseUrl = args[1];
        var (browser, headless, timeoutSec, username, password, delayMs) = ParseSeleniumArgs(args);

        var verifier = new SeleniumUiVerifier(baseUrl, browser, headless, timeoutSec, username, password, delayMs);
        var result = await verifier.VerifyAsync();

        Console.WriteLine("\n=== Selenium UI Verification Summary ===");
        if (result.Failures.Count > 0)
        {
            Console.WriteLine(" Failures detected:");
            foreach (var f in result.Failures)
            {
                Console.WriteLine($"  - {f}");
            }
        }
        else
        {
            Console.WriteLine(" All Selenium checks passed");
        }

        if (!string.IsNullOrEmpty(outputPath))
        {
            ReportWriter.WriteReport(outputPath, new
            {
                mode = "selenium-ui-check",
                baseUrl,
                browser,
                headless,
                timeoutSec,
                username = string.IsNullOrEmpty(username) ? "(default)" : "(provided)",
                failures = result.Failures,
                cases = result.Cases,
                delayMs,
                success = result.Failures.Count == 0
            });
        }

        return result.Failures.Count > 0 ? 6 : 0;
    }

    static async Task<int> RunPerformanceTest(string[] args, string? outputPath)
    {
        var baseUrl = args[1];
        var (endpoint, concurrency, durationSec, username, password, allowUnauth) = ParsePerfArgs(args);

        if (!args.Any(a => a.Equals("--confirm-perf", StringComparison.OrdinalIgnoreCase)))
        {
            concurrency = Math.Min(concurrency, 50);
            durationSec = Math.Min(durationSec, 60);
        }

        var verifier = new PerformanceVerifier(baseUrl, endpoint, concurrency, durationSec, username, password, allowUnauth);
        var result = await verifier.RunAsync();

        Console.WriteLine("\n=== Performance Summary ===");
        Console.WriteLine($"Total requests: {result.Total} (RPS={result.Rps:F1})");
        Console.WriteLine($"Success: {result.Success}, Errors: {result.Errors}");
        Console.WriteLine($"Latency ms -> avg: {result.AvgMs:F1}, p50: {result.P50Ms}, p95: {result.P95Ms}, p99: {result.P99Ms}");
        Console.WriteLine("Status codes:");
        foreach (var kv in result.StatusCounts)
        {
            Console.WriteLine($"  {kv.Key}: {kv.Value}");
        }

        if (!string.IsNullOrEmpty(outputPath))
        {
            ReportWriter.WriteReport(outputPath, new
            {
                mode = "perf",
                baseUrl,
                endpoint,
                concurrency,
                durationSec,
                totals = new { result.Total, result.Success, result.Errors, result.Rps },
                latency = new { avgMs = result.AvgMs, p50Ms = result.P50Ms, p95Ms = result.P95Ms, p99Ms = result.P99Ms },
                status = result.StatusCounts
            });
        }

        return result.Errors > 0 ? 4 : 0;
    }

    static (string? username, string? password, bool allowUnauth) ParseHttpCheckArgs(string[] args)
    {
        string? username = null, password = null;
        bool allowUnauth = false;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].Equals("--username", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { username = args[++i]; }
            else if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { password = args[++i]; }
            else if (args[i].Equals("--allow-unauth", StringComparison.OrdinalIgnoreCase)) { allowUnauth = true; }
        }

        return (username, password, allowUnauth);
    }

    static (string? username, string? password) ParseUiCheckArgs(string[] args)
    {
        string? username = null, password = null;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].Equals("--username", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { username = args[++i]; }
            else if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { password = args[++i]; }
        }

        return (username, password);
    }

    static (string browser, bool headless, int timeoutSec, string? username, string? password, int delayMs) ParseSeleniumArgs(string[] args)
    {
        string browser = "edge";
        bool headless = true;
        int timeoutSec = 15;
        string? username = null, password = null;
        int delayMs = 500;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].Equals("--browser", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var b = args[++i].ToLowerInvariant();
                if (b == "chrome" || b == "edge")
                {
                    browser = b;
                }
            }
            else if (args[i].Equals("--headless", StringComparison.OrdinalIgnoreCase))
            {
                headless = true;
            }
            else if (args[i].Equals("--no-headless", StringComparison.OrdinalIgnoreCase))
            {
                headless = false;
            }
            else if (args[i].Equals("--timeout", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var t))
            {
                timeoutSec = Math.Max(5, t);
                i++;
            }
            else if (args[i].Equals("--username", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                username = args[++i];
            }
            else if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                password = args[++i];
            }
            else if (args[i].Equals("--delay-ms", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var d))
            {
                delayMs = Math.Max(0, d);
                i++;
            }
        }

        return (browser, headless, timeoutSec, username, password, delayMs);
    }

    static (string endpoint, int concurrency, int durationSec, string? username, string? password, bool allowUnauth) ParsePerfArgs(string[] args)
    {
        string endpoint = "/api/staff";
        int concurrency = 10, durationSec = 30;
        string? username = null, password = null;
        bool allowUnauth = false;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i].Equals("--endpoint", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { endpoint = args[++i]; }
            else if (args[i].Equals("--concurrency", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var c)) { concurrency = c; i++; }
            else if (args[i].Equals("--duration", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var d)) { durationSec = d; i++; }
            else if (args[i].Equals("--username", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { username = args[++i]; }
            else if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { password = args[++i]; }
            else if (args[i].Equals("--allow-unauth", StringComparison.OrdinalIgnoreCase)) { allowUnauth = true; }
        }

        return (endpoint, concurrency, durationSec, username, password, allowUnauth);
    }
}
