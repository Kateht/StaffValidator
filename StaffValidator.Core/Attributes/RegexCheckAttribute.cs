using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace StaffValidator.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class RegexCheckAttribute : ValidationAttribute
    {
        public string Pattern { get; }

        public RegexCheckAttribute(string pattern)
        {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var str = value?.ToString() ?? string.Empty;
            var pattern = Pattern;
            if (!pattern.StartsWith("^")) pattern = "^" + pattern;
            if (!pattern.EndsWith("$")) pattern += "$";

            try
            {
                var regex = new Regex(pattern, RegexOptions.CultureInvariant);
                if (!regex.IsMatch(str))
                {
                    var msg = string.IsNullOrEmpty(ErrorMessage) ? $"{validationContext.MemberName}: invalid format" : ErrorMessage;
                    return new ValidationResult(msg);
                }
                return ValidationResult.Success;
            }
            catch (ArgumentException ex)
            {
                return new ValidationResult($"{validationContext.MemberName}: invalid regex ({ex.Message})");
            }
        }
    }
}
