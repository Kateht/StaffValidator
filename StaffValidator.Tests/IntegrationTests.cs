using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StaffValidator.Tests
{
    public class IntegrationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public IntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Get_StaffIndex_ReturnsOkAndContainsTitle()
        {
            var client = _factory.CreateClient();
            var resp = await client.GetAsync("/Staff");
            var content = await resp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Contains("Staff Management", content);
        }

        [Fact]
        public async Task Get_StaffCreate_ReturnsOkAndContainsTitle()
        {
            var client = _factory.CreateClient();
            var resp = await client.GetAsync("/Staff/Create");
            var content = await resp.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.Contains("Add New Staff", content);
        }
    }
}
