using System;
using System.IO;
using System.Linq;
using StaffValidator.Core.Models;
using StaffValidator.Core.Repositories;
using Xunit;

namespace StaffValidator.Tests
{
    public class StaffRepositoryTests : IDisposable
    {
        private readonly string _tempPath;
        private readonly StaffRepository _repo;

        public StaffRepositoryTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"staff_records_test_{Guid.NewGuid():N}.json");
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_tempPath) ?? Path.GetTempPath());
            _repo = new StaffRepository(_tempPath);
        }

        [Fact]
        public void Add_Get_Update_Workflow_Works()
        {
            var s = new Staff
            {
                StaffName = "Test User",
                Email = "test@example.com",
                PhoneNumber = "+1 555 9999",
                StartingDate = DateTime.UtcNow
            };

            _repo.Add(s);
            var all = _repo.GetAll().ToList();
            Assert.Single(all);
            var loaded = all.First();
            Assert.Equal("Test User", loaded.StaffName);

            // Update
            loaded.StaffName = "Updated Name";
            _repo.Update(loaded);
            var reloaded = _repo.Get(loaded.StaffID);
            Assert.NotNull(reloaded);
            Assert.Equal("Updated Name", reloaded!.StaffName);
        }

        public void Dispose()
        {
            try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
        }
    }
}
