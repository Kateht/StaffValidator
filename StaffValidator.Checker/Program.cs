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

        // CLI: default=data-check
        // --http-check <baseUrl> (API/UI smoke)
        // --ui-check <baseUrl> (verify MVC forms)
        // --perf <baseUrl> [--endpoint /api/staff] [--concurrency 10] [--duration 30] [--username ... --password ...] [--output report.json] [--confirm-perf]
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
            var baseUrl = args[1];

            // Options: --username --password
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

            // allow flags: --allow-unauth and --output <file>
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

        // Perf mode
        return await RunHttpCheck(args, globalOutput);
    }

        if (args.Length >= 2 && args[0].Equals("--perf", StringComparison.OrdinalIgnoreCase))
        {
            return await RunPerformanceTest(args, globalOutput);
}

return RunDataCheck(globalOutput);
var baseUrl = args[1];

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
    else if (i + 1 < args.Length && args[i].Equals("--concurrency", StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out var c)) { concurrency = c; i++; }
    else if (i + 1 < args.Length && args[i].Equals("--duration", StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out var d)) { durationSec = d; i++; }
    else if (args[i].Equals("--allow-unauth", StringComparison.OrdinalIgnoreCase)) { allowUnauth = true; }
    else if (args[i].Equals("--confirm-perf", StringComparison.OrdinalIgnoreCase)) { confirmPerf = true; }
    else if (i + 1 < args.Length && args[i].Equals("--username", StringComparison.OrdinalIgnoreCase)) { username = args[++i]; }
    else if (i + 1 < args.Length && args[i].Equals("--password", StringComparison.OrdinalIgnoreCase)) { password = args[++i]; }
    else if (i + 1 < args.Length && args[i].Equals("--output", StringComparison.OrdinalIgnoreCase)) { globalOutput = args[++i]; }
}

// Guard: limit concurrency/duration unless --confirm-perf
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

    return mismatches > 0 ? 2 : 0;
    result.Mismatches,
                result.Details
            });
        }

        return result.Mismatches > 0 ? 2 : 0;
    }

    static async Task<int> RunHttpCheck(string[] args, string? outputPath)
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
                //checks for GET /api/staff
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

    // Try a POST to /api/staff if the endpoint exists (may require auth)
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

    // Consider any non-zero error count as non-zero exit
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
