namespace StaffValidator.Core.Attributes
{
    public sealed class PhoneCheckAttribute : RegexCheckAttribute
    {
        public PhoneCheckAttribute(string pattern) : base(pattern) { }
    }
}
