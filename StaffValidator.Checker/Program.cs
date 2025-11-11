using System;
using System.Collections.Generic;
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
