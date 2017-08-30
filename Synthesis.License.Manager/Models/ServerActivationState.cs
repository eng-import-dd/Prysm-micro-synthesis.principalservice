namespace Synthesis.License.Manager.Models
{
    /// <summary>
    /// Represents the activation state of the server
    /// </summary>
    public enum ServerActivationState
    {
        /// <summary>
        /// Server is in compliance and server should be have normally
        /// </summary>
        Normal,
        /// <summary>
        /// Server is not in compliance - server should be have normally and warn customer but lockdown will eventually trigger
        /// </summary>
        PendingLockdown,
        /// <summary>
        /// No logins are permitted and user licenses cannot be assigned
        /// </summary>
        Lockdown
    }
}