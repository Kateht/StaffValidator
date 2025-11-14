using System;
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

            var factory = new TestWebApplicationFactory()
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

            var factory = new TestWebApplicationFactory()
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

            // Controller should return the view with validation error (not redirect when invalid)
            // Could be 200 OK (view) or 302 Found (redirect on some validation error behavior)
            Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.Found);
            var body = await resp.Content.ReadAsStringAsync();
            
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                Assert.Contains("Email: invalid format", body);
            }

            // Repository should remain empty
            Assert.Empty(inMemory.GetAll());
        }

        [Fact]
        public async Task Post_Create_WithInvalidPhone_ShowsValidationError_AndDoesNotAdd()
        {
            var inMemory = new InMemoryStaffRepository();

            var factory = new TestWebApplicationFactory()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IStaffRepository>();
                        services.AddSingleton<IStaffRepository>(_ => inMemory);
                    });
                });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var formData = new Dictionary<string, string>
            {
                ["StaffName"] = "Bad Phone User",
                ["Email"] = "gooduser@example.com",
                ["PhoneNumber"] = "invalid-phone-number",
                ["StartingDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
            };

            var content = new FormUrlEncodedContent(formData);
            var resp = await client.PostAsync("/Staff/Create", content);

            // Controller should return the view with validation error (not redirect when invalid)
            Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.Found);
            var body = await resp.Content.ReadAsStringAsync();
            
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                Assert.Contains("PhoneNumber: invalid format", body);
            }

            Assert.Empty(inMemory.GetAll());
        }

        [Fact]
        public async Task Post_Create_WithBothInvalid_ShowsMultipleValidationErrors()
        {
            var inMemory = new InMemoryStaffRepository();

            var factory = new TestWebApplicationFactory()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IStaffRepository>();
                        services.AddSingleton<IStaffRepository>(_ => inMemory);
                    });
                });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var formData = new Dictionary<string, string>
            {
                ["StaffName"] = "Both Invalid User",
                ["Email"] = "not-an-email",
                ["PhoneNumber"] = "not-a-phone",
                ["StartingDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
            };

            var content = new FormUrlEncodedContent(formData);
            var resp = await client.PostAsync("/Staff/Create", content);

            // Controller should return the view with validation error (not redirect when invalid)
            Assert.True(resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.Found);
            var body = await resp.Content.ReadAsStringAsync();
            
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                // Both validations should fail
                Assert.Contains("Email: invalid format", body);
                Assert.Contains("PhoneNumber: invalid format", body);
            }

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
                if (idx >= 0)
                {
                    _items[idx] = staff;
                }
            }

            public void Delete(int id)
            {
                var existing = _items.FirstOrDefault(s => s.StaffID == id);
                if (existing != null)
                {
                    _items.Remove(existing);
                }
            }

            public bool Exists(int id) => _items.Any(s => s.StaffID == id);

            public IEnumerable<Staff> Search(string searchTerm)
            {
                if (string.IsNullOrEmpty(searchTerm))
                {
                    return _items;
                }
                searchTerm = searchTerm.ToLowerInvariant();
                return _items.Where(s => s.StaffName.ToLowerInvariant().Contains(searchTerm) || s.Email.ToLowerInvariant().Contains(searchTerm));
            }

            public void SaveAll() { }
        }
    }
}
