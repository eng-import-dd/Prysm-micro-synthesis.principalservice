namespace Synthesis.License.Manager.Models
{
    /// <summary>
    /// Represents the http response that describes the current activation state of the license server
    /// </summary>
    public class ServerActivationStateResponse
    {
        /// <summary>
        /// Unlocked, Lockdown or about shut down
        /// </summary>
        public ServerActivationState ActivationState { get; set; }

        /// <summary>
        /// Reason for lockdown
        /// </summary>
        public LicenseViolationCode Violation { get; set; }

        /// <summary>
        /// Number of days the server will continue to authorize licenses before lockdown
        /// </summary>
        public int DaysTillLockdown { get; set; }
    }
}