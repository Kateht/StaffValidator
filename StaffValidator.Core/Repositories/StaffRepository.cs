using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StaffValidator.Core.Models;

namespace StaffValidator.Core.Repositories
{
    public interface IStaffRepository
    {
        IEnumerable<Staff> GetAll();
        Staff? Get(int id);
        void Add(Staff staff);
        void Update(Staff staff);
        void Delete(int id);
        bool Exists(int id);
        IEnumerable<Staff> Search(string searchTerm);
        void SaveAll();
    }

    public class StaffRepository : IStaffRepository
    {
        private readonly string _jsonPath;
        private readonly List<Staff> _items;

        public StaffRepository(string jsonPath = "data/staff_records.json")
        {
            _jsonPath = jsonPath;
            if (!File.Exists(_jsonPath))
            {
                _items = new List<Staff>();
                SaveAll();
            }
            else
            {
                var text = File.ReadAllText(_jsonPath);
                _items = JsonSerializer.Deserialize<List<Staff>>(text) ?? new List<Staff>();
            }
        }

        // New constructor to support configuration via IOptions
        public StaffRepository(IOptions<StaffRepositoryOptions> options)
            : this(options?.Value?.JsonPath ?? "data/staff_records.json")
        {
        }

        public IEnumerable<Staff> GetAll() => _items.AsReadOnly();

        public Staff? Get(int id) => _items.FirstOrDefault(x => x.StaffID == id);

        public void Add(Staff staff)
        {
            staff.StaffID = _items.Count == 0 ? 1 : _items.Max(s => s.StaffID) + 1;
            _items.Add(staff);
            SaveAll();
        }

        public void Update(Staff staff)
        {
            var idx = _items.FindIndex(s => s.StaffID == staff.StaffID);
            if (idx >= 0)
            {
                _items[idx] = staff;
                SaveAll();
            }
        }

        public void Delete(int id)
        {
            var staff = _items.FirstOrDefault(s => s.StaffID == id);
            if (staff != null)
            {
                _items.Remove(staff);
                SaveAll();
            }
        }

        public bool Exists(int id)
        {
            return _items.Any(s => s.StaffID == id);
        }

        public IEnumerable<Staff> Search(string searchTerm)
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                return _items;
            }

            searchTerm = searchTerm.ToLowerInvariant();
            return _items.Where(s => 
                s.StaffName.ToLowerInvariant().Contains(searchTerm) ||
                s.Email.ToLowerInvariant().Contains(searchTerm));
        }

        public void SaveAll()
        {
            var dir = Path.GetDirectoryName(_jsonPath) ?? ".";
            Directory.CreateDirectory(dir);
            File.WriteAllText(_jsonPath,
                JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
