namespace Synthesis.License.Manager.Models
{
    /// <summary>
    /// Details the reason server activation is not in compliance
    /// </summary>
    public enum LicenseViolationCode
    {
        FunctioningWithinNormalParameters,
        ServerLicenseNotFound,
        LicenseExpiringSoon,
        ClockWindbackDetected,
        UnableToObtainEntitlementsFromFNO
    }
}