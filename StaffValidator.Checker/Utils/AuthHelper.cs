using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace StaffValidator.Checker.Utils
{
    public static class AuthHelper
    {
        public static async Task<string?> AuthenticateAsync(HttpClient http, string? username, string? password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            try
            {
                Console.WriteLine($"Attempting authentication as {username}...");
                var loginResp = await http.PostAsJsonAsync("/api/auth/login", new { Username = username, Password = password });
                var body = await loginResp.Content.ReadAsStringAsync();
                
                if (!loginResp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Authentication failed with status {(int)loginResp.StatusCode}");
                    return null;
                }

                string? token = null;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    
                    // Try various token field names
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

                if (!string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("✅ Authentication succeeded, using bearer token");
                    return token;
                }
                
                Console.WriteLine("⚠️ Authentication did not return a token");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Authentication failed: {ex.GetType().Name} {ex.Message}");
                return null;
            }
        }
    }
}
