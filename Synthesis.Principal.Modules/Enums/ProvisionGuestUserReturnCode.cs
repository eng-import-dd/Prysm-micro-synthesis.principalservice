namespace Synthesis.PrincipalService.Enums
{
    public enum ProvisionGuestUserReturnCode
    {
        Success,
        SucessEmailVerificationNeeded,
        Failed,
        EmailIsNotUnique,
        UsernameIsNotUnique
    }
}
