using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using StaffValidator.Checker.Utils;

namespace StaffValidator.Checker.Verifiers
{
    public class UiVerifier
    {
        private readonly string _baseUrl;
        private readonly string? _username;
        private readonly string? _password;

        public UiVerifier(string baseUrl, string? username = null, string? password = null)
        {
            _baseUrl = baseUrl;
            _username = username;
            _password = password;
        }

        public async Task<UiVerificationResult> VerifyAsync()
        {
            Console.WriteLine($"Running UI layer verification against {_baseUrl}");
            using var http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            var result = new UiVerificationResult();

            // Authenticate if credentials provided
            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                await AuthenticateUi(http, result);
            }

            // Check MVC Views
            await CheckViews(http, result);

            // Verify form elements
            await CheckCreateForm(http, result);

            return result;
        }

        private async Task AuthenticateUi(HttpClient http, UiVerificationResult result)
        {
            try
            {
                Console.WriteLine($"Authenticating as {_username}...");
                var loginPage = await http.GetAsync("/Auth/Login");
                var loginContent = await loginPage.Content.ReadAsStringAsync();
                
                var csrfToken = HtmlHelper.ExtractCsrfToken(loginContent);
                
                var formData = new Dictionary<string, string>
                {
                    ["Username"] = _username!,
                    ["Password"] = _password!
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
                    result.Failures.Add($"UI login failed with status {(int)loginResp.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                result.Failures.Add($"UI authentication error: {ex.Message}");
            }
        }

        private async Task CheckViews(HttpClient http, UiVerificationResult result)
        {
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
                        result.Failures.Add($"{description}: returned {(int)resp.StatusCode}");
                    }
                    else if (!content.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Failures.Add($"{description}: missing expected text '{expectedText}'");
                    }
                    else
                    {
                        Console.WriteLine($"  ✅ {description} rendered correctly");
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add($"{description}: {ex.GetType().Name} - {ex.Message}");
                }
            }
        }

        private async Task CheckCreateForm(HttpClient http, UiVerificationResult result)
        {
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
                            result.Failures.Add($"Create form missing field: {field}");
                        }
                    }
                    
                    if (!result.Failures.Any(f => f.Contains("Create form missing")))
                    {
                        Console.WriteLine("  ✅ Create form contains all required fields");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Failures.Add($"Create form verification error: {ex.Message}");
            }
        }
    }

    public class UiVerificationResult
    {
        public List<string> Failures { get; set; } = new();
    }
}
