using System;
using System.Collections.Generic;
using System.Linq;

namespace StaffValidator.Core.Benchmark
{
    /// <summary>
    /// Generates synthetic datasets for benchmarking validation performance.
    /// Includes valid, invalid, and adversarial (ReDoS-style) test cases.
    /// </summary>
    public static class BenchmarkDataset
    {
        private static readonly Random _random = new Random(42); // Fixed seed for reproducibility

        /// <summary>
        /// Generates a dataset of test strings based on the specified type and count.
        /// </summary>
        /// <param name="type">Type of dataset: "email", "phone", or "redos"</param>
        /// <param name="count">Number of samples to generate</param>
        /// <returns>List of test strings with mixed valid/invalid samples</returns>
        public static List<string> Generate(string type, int count)
        {
            return type?.ToLowerInvariant() switch
            {
                "email" => GenerateEmailDataset(count),
                "email-hybrid" => GenerateEmailHybridDataset(count),
                "phone" => GeneratePhoneDataset(count),
                "redos" => GenerateRedosDataset(count),
                "mixed" => GenerateMixedDataset(count),
                _ => GenerateEmailDataset(count)
            };
        }

        /// <summary>
        /// Generates email addresses with 50% valid, 50% invalid samples.
        /// </summary>
        private static List<string> GenerateEmailDataset(int count)
        {
            var list = new List<string>(count);
            var domains = new[] { "gmail.com", "yahoo.com", "outlook.com", "company.vn", "test.edu.vn" };
            var invalidPatterns = new[] { "@@@", "no-at-sign", "multiple@@at.com", "spaces in@email.com", ".starts@with.dot" };

            for (int i = 0; i < count; i++)
            {
                if (i % 2 == 0)
                {
                    // Valid email
                    var username = $"user{i}_{_random.Next(1000)}";
                    var domain = domains[_random.Next(domains.Length)];
                    list.Add($"{username}@{domain}");
                }
                else
                {
                    // Invalid email
                    var pattern = invalidPatterns[_random.Next(invalidPatterns.Length)];
                    list.Add($"invalid{i}{pattern}");
                }
            }

            return list;
        }

        /// <summary>
        /// Generates a hybrid email dataset where Hybrid should excel:
        /// Mostly normal email samples mixed with adversarial email-shaped ReDoS inputs.
        /// Wrapper with default adversarial ratio of 0.10 (10%).
        /// </summary>
        public static List<string> GenerateEmailHybridDataset(int count)
        {
            return GenerateEmailHybridDataset(count, 0.10);
        }

        /// <summary>
        /// Mix normal emails with adversarial email-shaped ReDoS inputs.
        /// Clamps ratio to [0.0, 0.5] and ensures at least 1 adversarial when ratio > 0 and count > 0.
        /// </summary>
        public static List<string> GenerateEmailHybridDataset(int count, double adversarialRatio = 0.10)
        {
            if (count <= 0) return new List<string>();

            if (double.IsNaN(adversarialRatio) || double.IsInfinity(adversarialRatio)) adversarialRatio = 0.10;
            var ratio = Math.Max(0.0, Math.Min(0.5, adversarialRatio));

            int adversarial = ratio <= 0.0 ? 0 : Math.Max(1, (int)Math.Round(count * ratio));
            int normal = Math.Max(0, count - adversarial);

            var normalEmails = GenerateEmailDataset(normal);

            var attacksSource = RedosDatasetGenerator.GenerateEmailRedosAttacks();
            var adversarialEmails = new List<string>(adversarial);
            for (int i = 0; i < adversarial; i++)
            {
                adversarialEmails.Add(attacksSource[i % attacksSource.Count]);
            }

            var combined = normalEmails.Concat(adversarialEmails).ToList();
            return combined.OrderBy(_ => _random.Next()).ToList();
        }

        
        /// <summary>
        /// Generates phone numbers with international formats, 50% valid, 50% invalid.
        /// </summary>
        private static List<string> GeneratePhoneDataset(int count)
        {
            var list = new List<string>(count);
            var countryCodes = new[] { "+1", "+44", "+84", "+81", "+86" };

            for (int i = 0; i < count; i++)
            {
                if (i % 2 == 0)
                {
                    // Valid phone
                    var cc = countryCodes[_random.Next(countryCodes.Length)];
                    var number = _random.Next(100000000, 999999999);
                    var format = _random.Next(3);
                    
                    list.Add(format switch
                    {
                        0 => $"{cc} {number}",
                        1 => $"{cc}-{number}",
                        _ => $"{cc}{number}"
                    });
                }
                else
                {
                    // Invalid phone
                    var invalidFormats = new[] 
                    { 
                        $"abc{_random.Next(1000)}",
                        $"++{_random.Next(100)}",
                        new string('1', 20), // too long
                        "123", // too short
                        "phone-number-text"
                    };
                    list.Add(invalidFormats[_random.Next(invalidFormats.Length)]);
                }
            }

            return list;
        }

        /// <summary>
        /// Generates adversarial ReDoS-style inputs designed to stress-test regex engines.
        /// Uses RedosDatasetGenerator to create realistic email-based attack patterns.
        /// </summary>
        private static List<string> GenerateRedosDataset(int count)
        {
            // For the classic catastrophic-backtracking pattern ^(a+)+$,
            // we generate inputs that trigger worst-case backtracking:
            //  - Many 'a's followed by a non-matching char (e.g., 'X')
            //  - Some pure matching strings of 'a's (to mix results)
            var results = new List<string>(count);

            // Base lengths chosen to scale quickly; adjust as needed
            var lengths = new List<int>();
            for (int i = 100; i <= 5000; i += 100)
            {
                lengths.Add(i);
            }

            int idx = 0;
            while (results.Count < count)
            {
                var len = lengths[idx % lengths.Count];

                // Worst-case: long run of 'a' then a failing char
                results.Add(new string('a', len) + "X");
                if (results.Count >= count)
                {
                    break;
                }

                // Matching case: long run of 'a' only
                results.Add(new string('a', len));

                idx++;
            }

            return results;
        }

        /// <summary>
        /// Generates a mixed dataset with both email and phone samples.
        /// </summary>
        private static List<string> GenerateMixedDataset(int count)
        {
            var emails = GenerateEmailDataset(count / 2);
            var phones = GeneratePhoneDataset(count / 2);
            var mixed = emails.Concat(phones).ToList();
            
            // Shuffle for randomness
            return mixed.OrderBy(_ => _random.Next()).ToList();
        }

        /// <summary>
        /// Generates a dataset with known ground truth for accuracy testing.
        /// </summary>
        /// <returns>Tuple of (input, expectedValid) pairs</returns>
        public static List<(string Input, bool ExpectedValid)> GenerateGroundTruthDataset(string type, int count)
        {
            var result = new List<(string, bool)>(count);

            if (type?.ToLowerInvariant() == "email")
            {
                var validEmails = new[]
                {
                    "test@example.com",
                    "user.name@domain.co.uk",
                    "first+last@company.vn",
                    "admin123@test-server.edu",
                };

                var invalidEmails = new[]
                {
                    "not-an-email",
                    "@no-local-part.com",
                    "no-at-sign.com",
                    "multiple@@signs@test.com",
                    "spaces in@email.com",
                };

                for (int i = 0; i < count; i++)
                {
                    if (i % 2 == 0)
                    {
                        result.Add((validEmails[i % validEmails.Length], true));
                    }
                    else
                    {
                        result.Add((invalidEmails[i % invalidEmails.Length], false));
                    }
                }
            }
            else if (type?.ToLowerInvariant() == "phone")
            {
                var validPhones = new[]
                {
                    "+1 555 123 4567",
                    "+44 20 7946 0958",
                    "+84 123456789",
                    "555-1234",
                };

                var invalidPhones = new[]
                {
                    "abc123",
                    "12", // too short
                    new string('1', 20), // too long
                    "+++invalid",
                };

                for (int i = 0; i < count; i++)
                {
                    if (i % 2 == 0)
                    {
                        result.Add((validPhones[i % validPhones.Length], true));
                    }
                    else
                    {
                        result.Add((invalidPhones[i % invalidPhones.Length], false));
                    }
                }
            }

            return result;
        }
    }
}
