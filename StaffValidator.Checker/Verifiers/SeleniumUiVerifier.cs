using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using System.Reflection;
using System.Text.RegularExpressions;
using StaffValidator.Core.Models;
using StaffValidator.Core.Attributes;
using OpenQA.Selenium.Support.UI;

namespace StaffValidator.Checker.Verifiers
{
    public class SeleniumUiVerifier
    {
        private readonly string _baseUrl;
        private readonly string _browser; // "edge" or "chrome"
        private readonly bool _headless;
        private readonly TimeSpan _timeout;
        private readonly string? _username;
        private readonly string? _password;
        private readonly int _delayMs;

        public SeleniumUiVerifier(string baseUrl, string browser = "edge", bool headless = true, int timeoutSeconds = 15, string? username = null, string? password = null, int delayMs = 500)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _browser = string.IsNullOrWhiteSpace(browser) ? "edge" : browser.ToLowerInvariant();
            _headless = headless;
            _timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds));
            _username = username;
            _password = password;
            _delayMs = Math.Max(0, delayMs);
        }

        public async Task<SeleniumUiVerificationResult> VerifyAsync()
        {
            var result = new SeleniumUiVerificationResult();
            IWebDriver? driver = null;

            try
            {
                driver = CreateDriver();
                driver.Manage().Timeouts().ImplicitWait = _timeout;

                // Ensure we are authenticated before visiting protected pages
                EnsureLoggedIn(driver, result);

                Pause();

                VisitAndExpect(driver, _baseUrl + "/", new [] { "Staff Management", "Staff Records" }, "Home page (Staff index)", result);
                Pause();
                VisitAndExpect(driver, _baseUrl + "/Staff", new [] { "Staff Management", "Email", "Add New Staff" }, "Staff list page", result);

                // Create page expectations
                driver.Navigate().GoToUrl(_baseUrl + "/Staff/Create");
                Pause();
                ExpectFieldByName(driver, "StaffName", result);
                ExpectFieldByName(driver, "Email", result);
                ExpectFieldByName(driver, "PhoneNumber", result);
                ExpectFormById(driver, "staffForm", result);

                // Run data-driven tests on the Create form for email and phone
                RunCreateFormCases(driver, result);

                result.Success = result.Failures.Count == 0 && result.Cases.TrueForAll(c => c.Pass);
            }
            catch (WebDriverException wde)
            {
                result.Failures.Add($"WebDriver error: {wde.Message}");
            }
            catch (Exception ex)
            {
                result.Failures.Add($"Unexpected error: {ex.Message}");
            }
            finally
            {
                try { driver?.Quit(); driver?.Dispose(); } catch { }
            }

            await Task.CompletedTask;
            return result;
        }

        private IWebDriver CreateDriver()
        {
            if (_browser == "chrome")
            {
                var options = new ChromeOptions();
                if (_headless) options.AddArgument("--headless=new");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--window-size=1920,1080");
                return new ChromeDriver(options);
            }
            else
            {
                var options = new EdgeOptions();
                if (_headless) options.AddArgument("--headless=new");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--window-size=1920,1080");
                return new EdgeDriver(options);
            }
        }

        private void VisitAndExpect(IWebDriver driver, string url, IEnumerable<string> mustContain, string label, SeleniumUiVerificationResult result)
        {
            driver.Navigate().GoToUrl(url);
            Pause();
            var html = driver.PageSource ?? string.Empty;
            foreach (var token in mustContain)
            {
                if (!html.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    result.Failures.Add($"{label} missing token: '{token}'");
                }
            }
        }

        private void ExpectFieldByName(IWebDriver driver, string name, SeleniumUiVerificationResult result)
        {
            try
            {
                driver.FindElement(By.Name(name));
            }
            catch (NoSuchElementException)
            {
                result.Failures.Add($"Missing form field: {name}");
            }
        }

        private void ExpectFormById(IWebDriver driver, string id, SeleniumUiVerificationResult result)
        {
            try
            {
                driver.FindElement(By.Id(id));
            }
            catch (NoSuchElementException)
            {
                result.Failures.Add($"Missing form with id: {id}");
            }
        }

        private void EnsureLoggedIn(IWebDriver driver, SeleniumUiVerificationResult result)
        {
            // Try to access a protected page to trigger login redirect
            driver.Navigate().GoToUrl(_baseUrl + "/Staff");
            Pause();

            if (driver.Url.Contains("/Auth/Login", StringComparison.OrdinalIgnoreCase))
            {
                // Use provided credentials or default demo admin
                var user = string.IsNullOrEmpty(_username) ? "admin" : _username;
                var pass = string.IsNullOrEmpty(_password) ? "admin123" : _password;

                try
                {
                    var userInput = driver.FindElement(By.Name("Username"));
                    var passInput = driver.FindElement(By.Name("Password"));

                    userInput.Clear();
                    userInput.SendKeys(user);
                    passInput.Clear();
                    passInput.SendKeys(pass + Keys.Enter);
                    // Wait until we are not on the login page or timeout
                    var wait = new WebDriverWait(driver, _timeout);
                    try { wait.Until(d => !d.Url.Contains("/Auth/Login", StringComparison.OrdinalIgnoreCase)); }
                    catch { }
                    if (driver.Url.Contains("/Auth/Login", StringComparison.OrdinalIgnoreCase)) result.Failures.Add("Login failed: still on Login page after submitting credentials");
                }
                catch (NoSuchElementException)
                {
                    result.Failures.Add("Login form not found: expected fields 'Username' and 'Password'");
                }
            }
        }

        private void RunCreateFormCases(IWebDriver driver, SeleniumUiVerificationResult result)
        {
            // Pull regex patterns from model attributes to align expectations
            var emailAttr = typeof(Staff).GetProperty(nameof(Staff.Email))?
                .GetCustomAttribute<EmailCheckAttribute>();
            var phoneAttr = typeof(Staff).GetProperty(nameof(Staff.PhoneNumber))?
                .GetCustomAttribute<PhoneCheckAttribute>();

            var emailPattern = emailAttr?.Pattern ?? @"^[A-Za-z0-9]+([._%+\-][A-Za-z0-9]+)*@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$";
            var phonePattern = phoneAttr?.Pattern ?? @"^(\+?\d{1,3}[\s\-]?)?(\(?\d{2,4}\)?[\s\-]?)?[\d\s\-]{6,15}$";

            bool EmailIsValid(string v) => Regex.IsMatch(v ?? string.Empty, emailPattern);
            bool PhoneIsValid(string v) => Regex.IsMatch(v ?? string.Empty, phonePattern);

            var baselinePhone = "+1 (555) 123-4567";
            var baselineEmail = "user@example.com";

            var emailCases = new []
            {
                "user@example.com",
                "user.name+tag@example.co.uk",
                "u@ex.io",
                "first_last@example-domain.com",
                "user..dot@example.com",
                "user@sub_domain.com",
                "user@example",
                "@example.com",
                "user@example.c",
                "user@-example.com",
            };

            var phoneCases = new []
            {
                "0123456789",
                "+84 901 234 567",
                "(021) 234-5678",
                "123-456",
                "+1 (555) 123-4567",
                "++1 555 1234",
                "12",
                "abcdefghij",
                "+123 456 789 012 345 678 9",
                "+49-30-123456"
            };

            int counter = 1;

            // Test email variations with baseline phone
            foreach (var email in emailCases)
            {
                bool expectValid = EmailIsValid(email) && PhoneIsValid(baselinePhone);
                var name = $"Selenium EmailCase {counter:00}";
                var caseResult = SubmitCreateForm(driver, name, email, baselinePhone, expectValid, out bool observedValid, out string details);
                result.Cases.Add(new SeleniumCase
                {
                    Type = "email",
                    Name = name,
                    Email = email,
                    Phone = baselinePhone,
                    ExpectedValid = expectValid,
                    ObservedValid = observedValid,
                    Pass = expectValid == observedValid,
                    Details = details
                });

                // Cleanup created row if valid and observed valid
                if (observedValid)
                {
                    TryDeleteByName(driver, name.ToLowerInvariant());
                }
                counter++;
            }

            // Test phone variations with baseline email
            counter = 1;
            foreach (var phone in phoneCases)
            {
                bool expectValid = EmailIsValid(baselineEmail) && PhoneIsValid(phone);
                var name = $"Selenium PhoneCase {counter:00}";
                var caseResult = SubmitCreateForm(driver, name, baselineEmail, phone, expectValid, out bool observedValid, out string details);
                result.Cases.Add(new SeleniumCase
                {
                    Type = "phone",
                    Name = name,
                    Email = baselineEmail,
                    Phone = phone,
                    ExpectedValid = expectValid,
                    ObservedValid = observedValid,
                    Pass = expectValid == observedValid,
                    Details = details
                });

                if (observedValid)
                {
                    TryDeleteByName(driver, name.ToLowerInvariant());
                }
                counter++;
            }
        }

        private bool SubmitCreateForm(IWebDriver driver, string staffName, string email, string phone, bool expectValid, out bool observedValid, out string details)
        {
            details = string.Empty;
            observedValid = false;

            driver.Navigate().GoToUrl(_baseUrl + "/Staff/Create");
            Pause();

            // Fill fields
            var nameInput = driver.FindElement(By.Name("StaffName"));
            var emailInput = driver.FindElement(By.Name("Email"));
            var phoneInput = driver.FindElement(By.Name("PhoneNumber"));

            nameInput.Clear();
            nameInput.SendKeys(staffName);
            emailInput.Clear();
            emailInput.SendKeys(email);
            phoneInput.Clear();
            phoneInput.SendKeys(phone);

            // Submit
            try
            {
                var form = driver.FindElement(By.Id("staffForm"));
                form.Submit();
            }
            catch
            {
                // Fallback: click submit button
                try
                {
                    var submitBtn = driver.FindElement(By.CssSelector("#staffForm button[type='submit']"));
                    submitBtn.Click();
                }
                catch { }
            }

            // Observe result (fast): don't wait long on invalid cases
            Pause();
            var currentUrl = driver.Url;

            if (!currentUrl.Contains("/Staff/Create", StringComparison.OrdinalIgnoreCase))
            {
                // Assume success (redirect to Index)
                observedValid = true;
                details = "Redirected away from Create (treated as success)";
                return true;
            }
            else
            {
                // Still on Create – likely invalid; check for validation hints
                var html = driver.PageSource ?? string.Empty;
                if (html.Contains("invalid format", StringComparison.OrdinalIgnoreCase) ||
                    html.Contains("Please correct the validation errors", StringComparison.OrdinalIgnoreCase))
                {
                    observedValid = false;
                    details = "Validation errors present on Create page";
                    return true;
                }

                // Could be blocked by client-side validation
                observedValid = false;
                details = "Submission did not navigate; likely blocked by client validation";
                return true;
            }
        }

        private void TryDeleteByName(IWebDriver driver, string lowerName)
        {
            try
            {
                driver.Navigate().GoToUrl(_baseUrl + "/Staff");
                // Find row by data-name attribute (already lower-cased in view)
                var row = driver.FindElement(By.XPath($"//tr[contains(@class,'staff-row')][@data-name='{lowerName}']"));
                var deleteBtn = row.FindElement(By.CssSelector("button.btn-outline-danger"));
                deleteBtn.Click();

                // Handle confirm dialog
                try
                {
                    var alert = driver.SwitchTo().Alert();
                    alert.Accept();
                }
                catch { }

                Pause();
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        private void Pause()
        {
            if (_delayMs > 0)
            {
                System.Threading.Thread.Sleep(_delayMs);
            }
        }
    }

    public class SeleniumUiVerificationResult
    {
        public bool Success { get; set; }
        public List<string> Failures { get; set; } = new();
        public List<SeleniumCase> Cases { get; set; } = new();
    }

    public class SeleniumCase
    {
        public string Type { get; set; } = string.Empty; // email or phone
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool ExpectedValid { get; set; }
        public bool ObservedValid { get; set; }
        public bool Pass { get; set; }
        public string Details { get; set; } = string.Empty;
    }
}
                    
