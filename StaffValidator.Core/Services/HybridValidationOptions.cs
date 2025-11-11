namespace StaffValidator.Core.Services
{
    /// <summary>
    /// Options used to configure the HybridValidatorService.
    /// Bound from configuration section "HybridValidation".
    /// </summary>
    public class HybridValidationOptions
    {
        /// <summary>
        /// Timeout in milliseconds to apply to Regex matches before falling back to automata.
        /// Defaults to 200 ms.
        /// </summary>
        public int RegexTimeoutMs { get; set; } = 200;
        /// <summary>
        /// Maximum number of concurrent regex match operations allowed. If exceeded, the
        /// validator will fall back to automata for safety instead of queuing additional regex work.
        /// Defaults to 4.
        /// </summary>
        public int MaxConcurrentRegexMatches { get; set; } = 4;
    }
}
