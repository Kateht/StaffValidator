using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StaffValidator.Core.Repositories;
using StaffValidator.Core.Services;

namespace StaffValidator.Checker.Verifiers
{
    public class DataVerifier
    {
        private readonly string _dataPath;
        private readonly ILogger<HybridValidatorService> _logger;

        public DataVerifier(string dataPath, ILogger<HybridValidatorService> logger)
        {
            _dataPath = dataPath;
            _logger = logger;
        }

        public DataVerificationResult Verify()
        {
            var repo = new StaffRepository(_dataPath);
            var options = Options.Create(new HybridValidationOptions
            {
                RegexTimeoutMs = 200,
                MaxConcurrentRegexMatches = 4
            });

            var validator = new HybridValidatorService(options, _logger);
            var nfaEmail = AutomataFactory.BuildEmailNfa();
            var nfaPhone = AutomataFactory.BuildPhoneNfa();

            var result = new DataVerificationResult();

            foreach (var staff in repo.GetAll())
            {
                var (ok, errors) = validator.ValidateAll(staff);
                var nfaEmailOk = nfaEmail.Simulate(staff.Email);
                var nfaPhoneOk = nfaPhone.Simulate(staff.PhoneNumber);

                if (!ok || !nfaEmailOk || !nfaPhoneOk)
                {
                    result.Mismatches++;
                    var msg = $"[!] Staff {staff.StaffID} - {staff.StaffName} failed checks. ValidatorOk={ok}, NfaEmail={nfaEmailOk}, NfaPhone={nfaPhoneOk}";
                    result.Details.Add(msg);
                    
                    if (errors != null && errors.Count > 0)
                    {
                        foreach (var e in errors)
                        {
                            result.Details.Add("    - " + e);
                        }
                    }
                    
                    Console.WriteLine(msg);
                }
            }

            return result;
        }
    }

    public class DataVerificationResult
    {
        public int Mismatches { get; set; }
        public List<string> Details { get; set; } = new();
    }
}
