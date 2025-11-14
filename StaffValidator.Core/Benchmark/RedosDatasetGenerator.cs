using System.Collections.Generic;

namespace StaffValidator.Core.Benchmark
{
    /// <summary>
    /// Generates malicious input strings designed to trigger ReDoS (Regular Expression Denial of Service) attacks
    /// </summary>
    public static class RedosDatasetGenerator
    {
        /// <summary>
        /// Generates email-like strings with exponential backtracking patterns
        /// Pattern targets: ([._%+\-][A-Za-z0-9]+)* - causes exponential backtracking
        /// </summary>
        public static List<string> GenerateEmailRedosAttacks()
        {
            var attacks = new List<string>();

            // EVIL Pattern 1: Catastrophic backtracking with ambiguous separator matching
            // The ([._%+\-][A-Za-z0-9]+)* can match ".aaa" as one group or ".a" + ".a" + ".a"
            // When it fails at the end, it tries EVERY possible combination = O(2^n)
            for (int len = 50; len <= 250; len += 50)
            {
                // Create: aaa.aaa.aaa...X - the regex tries to match each .aaa as separate groups
                // then backtracks when X doesn't match, trying all 2^n combinations
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    sb.Append(".aaa");
                }
                sb.Append("X"); // Invalid ending forces full backtracking
                attacks.Add(sb.ToString());
                
                // Variant with underscores
                sb.Clear();
                for (int i = 0; i < len; i++)
                {
                    sb.Append("_bbb");
                }
                sb.Append("!");
                attacks.Add(sb.ToString());
            }

            // EVIL Pattern 2: Nested quantifiers with overlapping matches
            // Pattern like: a+a+a+...@ where each 'a+' can be grouped differently
            for (int len = 60; len <= 300; len += 60)
            {
                // Create patterns where separator can be part of multiple groups
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    sb.Append(".a");
                }
                sb.Append(".@X"); // Forces backtracking through all combinations
                attacks.Add(sb.ToString());
                
                // Mix separators to maximize ambiguity
                sb.Clear();
                for (int i = 0; i < len; i++)
                {
                    sb.Append(i % 2 == 0 ? ".x" : "_x");
                }
                sb.Append("@!");
                attacks.Add(sb.ToString());
            }

            // EVIL Pattern 3: Domain part with catastrophic backtracking
            // ([A-Za-z0-9\-]+)* in domain - each char can start/end a group
            for (int len = 80; len <= 400; len += 80)
            {
                var sb = new System.Text.StringBuilder("user@");
                // Create: a-a-a-a-...X where regex tries all ways to split into groups
                for (int i = 0; i < len; i++)
                {
                    sb.Append("a-");
                }
                sb.Append("X"); // Invalid TLD forces backtracking
                attacks.Add(sb.ToString());
                
                // Variant: long sequence then invalid char
                sb = new System.Text.StringBuilder("test@");
                for (int i = 0; i < len; i++)
                {
                    sb.Append("x");
                }
                sb.Append("!"); // Not a valid TLD start
                attacks.Add(sb.ToString());
            }

            // EVIL Pattern 4: Maximum ambiguity - every character can be grouped multiple ways
            for (int len = 50; len <= 250; len += 50)
            {
                // Pattern: .a.a.a...a@ where @ position is ambiguous
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    sb.Append(".a");
                }
                sb.Append("@"); // @ can be considered part of username or separator
                sb.Append(new string('b', len)); // Then a long invalid domain
                sb.Append("X"); // No valid TLD
                attacks.Add(sb.ToString());
            }

            // EVIL Pattern 5: Deeply nested with multiple quantifier levels
            // Most evil - combines all techniques
            for (int len = 100; len <= 300; len += 100)
            {
                var sb = new System.Text.StringBuilder();
                // Create pattern where EVERY char could be a separator or part of name
                for (int i = 0; i < len; i++)
                {
                    sb.Append(".");
                    sb.Append("a");
                }
                sb.Append("@");
                for (int i = 0; i < len; i++)
                {
                    sb.Append("-");
                    sb.Append("b");
                }
                sb.Append("!invalid"); // Forces complete backtracking
                attacks.Add(sb.ToString());
            }

            // Normal valid emails as baseline (for comparison)
            attacks.Add("user@example.com");
            attacks.Add("test.user@domain.co.uk");
            attacks.Add("valid.email123@test-domain.org");
            attacks.Add("simple@test.com");

            return attacks;
        }

        /// <summary>
        /// Generates phone-like strings with exponential backtracking patterns
        /// Pattern targets: (\d[\s\-]?)+ and optional groups - causes exponential backtracking
        /// </summary>
        public static List<string> GeneratePhoneRedosAttacks()
        {
            var attacks = new List<string>();

            // EVIL Pattern 1: Catastrophic backtracking with ambiguous separator
            // The (\d[\s\-]?)+ can match "1-" as one iteration or "1" + "-1" split across iterations
            // When it fails, tries every possible split = O(2^n) complexity
            for (int len = 50; len <= 300; len += 50)
            {
                // Create: 1-1-1-...X where each digit-separator can be grouped multiple ways
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    sb.Append("1-");
                }
                sb.Append("X"); // Invalid ending forces full backtracking
                attacks.Add(sb.ToString());
                
                // Variant with spaces - even worse because space is harder to optimize
                sb.Clear();
                for (int i = 0; i < len; i++)
                {
                    sb.Append("5 ");
                }
                sb.Append("!");
                attacks.Add(sb.ToString());
                
                // Mix both separators for maximum ambiguity
                sb.Clear();
                for (int i = 0; i < len; i++)
                {
                    sb.Append(i % 2 == 0 ? "9-" : "9 ");
                }
                sb.Append("a");
                attacks.Add(sb.ToString());
            }

            // EVIL Pattern 2: Deeply nested optional groups
            // Pattern (\(?\d{2,4}\)?[\s\-]?)? creates exponential possibilities
            for (int len = 60; len <= 360; len += 60)
            {
                // Each digit can be: inside parens, outside parens, with separator, without
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    sb.Append(i % 3 == 0 ? "(1" : i % 3 == 1 ? "2)" : "3");
                }
                sb.Append("X"); // Forces backtracking through all possibilities
                attacks.Add(sb.ToString());
                
                // Long sequence of digits with optional separators
                sb.Clear();
                for (int i = 0; i < len; i++)
                {
                    sb.Append("1");
                }
                for (int i = 0; i < len / 3; i++)
                {
                    sb.Append(i % 2 == 0 ? " " : "-");
                }
                sb.Append("!");
                attacks.Add(sb.ToString());
            }

            // EVIL Pattern 3: Country code ambiguity
            // Pattern (\+?\d{1,3}[\s\-]?)? where + and digits can be grouped many ways
            for (int len = 80; len <= 480; len += 80)
            {
                var sb = new System.Text.StringBuilder("+");
                // Create: +1-1-1-...invalid where + can be part of country code or separate
                for (int i = 0; i < len; i++)
                {
                    sb.Append("1-");
                }
                sb.Append("invalid");
                attacks.Add(sb.ToString());
                
                // Multiple + signs increase ambiguity
                sb = new System.Text.StringBuilder("+");
                for (int i = 0; i < len; i++)
                {
                    sb.Append(i % 10 == 0 ? "+" : "9");
                }
                sb.Append("X");
                attacks.Add(sb.ToString());
            }

            // EVIL Pattern 4: Maximum separator ambiguity
            // Each separator position creates branching in regex engine
            for (int len = 50; len <= 300; len += 50)
            {
                var sb = new System.Text.StringBuilder();
                // Pattern: 1 2-3 4-5 6-... where each separator could belong to previous or next digit
                for (int i = 0; i < len; i++)
                {
                    sb.Append(i);
                    sb.Append(i % 3 == 0 ? " " : i % 3 == 1 ? "-" : "");
                }
                sb.Append("abc"); // Invalid ending
                attacks.Add(sb.ToString());
            }

            // EVIL Pattern 5: Extreme length with overlapping patterns
            // Combines all evil techniques - parentheses, separators, digits
            for (int len = 100; len <= 500; len += 100)
            {
                var sb = new System.Text.StringBuilder();
                // Create deeply ambiguous pattern
                for (int i = 0; i < len; i++)
                {
                    if (i % 5 == 0)
                    {
                        sb.Append("(");
                    }
                    sb.Append(i % 10);
                    if (i % 5 == 4)
                    {
                        sb.Append(")");
                    }
                    if (i % 2 == 0)
                    {
                        sb.Append(i % 4 == 0 ? "-" : " ");
                    }
                }
                sb.Append("X!invalid"); // Forces complete backtracking
                attacks.Add(sb.ToString());
            }

            // EVIL Pattern 6: Pure digit sequence with separators at end
            // Regex tries to match digits, then fails on separators, backtracks through ALL digits
            for (int len = 100; len <= 400; len += 100)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    sb.Append(i % 10);
                }
                for (int i = 0; i < len / 2; i++)
                {
                    sb.Append("-");
                }
                sb.Append("X");
                attacks.Add(sb.ToString());
            }

            // Normal valid phones as baseline (for comparison)
            attacks.Add("1234567890");
            attacks.Add("+1-555-123-4567");
            attacks.Add("(555) 123-4567");
            attacks.Add("+84 123 456 789");
            attacks.Add("555-1234");

            return attacks;
        }

        /// <summary>
        /// Generates detailed dataset with metadata for analysis
        /// </summary>
        public static List<RedosTestCase> GenerateDetailedEmailRedos()
        {
            var testCases = new List<RedosTestCase>();

            // Catastrophic Backtracking - Repeating separators
            AddTestCases(testCases, "Catastrophic_Backtracking_Separators", 
                new[] { 20, 40, 60, 80, 100 },
                len => "user" + string.Concat(System.Linq.Enumerable.Repeat(".a", len)) + "X");

            // Catastrophic Backtracking - Underscore pattern
            AddTestCases(testCases, "Catastrophic_Backtracking_Underscore",
                new[] { 25, 50, 75, 100, 125 },
                len => "test" + string.Concat(System.Linq.Enumerable.Repeat("_a", len)) + "!");

            // Nested Quantifiers - Plus signs
            AddTestCases(testCases, "Nested_Quantifiers_Plus",
                new[] { 30, 60, 90, 120, 150 },
                len => string.Concat(System.Linq.Enumerable.Repeat("a+", len)) + "@test");

            // Overlapping Patterns - Domain backtracking
            AddTestCases(testCases, "Overlapping_Domain",
                new[] { 40, 80, 120, 160, 200 },
                len => "user@" + string.Concat(System.Linq.Enumerable.Repeat("a-", len)) + "!");

            // Alternating patterns
            AddTestCases(testCases, "Alternating_Pattern",
                new[] { 20, 40, 60, 80, 100 },
                len => string.Concat(System.Linq.Enumerable.Repeat("ab", len)) + "@" + string.Concat(System.Linq.Enumerable.Repeat("cd", len)));

            // Normal Input (baseline)
            testCases.Add(new RedosTestCase("Normal_Input", 18, "user.test@example.com"));
            testCases.Add(new RedosTestCase("Normal_Input", 25, "test123@domain.co.uk"));
            testCases.Add(new RedosTestCase("Normal_Input", 30, "valid_email+tag@subdomain.test.org"));

            return testCases;
        }

        /// <summary>
        /// Generates detailed dataset for phone ReDoS attacks
        /// </summary>
        public static List<RedosTestCase> GenerateDetailedPhoneRedos()
        {
            var testCases = new List<RedosTestCase>();

            // Exponential Backtracking - Dash separator
            AddTestCases(testCases, "Exponential_Backtracking_Dash",
                new[] { 20, 40, 60, 80, 100 },
                len => string.Concat(System.Linq.Enumerable.Repeat("1-", len)) + "X");

            // Exponential Backtracking - Space separator
            AddTestCases(testCases, "Exponential_Backtracking_Space",
                new[] { 25, 50, 75, 100, 125 },
                len => string.Concat(System.Linq.Enumerable.Repeat("5 ", len)) + "!");

            // Nested Optional Groups - Parentheses
            AddTestCases(testCases, "Nested_Optional_Parentheses",
                new[] { 30, 60, 90, 120, 150 },
                len => string.Concat(System.Linq.Enumerable.Repeat("(12)", len / 3)) + new string('3', len / 2) + "X");

            // Country Code Pattern
            AddTestCases(testCases, "Country_Code_Pattern",
                new[] { 35, 70, 105, 140, 175 },
                len => "+" + string.Concat(System.Linq.Enumerable.Repeat("1-", len)) + "invalid");

            // Mixed Separators
            AddTestCases(testCases, "Mixed_Separators",
                new[] { 40, 80, 120, 160, 200 },
                len => string.Concat(System.Linq.Enumerable.Repeat("1 2-", len / 2)) + "abc");

            // Extreme Length Pattern
            AddTestCases(testCases, "Extreme_Length",
                new[] { 50, 100, 150, 200, 250 },
                len => new string('8', len) + new string('-', len / 3) + new string(' ', len / 3) + "X");

            // Normal Phone (baseline)
            testCases.Add(new RedosTestCase("Normal_Phone", 10, "1234567890"));
            testCases.Add(new RedosTestCase("Normal_Phone", 12, "+84123456789"));
            testCases.Add(new RedosTestCase("Normal_Phone", 15, "(555) 123-4567"));
            testCases.Add(new RedosTestCase("Normal_Phone", 17, "+1-555-123-4567"));

            return testCases;
        }

        private static void AddTestCases(List<RedosTestCase> list, string pattern, int[] lengths, System.Func<int, string> generator)
        {
            foreach (var len in lengths)
            {
                list.Add(new RedosTestCase(pattern, len, generator(len)));
            }
        }
    }

    /// <summary>
    /// Represents a single ReDoS test case with metadata
    /// </summary>
    public class RedosTestCase
    {
        public string AttackPattern { get; set; }
        public int InputLength { get; set; }
        public string Input { get; set; }

        public RedosTestCase(string pattern, int length, string input)
        {
            AttackPattern = pattern;
            InputLength = length;
            Input = input;
        }
    }
}
