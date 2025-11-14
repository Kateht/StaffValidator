using System.Text.RegularExpressions;

namespace StaffValidator.Checker.Utils
{
    public static class HtmlHelper
    {
        public static string? ExtractCsrfToken(string html)
        {
            var tokenMatch = Regex.Match(
                html,
                @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
                RegexOptions.IgnoreCase);

            return tokenMatch.Success ? tokenMatch.Groups[1].Value : null;
        }
    }
}
