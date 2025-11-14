using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using StaffValidator.Checker.Utils;

namespace StaffValidator.Checker.Verifiers
{
    public class HttpVerifier
    {
        private readonly string _baseUrl;
        private readonly string? _username;
        private readonly string? _password;
        private readonly bool _allowUnauth;

        public HttpVerifier(string baseUrl, string? username = null, string? password = null, bool allowUnauth = false)
        {
            _baseUrl = baseUrl;
            _username = username;
            _password = password;
            _allowUnauth = allowUnauth;
        }

        public async Task<HttpVerificationResult> VerifyAsync()
        {
            Console.WriteLine($"Running HTTP smoke checks against {_baseUrl}");
            using var http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            var result = new HttpVerificationResult { BaseUrl = _baseUrl };

            // Authenticate if credentials provided
            var token = await AuthHelper.AuthenticateAsync(http, _username, _password);
            if (!string.IsNullOrEmpty(token))
            {
                result.AuthUsed = true;
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            else if (!_allowUnauth && !string.IsNullOrEmpty(_username))
            {
                Console.WriteLine("Authentication required but not obtained; failing HTTP checks.");
                result.Failures.Add("Authentication failed");
                return result;
            }

            // Check endpoints
            var endpoints = new[]
            {
                (Method: "GET", Path: "/"),
                (Method: "GET", Path: "/swagger"),
                (Method: "GET", Path: "/api/staff"),
            };

            foreach (var ep in endpoints)
            {
                try
                {
                    var resp = await http.GetAsync(ep.Path);
                    Console.WriteLine($"{ep.Method} {ep.Path} -> {(int)resp.StatusCode} {resp.ReasonPhrase}");
                    
                    if (!resp.IsSuccessStatusCode)
                    {
                        result.Failures.Add($"{ep.Method} {ep.Path} returned {(int)resp.StatusCode}");
                    }
                    else if (ep.Path.Equals("/api/staff", StringComparison.OrdinalIgnoreCase))
                    {
                        await ValidateStaffApiResponse(resp, result);
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{ep.Method} {ep.Path} threw {ex.GetType().Name}: {ex.Message}");
                }
            }

            // Try POST
            await TestPostStaff(http, result, _allowUnauth);

            return result;
        }

        private async Task ValidateStaffApiResponse(HttpResponseMessage resp, HttpVerificationResult result)
        {
            try
            {
                var content = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                {
                    result.Failures.Add("/api/staff did not return a JSON array");
                }
                else
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (!HasRequiredFields(item))
                        {
                            result.Failures.Add("/api/staff returned items missing required fields");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Failures.Add($"/api/staff JSON parse error: {ex.Message}");
            }
        }

        private async Task TestPostStaff(HttpClient http, HttpVerificationResult result, bool allowUnauth)
        {
            try
            {
                var sample = new { StaffID = "test-001", StaffName = "Test User", Email = "test@example.com", PhoneNumber = "+1000000000" };
                var postResp = await http.PostAsJsonAsync("/api/staff", sample);
                Console.WriteLine($"POST /api/staff -> {(int)postResp.StatusCode} {postResp.ReasonPhrase}");
                
                if (!postResp.IsSuccessStatusCode)
                {
                    if (postResp.StatusCode == System.Net.HttpStatusCode.Unauthorized && allowUnauth)
                    {
                        Console.WriteLine("POST /api/staff returned 401 but --allow-unauth is set; continuing.");
                    }
                    else
                    {
                        result.Failures.Add($"POST /api/staff returned {(int)postResp.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Failures.Add($"POST /api/staff threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        private bool HasRequiredFields(System.Text.Json.JsonElement el)
        {
            return HasAnyProperty(el, new[] { "StaffID", "staffID", "staffId" }) &&
                   HasAnyProperty(el, new[] { "StaffName", "staffName" }) &&
                   HasAnyProperty(el, new[] { "Email", "email" }) &&
                   HasAnyProperty(el, new[] { "PhoneNumber", "phoneNumber", "phone" });
        }

        private bool HasAnyProperty(System.Text.Json.JsonElement el, string[] names)
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

    public class HttpVerificationResult
    {
        public string BaseUrl { get; set; } = string.Empty;
        public bool AuthUsed { get; set; }
        public List<string> Failures { get; set; } = new();
    }
}
