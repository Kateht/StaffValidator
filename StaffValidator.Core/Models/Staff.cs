using System;
using StaffValidator.Core.Attributes;

namespace StaffValidator.Core.Models
{
    public class Staff
    {
        public int StaffID { get; set; }
        public string StaffName { get; set; } = string.Empty;

        [EmailCheck(@"^[A-Za-z0-9]+([._%+\-][A-Za-z0-9]+)*@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$")]
        public string Email { get; set; } = string.Empty;

        [PhoneCheck(@"^(\+?\d{1,3}[\s\-]?)?(\(?\d{2,4}\)?[\s\-]?)?[\d\s\-]{6,15}$")]
        public string PhoneNumber { get; set; } = string.Empty;

        public DateTime StartingDate { get; set; } = DateTime.UtcNow;
        public string PhotoPath { get; set; } = string.Empty;
    }
}
