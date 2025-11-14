using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using StaffValidator.Core.Models;
using StaffValidator.Core.Repositories;
using StaffValidator.WebApp.Data;

namespace StaffValidator.WebApp.Repositories
{
    public class EfStaffRepository : IStaffRepository
    {
        private readonly StaffDbContext _db;

        public EfStaffRepository(StaffDbContext db)
        {
            _db = db;
        }

        public IEnumerable<Staff> GetAll()
        {
            return _db.Staff.AsNoTracking().OrderBy(s => s.StaffID).ToList();
        }

        public Staff? Get(int id)
        {
            // Use AsNoTracking to avoid tracking conflicts when a separate instance is later Updated
            return _db.Staff.AsNoTracking().FirstOrDefault(s => s.StaffID == id);
        }

        public void Add(Staff staff)
        {
            _db.Staff.Add(staff);
            _db.SaveChanges();
        }

        public void Update(Staff staff)
        {
            // Ensure no already-tracked entity with same key is in the context
            var local = _db.Set<Staff>().Local.FirstOrDefault(e => e.StaffID == staff.StaffID);
            if (local != null)
            {
                _db.Entry(local).State = EntityState.Detached;
            }
            _db.Entry(staff).State = EntityState.Modified;
            _db.SaveChanges();
        }

        public void Delete(int id)
        {
            var entity = _db.Staff.FirstOrDefault(s => s.StaffID == id);
            if (entity != null)
            {
                _db.Staff.Remove(entity);
                _db.SaveChanges();
            }
        }

        public bool Exists(int id)
        {
            return _db.Staff.Any(s => s.StaffID == id);
        }

        public IEnumerable<Staff> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return GetAll();
            }

            var term = searchTerm.ToLowerInvariant();
            return _db.Staff.AsNoTracking()
                .Where(s => s.StaffName.ToLower().Contains(term) || s.Email.ToLower().Contains(term))
                .ToList();
        }

        public void SaveAll()
        {
            _db.SaveChanges();
        }
    }
}
