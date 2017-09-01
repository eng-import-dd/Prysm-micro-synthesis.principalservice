namespace Synthesis.License.Manager.Models
{
    /// <summary>
    /// Represents the whether the attempt to activate the server was successful
    /// </summary>
    public enum ActivationResult
    {
        /// <summary>
        /// The server was successuflly activated
        /// </summary>
        Success,

        /// <summary>
        /// The activation id was invalid
        /// </summary>
        InvalidActivationId,

        /// <summary>
        /// The activation license file was invalid
        /// </summary>
        InvalidActivationFile,

        /// <summary>
        /// The license file was already processed
        /// </summary>
        LicenseFileAlreadyProcesssed
    }
}