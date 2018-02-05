namespace Synthesis.PrincipalService.Enums
{
    public enum CreateGuestResponseCode
    {
        Failed,
        Unauthorized,
        FirstOrLastNameIsNull,
        InvalidEmail,
        UserExists,
        InvalidPassword,
        PasswordConfirmationError,
        SucessEmailVerificationNeeded,
        UserNotInvited,
        Success
    }
}
