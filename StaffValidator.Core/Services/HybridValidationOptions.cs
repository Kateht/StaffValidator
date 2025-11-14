namespace StaffValidator.Core.Services
{
    /// Options used to configure the HybridValidatorService.
    /// Bound from configuration section "HybridValidation".
    public class HybridValidationOptions
    {
        /// Timeout in milliseconds to apply to Regex matches before falling back to automata.
        /// Defaults to 200 ms.
        public int RegexTimeoutMs { get; set; } = 200;
        /// Maximum number of concurrent regex match operations allowed. If exceeded, the
        /// validator will fall back to automata for safety instead of queuing additional regex work.
        /// Defaults to 4.
        public int MaxConcurrentRegexMatches { get; set; } = 4;
        /// Enable or disable DFA fallback when regex times out or fails.
        /// Defaults to true.
        public bool EnableDfaFallback { get; set; } = true;
    }
}
