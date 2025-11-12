using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StaffValidator.Tests
{
    public class WebAppIntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public WebAppIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetApiStaff_ReturnsArray_WithRequiredFields()
        {
            var client = _factory.CreateClient();
            var resp = await client.GetAsync("/api/staff");
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            
            // API returns wrapped response with Success, Message, Data, Timestamp
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
            Assert.True(doc.RootElement.TryGetProperty("data", out var dataEl) || doc.RootElement.TryGetProperty("Data", out dataEl));
            
            // Data should be an array
            Assert.Equal(JsonValueKind.Array, dataEl.ValueKind);
            
            if (dataEl.GetArrayLength() > 0)
            {
                var el = dataEl[0];
                Assert.True(el.TryGetProperty("StaffID", out _ ) || el.TryGetProperty("staffID", out _));
                Assert.True(el.TryGetProperty("StaffName", out _) || el.TryGetProperty("staffName", out _));
                Assert.True(el.TryGetProperty("Email", out _) || el.TryGetProperty("email", out _));
                Assert.True(el.TryGetProperty("PhoneNumber", out _) || el.TryGetProperty("phoneNumber", out _));
            }
        }

        [Fact]
        public async Task ApiAuth_Login_And_PostStaff_Succeeds()
        {
            var client = _factory.CreateClient();

            var loginResp = await client.PostAsJsonAsync("/api/auth/login", new { Username = "admin", Password = "admin123" });
            loginResp.EnsureSuccessStatusCode();
            var loginBody = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(loginBody.TryGetProperty("token", out var tokenEl) && tokenEl.ValueKind == JsonValueKind.String);
            var token = tokenEl.GetString();
            Assert.False(string.IsNullOrEmpty(token));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var sample = new { StaffID = "int-test-1", StaffName = "Integration User", Email = "int@example.com", PhoneNumber = "+100000000" };
            var postResp = await client.PostAsJsonAsync("/api/staff", sample);
            // either created or ok depending on controller behavior
            Assert.True(postResp.IsSuccessStatusCode);
        }
    }
}
