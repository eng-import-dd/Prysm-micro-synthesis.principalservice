namespace Synthesis.PrincipalService.Enums
{
    public enum CreateGuestResponseCode
    {
        Failed,
        Unauthorized,
        FirstOrLastNameIsNull,
        EmailIsNotUnique,
        InvalidEmail,
        UserExists,
        UsernameIsNotUnique,
        InvalidPassword,
        PasswordConfirmationError,
        SucessEmailVerificationNeeded,
        Success
    }
}
