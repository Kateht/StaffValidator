using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StaffValidator.Core.Models;
using StaffValidator.Core.Repositories;
using Xunit;

namespace StaffValidator.Tests
{
    public class PostCreateIntegrationTests
    {
        [Fact]
        public async Task Post_Create_AddsStaff_AndRedirects()
        {
            // Arrange: create an in-memory repo and replace the application's registration
            var inMemory = new InMemoryStaffRepository();

            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        // remove existing registration(s) for IStaffRepository
                        services.RemoveAll<IStaffRepository>();
                        services.AddSingleton<IStaffRepository>(_ => inMemory);
                    });
                });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var form = new Dictionary<string, string>
            {
                ["StaffName"] = "Integration Test User",
                ["Email"] = "ituser@example.com",
                ["PhoneNumber"] = "+44 7000 000000",
                ["StartingDate"] = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
            };

            var content = new FormUrlEncodedContent(form);

            // Act
            var resp = await client.PostAsync("/Staff/Create", content);

            // Assert - expect redirect to Index
            Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);

            // Ensure repository received the added staff
            var all = inMemory.GetAll().ToList();
            Assert.Single(all);
            var added = all.First();
            Assert.Equal("Integration Test User", added.StaffName);
            Assert.Equal("ituser@example.com", added.Email);
        }

        [Fact]
        public async Task Post_Create_WithInvalidEmail_ShowsValidationError_AndDoesNotAdd()
        {
            var inMemory = new InMemoryStaffRepository();

            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IStaffRepository>();
                        services.AddSingleton<IStaffRepository>(_ => inMemory);
                    });
                });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var form = new Dictionary<string, string>
            {
                ["StaffName"] = "Bad Email User",
                ["Email"] = "not-an-email",
                ["PhoneNumber"] = "+44 7000 000000",
                ["StartingDate"] = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
            };

            var content = new FormUrlEncodedContent(form);

            var resp = await client.PostAsync("/Staff/Create", content);

            // Controller should return the view (200) with validation error and not redirect
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Email: invalid format", body);

            // Repository should remain empty
            Assert.Empty(inMemory.GetAll());
        }

        private class InMemoryStaffRepository : IStaffRepository
        {
            private readonly List<Staff> _items = new();

            public IEnumerable<Staff> GetAll() => _items.AsReadOnly();

            public Staff? Get(int id) => _items.FirstOrDefault(x => x.StaffID == id);

            public void Add(Staff staff)
            {
                staff.StaffID = _items.Count == 0 ? 1 : _items.Max(s => s.StaffID) + 1;
                _items.Add(staff);
            }

            public void Update(Staff staff)
            {
                var idx = _items.FindIndex(s => s.StaffID == staff.StaffID);
                if (idx >= 0) _items[idx] = staff;
            }

            public void SaveAll() { }
        }
    }
}
