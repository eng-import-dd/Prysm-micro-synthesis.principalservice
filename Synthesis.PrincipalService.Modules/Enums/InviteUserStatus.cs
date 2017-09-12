namespace Synthesis.PrincipalService.Enums
{
    public enum InviteUserStatus
    {
        Success,
        UserEmailFormatInvalid,
        UserEmailDomainFree,
        UserEmailNotDomainAllowed,
        DuplicateUserEmail,
        DuplicateUserEntry
    }
}
