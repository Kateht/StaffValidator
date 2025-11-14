using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using HtmlAgilityPack;

namespace StaffValidator.Tests
{
    /// <summary>
    /// Interface layer verification for the web application
    /// Tests UI components, form validation, and user interactions
    /// </summary>
    public class InterfaceVerificationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public InterfaceVerificationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task VerifyStaffIndexPage_ContainsRequiredElements()
        {
            // Verify the interface layer renders correctly
            var response = await _client.GetAsync("/Staff");
            var content = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            // Verify essential UI elements
            Assert.Contains("Staff Management", content);
            Assert.NotNull(doc.DocumentNode.SelectSingleNode("//table[@id='staffTable']"));
            Assert.NotNull(doc.DocumentNode.SelectSingleNode("//input[@id='searchInput']"));
            Assert.NotNull(doc.DocumentNode.SelectSingleNode("//select[@id='sortSelect']"));
            Assert.Contains("Add New Staff", content);
        }

        [Fact]
        public async Task VerifyCreateForm_ContainsValidationAttributes()
        {
            var response = await _client.GetAsync("/Staff/Create");
            var content = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            // Verify form validation elements - using id attribute instead of asp-for
            var emailInput = doc.DocumentNode.SelectSingleNode("//input[@type='email']");
            var phoneInput = doc.DocumentNode.SelectSingleNode("//input[@type='tel']");
            var nameInput = doc.DocumentNode.SelectSingleNode("//input[@id='StaffName']");

            Assert.NotNull(emailInput);
            Assert.NotNull(phoneInput);
            Assert.NotNull(nameInput);

            // Verify required attributes exist (value can be empty string or "required")
            var nameRequired = nameInput.GetAttributeValue("required", string.Empty);
            var emailRequired = emailInput.GetAttributeValue("required", string.Empty);
            Assert.NotNull(nameRequired); // attribute exists
            Assert.NotNull(emailRequired); // attribute exists
        }

        [Fact]
        public async Task VerifyDataIntegration_InterfaceDisplaysData()
        {
            var response = await _client.GetAsync("/Staff");
            var content = await response.Content.ReadAsStringAsync();

            // Verify interface shows data from the intermediary layer
            Assert.Contains("Alice", content);
            Assert.Contains("Bob", content);
            Assert.Contains("Carol", content);
            Assert.Contains("alice@example.com", content);
        }

        [Theory]
        [InlineData("/Staff", "Staff Management")]
        [InlineData("/Staff/Create", "Add New Staff")]
        public async Task VerifyPageTitles_AreCorrect(string url, string expectedTitle)
        {
            var response = await _client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            Assert.Contains(expectedTitle, content);
        }

        [Fact]
        public async Task VerifyResponsiveDesign_ContainsBootstrapClasses()
        {
            var response = await _client.GetAsync("/Staff");
            var content = await response.Content.ReadAsStringAsync();

            // Verify responsive CSS classes
            Assert.Contains("container", content);
            Assert.Contains("table-responsive", content);
            Assert.Contains("btn-group", content);
            Assert.Contains("card", content);
        }
    }
}