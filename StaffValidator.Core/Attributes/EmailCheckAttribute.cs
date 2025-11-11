namespace StaffValidator.Core.Attributes
{
    public sealed class EmailCheckAttribute : RegexCheckAttribute
    {
        public EmailCheckAttribute(string pattern) : base(pattern) { }
    }
}
