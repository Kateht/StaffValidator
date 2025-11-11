using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using StaffValidator.Core.Attributes;

namespace StaffValidator.Core.Services
{
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public string Message { get; set; } = string.Empty;
    }

    public class ValidatorService
    {
        private static readonly ConcurrentDictionary<string, Regex> _regexCache = new();

        private static Regex GetOrAddRegex(string pattern)
        {
            if (!pattern.StartsWith("^")) pattern = "^" + pattern;
            if (!pattern.EndsWith("$")) pattern += "$";
            return _regexCache.GetOrAdd(pattern,
                p => new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant));
        }

        public virtual (bool ok, List<string> errors) ValidateAll(object obj)
        {
            var errors = new List<string>();
            foreach (var p in obj.GetType().GetProperties())
            {
                var attr = p.GetCustomAttribute<RegexCheckAttribute>();
                if (attr == null) continue;

                var value = p.GetValue(obj)?.ToString() ?? "";
                try
                {
                    var regex = GetOrAddRegex(attr.Pattern);
                    if (!regex.IsMatch(value))
                        errors.Add($"{p.Name}: invalid format ({attr.Pattern})");
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"{p.Name}: invalid regex ({ex.Message})");
                }
            }
            return (errors.Count == 0, errors);
        }
    }
}
