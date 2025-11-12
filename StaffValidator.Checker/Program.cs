using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StaffValidator.Core.Repositories;
using StaffValidator.Core.Services;

class Checker
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== StaffValidator Checker ===");
        // parse global flags
        string? globalOutput = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                globalOutput = args[i + 1];
                i++;
            }
        }

        // simple CLI: default is data-check; use --http-check <baseUrl> to perform interface smoke tests
        // perf/load test: --perf <baseUrl> [--endpoint /api/staff] [--concurrency 10] [--duration 30] [--username ... --password ...] [--output report.json] [--confirm-perf]
        // ui-check: --ui-check <baseUrl> to verify MVC interface layer (HTML rendering, forms)
        if (args.Length >= 2 && args[0].Equals("--ui-check", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = args[1];
            string? username = null;
            string? password = null;
            string? outputPath = null;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].Equals("--username", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { username = args[++i]; }
                else if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { password = args[++i]; }
                else if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) { outputPath = args[++i]; }
            }
            return await RunUiChecksAsync(baseUrl, username, password, outputPath);
        }
        
        if (args.Length >= 2 && args[0].Equals("--http-check", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = args[1];

            // optional creds: --username <user> --password <pass>
            string? username = null;
            string? password = null;
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].Equals("--username", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    username = args[i + 1]; i++;
                }
                else if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    password = args[i + 1]; i++;
                }
            }

            // also allow flags: --allow-unauth and --output <file>
            bool allowUnauth = false;
            string? outputPath = null;
            for (int i = 2; i < args.Length; i++)
            {
                    if (args[i].Equals("--allow-unauth", StringComparison.OrdinalIgnoreCase))
                    {
                        allowUnauth = true;
                    }
                    else if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        outputPath = args[i + 1]; i++;
                    }
            }

            return await RunHttpChecksAsync(baseUrl, username, password, allowUnauth, outputPath);
        }

        // perf mode
        if (args.Length >= 2 && args[0].Equals("--perf", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = args[1];

            // defaults
            string endpoint = "/api/staff";
            int concurrency = 10;
            int durationSec = 30;
            bool allowUnauth = false;
            bool confirmPerf = false;
            string? username = null;
            string? password = null;

            // parse options
            for (int i = 2; i < args.Length; i++)
            {
                if (i + 1 < args.Length && args[i].Equals("--endpoint", StringComparison.OrdinalIgnoreCase)) { endpoint = args[++i]; }
                else if (i + 1 < args.Length && args[i].Equals("--concurrency", StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i+1], out var c)) { concurrency = c; i++; }
                else if (i + 1 < args.Length && args[i].Equals("--duration", StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i+1], out var d)) { durationSec = d; i++; }
                else if (args[i].Equals("--allow-unauth", StringComparison.OrdinalIgnoreCase)) { allowUnauth = true; }
                else if (args[i].Equals("--confirm-perf", StringComparison.OrdinalIgnoreCase)) { confirmPerf = true; }
                else if (i + 1 < args.Length && args[i].Equals("--username", StringComparison.OrdinalIgnoreCase)) { username = args[++i]; }
                else if (i + 1 < args.Length && args[i].Equals("--password", StringComparison.OrdinalIgnoreCase)) { password = args[++i]; }
                else if (i + 1 < args.Length && args[i].Equals("--output", StringComparison.OrdinalIgnoreCase)) { globalOutput = args[++i]; }
            }

            // safety guard: cap concurrency/duration unless confirmed
            if (!confirmPerf)
            {
                if (concurrency > 50)
                {
                    concurrency = 50;
                }
                if (durationSec > 60)
                {
                    durationSec = 60;
                }
            }

            return await RunPerfAsync(baseUrl, endpoint, concurrency, durationSec, username, password, allowUnauth, globalOutput);
        }

        return RunDataChecks(globalOutput);
    }

    private static int RunDataChecks(string? outputPath = null)
    {
        var repoPath = "data/staff_records.json";
        var repo = new StaffRepository(repoPath);

        // configure hybrid validator with conservative defaults; adjust as needed
        var options = Options.Create(new HybridValidationOptions
        {
            RegexTimeoutMs = 200,
            MaxConcurrentRegexMatches = 4
        });

    var logger = new SimpleConsoleLogger<HybridValidatorService>();
    var validator = new HybridValidatorService(options, logger);

        var nfaEmail = AutomataFactory.BuildEmailNfa();
        var nfaPhone = AutomataFactory.BuildPhoneNfa();

        int mismatches = 0;
        var details = new List<string>();
        foreach (var s in repo.GetAll())
        {
            var (ok, errors) = validator.ValidateAll(s);
            var nfaEmailOk = nfaEmail.Simulate(s.Email);
            var nfaPhoneOk = nfaPhone.Simulate(s.PhoneNumber);

            if (!ok || !nfaEmailOk || !nfaPhoneOk)
            {
                mismatches++;
                var msg = $"[!] Staff {s.StaffID} - {s.StaffName} failed checks. ValidatorOk={ok}, NfaEmail={nfaEmailOk}, NfaPhone={nfaPhoneOk}";
                details.Add(msg);
                if (errors != null && errors.Count > 0)
                {
                    foreach (var e in errors)
                    {
                        details.Add("    - " + e);
                    }
                }
                Console.WriteLine(msg);
            }
        }

        Console.WriteLine($"Completed verification. Total mismatches: {mismatches}");

        if (!string.IsNullOrEmpty(outputPath))
        {
            var report = new
            {
                mode = "data-check",
                mismatches,
                details
            };
            try
            {
                System.IO.File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Wrote report to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed writing report: {ex.Message}");
            }
        }

        // Return non-zero exit code if mismatches found (useful for CI)
        return mismatches > 0 ? 2 : 0;
    }

    private static async Task<int> RunUiChecksAsync(string baseUrl, string? username = null, string? password = null, string? outputPath = null)
    {
        Console.WriteLine($"Running UI layer verification against {baseUrl}");
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var failures = new List<string>();

        // Authenticate if credentials provided
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            try
            {
                Console.WriteLine($"Authenticating as {username}...");
                var loginPage = await http.GetAsync("/Auth/Login");
                var loginContent = await loginPage.Content.ReadAsStringAsync();
                
                // Parse CSRF token if exists
                var csrfToken = ExtractCsrfToken(loginContent);
                
                // Attempt form-based login
                var formData = new Dictionary<string, string>
                {
                    ["Username"] = username,
                    ["Password"] = password
                };
                if (!string.IsNullOrEmpty(csrfToken))
                {
                    formData["__RequestVerificationToken"] = csrfToken;
                }
                
                var loginResp = await http.PostAsync("/Auth/Login", new FormUrlEncodedContent(formData));
                if (loginResp.IsSuccessStatusCode || loginResp.StatusCode == System.Net.HttpStatusCode.Redirect)
                {
                    Console.WriteLine("✅ UI authentication succeeded");
                }
                else
                {
                    failures.Add($"UI login failed with status {(int)loginResp.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"UI authentication error: {ex.Message}");
            }
        }

        // Check MVC Views (Interface Layer verification)
        var uiEndpoints = new[]
        {
            ("/", "Staff Management", "Home/Index page"),
            ("/Staff", "Staff", "Staff list page"),
            ("/Staff/Create", "Add", "Create staff form"),
            ("/Auth/Login", "Login", "Login page")
        };

        foreach (var (path, expectedText, description) in uiEndpoints)
        {
            try
            {
                var resp = await http.GetAsync(path);
                var content = await resp.Content.ReadAsStringAsync();
                
                Console.WriteLine($"GET {path} -> {(int)resp.StatusCode}");
                
                if (!resp.IsSuccessStatusCode)
                {
                    failures.Add($"{description}: returned {(int)resp.StatusCode}");
                }
                else if (!content.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{description}: missing expected text '{expectedText}'");
                }
                else
                {
                    Console.WriteLine($"  ✅ {description} rendered correctly");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{description}: {ex.GetType().Name} - {ex.Message}");
            }
        }

        // Verify form elements exist in Create page
        try
        {
            var createResp = await http.GetAsync("/Staff/Create");
            if (createResp.IsSuccessStatusCode)
            {
                var content = await createResp.Content.ReadAsStringAsync();
                var requiredFields = new[] { "StaffName", "Email", "PhoneNumber" };
                
                foreach (var field in requiredFields)
                {
                    if (!content.Contains($"name=\"{field}\"", StringComparison.OrdinalIgnoreCase) &&
                        !content.Contains($"asp-for=\"{field}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add($"Create form missing field: {field}");
                    }
                }
                
                if (failures.Count == 0 || !failures.Any(f => f.Contains("Create form missing")))
                {
                    Console.WriteLine("  ✅ Create form contains all required fields");
                }
            }
        }
        catch (Exception ex)
        {
            failures.Add($"Create form verification error: {ex.Message}");
        }

        Console.WriteLine("\n=== UI Layer Verification Summary ===");
        if (failures.Count > 0)
        {
            Console.WriteLine("❌ Failures detected:");
            foreach (var f in failures)
            {
                Console.WriteLine($"  - {f}");
            }
        }
        else
        {
            Console.WriteLine("✅ All UI layer checks passed");
        }

        // Write report
        if (!string.IsNullOrEmpty(outputPath))
        {
            var report = new
            {
                mode = "ui-check",
                baseUrl,
                timestamp = DateTime.UtcNow,
                failures,
                success = failures.Count == 0
            };
            try
            {
                System.IO.File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"\n📄 Report written to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed writing report: {ex.Message}");
            }
        }

        return failures.Count > 0 ? 5 : 0;
    }

    private static string? ExtractCsrfToken(string html)
    {
        // Simple CSRF token extraction (look for __RequestVerificationToken input)
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            html, 
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        return tokenMatch.Success ? tokenMatch.Groups[1].Value : null;
    }

    private static async Task<int> RunHttpChecksAsync(string baseUrl, string? username = null, string? password = null, bool allowUnauth = false, string? outputPath = null)
    {
        Console.WriteLine($"Running HTTP smoke checks against {baseUrl}");
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var failures = new List<string>();
        string? token = null;
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            try
            {
                Console.WriteLine($"Attempting authentication as {username}...");
                var loginResp = await http.PostAsJsonAsync("/api/auth/login", new { Username = username, Password = password });
                var body = await loginResp.Content.ReadAsStringAsync();
                if (loginResp.IsSuccessStatusCode)
                {
                    // try to parse token from common shapes
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("token", out var t1) && t1.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            token = t1.GetString();
                        }
                        else if (doc.RootElement.TryGetProperty("access_token", out var t2) && t2.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            token = t2.GetString();
                        }
                        else if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (data.TryGetProperty("token", out var d1) && d1.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                token = d1.GetString();
                            }
                            else if (data.TryGetProperty("access_token", out var d2) && d2.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                token = d2.GetString();
                            }
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("Authentication succeeded, using bearer token for subsequent requests.");
                    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                else
                {
                    Console.WriteLine($"Authentication did not return a token (status {(int)loginResp.StatusCode}). Continuing without auth.");
                    if (!allowUnauth)
                    {
                        Console.WriteLine("Authentication required but not obtained; failing HTTP checks.");
                        return 3;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication attempt failed: {ex.GetType().Name} {ex.Message}");
            }
        }

        // endpoints to check
        var endpoints = new[] { 
            (Method: "GET", Path: "/"),
            (Method: "GET", Path: "/swagger"),
            (Method: "GET", Path: "/api/staff"),
        };

        foreach (var ep in endpoints)
        {
            try
            {
                HttpResponseMessage resp = ep.Method == "GET"
                    ? await http.GetAsync(ep.Path)
                    : await http.SendAsync(new HttpRequestMessage(new HttpMethod(ep.Method), ep.Path));

                Console.WriteLine($"{ep.Method} {ep.Path} -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
                if (!resp.IsSuccessStatusCode)
                {
                    failures.Add($"{ep.Method} {ep.Path} returned {(int)resp.StatusCode}");
                }
                else
                {
                    // schema checks for GET /api/staff
                    if (ep.Path.Equals("/api/staff", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var content = await resp.Content.ReadAsStringAsync();
                            using var doc = System.Text.Json.JsonDocument.Parse(content);
                            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                            {
                                failures.Add($"{ep.Path} did not return a JSON array");
                            }
                            else
                            {
                                foreach (var item in doc.RootElement.EnumerateArray())
                                {
                                    if (!HasAnyProperty(item, new[] { "StaffID", "staffID", "staffId" }) || !HasAnyProperty(item, new[] { "StaffName", "staffName" }) || !HasAnyProperty(item, new[] { "Email", "email" }) || !HasAnyProperty(item, new[] { "PhoneNumber", "phoneNumber", "phone" }))
                                    {
                                        failures.Add($"{ep.Path} returned items missing required fields");
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{ep.Path} JSON parse error: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{ep.Method} {ep.Path} threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Try a minimal POST to /api/staff if the endpoint exists (may require auth)
        try
        {
            var sample = new { StaffID = "test-001", StaffName = "Test User", Email = "test@example.com", PhoneNumber = "+1000000000" };
            var postResp = await http.PostAsJsonAsync("/api/staff", sample);
            Console.WriteLine($"POST /api/staff -> {(int)postResp.StatusCode} {postResp.ReasonPhrase}");
            // require auth by default unless allowUnauth is true
            if (!postResp.IsSuccessStatusCode)
            {
                if (postResp.StatusCode == System.Net.HttpStatusCode.Unauthorized && allowUnauth)
                {
                    Console.WriteLine("POST /api/staff returned 401 Unauthorized but --allow-unauth is set; continuing.");
                }
                else
                {
                    failures.Add($"POST /api/staff returned {(int)postResp.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            failures.Add($"POST /api/staff threw {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine("\nHTTP smoke check completed.");
        if (failures.Count > 0)
        {
            Console.WriteLine("Failures:");
            foreach (var f in failures)
            {
                Console.WriteLine(" - " + f);
            }
        }

        // write JSON report if requested
        if (!string.IsNullOrEmpty(outputPath))
        {
            var report = new
            {
                mode = "http-check",
                baseUrl,
                authUsed = !string.IsNullOrEmpty(token),
                failures
            };
            try
            {
                System.IO.File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Wrote report to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed writing report: {ex.Message}");
            }
        }

        return failures.Count > 0 ? 3 : 0;
    }

    private static async Task<int> RunPerfAsync(
        string baseUrl,
        string endpoint,
        int concurrency,
        int durationSec,
        string? username,
        string? password,
        bool allowUnauth,
        string? outputPath)
    {
        Console.WriteLine($"Running performance test against {baseUrl}{endpoint} with concurrency={concurrency}, duration={durationSec}s");

        // Configure HttpClient for high throughput
        var handler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = Math.Max(10, concurrency * 2)
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(15) };

        // Optional auth
        string? token = null;
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            try
            {
                var loginResp = await http.PostAsJsonAsync("/api/auth/login", new { Username = username, Password = password });
                if (loginResp.IsSuccessStatusCode)
                {
                    var body = await loginResp.Content.ReadAsStringAsync();
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("token", out var t1) && t1.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            token = t1.GetString();
                        }
                        else if (doc.RootElement.TryGetProperty("access_token", out var t2) && t2.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            token = t2.GetString();
                        }
                    }
                    catch { }
                }
                if (!string.IsNullOrEmpty(token))
                {
                    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                else if (!allowUnauth)
                {
                    Console.WriteLine("Auth token not obtained and --allow-unauth not set; aborting perf test.");
                    return 3;
                }
            }
                catch (Exception ex)
                {
                    Console.WriteLine($"Auth attempt failed: {ex.Message}");
                    if (!allowUnauth)
                    {
                        return 3;
                    }
                }
        }

        var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(durationSec));
        var swTotal = Stopwatch.StartNew();
        var latencies = new List<long>(capacity: Math.Min(200_000, concurrency * durationSec * 2));
        var statusCounts = new Dictionary<int, int>();
        int success = 0, errors = 0;

        // simple worker that loops until cancellation
        async Task Worker()
        {
            var rng = new Random();
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    using var resp = await http.GetAsync(endpoint, cts.Token);
                    sw.Stop();
                    lock (latencies) { latencies.Add(sw.ElapsedMilliseconds); }
                    var code = (int)resp.StatusCode;
                    lock (statusCounts) { statusCounts[code] = statusCounts.TryGetValue(code, out var n) ? n + 1 : 1; }
                    if (resp.IsSuccessStatusCode)
                    {
                        System.Threading.Interlocked.Increment(ref success);
                    }
                    else
                    {
                        System.Threading.Interlocked.Increment(ref errors);
                    }
                }
                catch (OperationCanceledException)
                {
                    // normal on cancellation
                    break;
                }
                catch (Exception)
                {
                    System.Threading.Interlocked.Increment(ref errors);
                }

                // tiny jitter to avoid lockstep
                await Task.Delay(rng.Next(1, 5));
            }
        }

        var tasks = Enumerable.Range(0, Math.Max(1, concurrency)).Select(_ => Worker()).ToArray();
        await Task.WhenAll(tasks);
        swTotal.Stop();

        // compute metrics
        double seconds = Math.Max(0.001, swTotal.Elapsed.TotalSeconds);
        int total = success + errors;
        double rps = total / seconds;
        long p50 = 0, p95 = 0, p99 = 0;
        double avg = 0;
        if (latencies.Count > 0)
        {
            latencies.Sort();
            avg = latencies.Average();
            p50 = Percentile(latencies, 50);
            p95 = Percentile(latencies, 95);
            p99 = Percentile(latencies, 99);
        }

        Console.WriteLine("\n=== Performance summary ===");
        Console.WriteLine($"Total requests: {total} in {seconds:F1}s (RPS={rps:F1})");
        Console.WriteLine($"Success: {success}, Errors: {errors}");
        Console.WriteLine($"Latency ms -> avg: {avg:F1}, p50: {p50}, p95: {p95}, p99: {p99}");
        Console.WriteLine("Status codes:");
        foreach (var kv in statusCounts.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {kv.Key}: {kv.Value}");
        }

        if (!string.IsNullOrEmpty(outputPath))
        {
            var report = new
            {
                mode = "perf",
                baseUrl,
                endpoint,
                concurrency,
                durationSec,
                totals = new { total, success, errors, rps },
                latency = new { avgMs = avg, p50Ms = p50, p95Ms = p95, p99Ms = p99 },
                status = statusCounts
            };
            try
            {
                System.IO.File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Wrote perf report to {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed writing perf report: {ex.Message}");
            }
        }

        // Consider any non-zero error count as non-zero exit (so CI can catch regressions)
        return errors > 0 ? 4 : 0;
    }

    private static long Percentile(List<long> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }
        if (percentile <= 0)
        {
            return sortedValues[0];
        }
        if (percentile >= 100)
        {
            return sortedValues[^1];
        }
        var rank = (percentile / 100.0) * (sortedValues.Count - 1);
        int low = (int)Math.Floor(rank);
        int high = (int)Math.Ceiling(rank);
        if (low == high)
        {
            return sortedValues[low];
        }
        double frac = rank - low;
        return (long)Math.Round(sortedValues[low] + frac * (sortedValues[high] - sortedValues[low]));
    }

    private static bool HasAnyProperty(System.Text.Json.JsonElement el, string[] names)
    {
        foreach (var n in names)
        {
            if (el.TryGetProperty(n, out var _))
            {
                return true;
            }
        }
        return false;
    }
}

// Minimal console logger implementation used by the checker to avoid extra dependencies
internal class SimpleConsoleLogger<T> : ILogger<T>
{
    IDisposable ILogger.BeginScope<TState>(TState state) => new NoopDisposable();

    bool ILogger.IsEnabled(LogLevel logLevel) => true;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            var msg = formatter != null ? formatter(state, exception) : state?.ToString();
            Console.WriteLine($"[{logLevel}] {msg}");
            if (exception != null)
            {
                Console.WriteLine(exception.ToString());
            }
        }
        catch { }
    }
}

internal class NoopDisposable : IDisposable
{
    public void Dispose() { }
}
