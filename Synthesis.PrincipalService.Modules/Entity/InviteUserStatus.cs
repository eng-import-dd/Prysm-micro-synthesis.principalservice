namespace Synthesis.PrincipalService.Entity
{
    public enum InviteUserStatus
    {
        Success,
        UserEmailFormatInvalid,
        UserEmailDomainFree,
        UserEmailNotDomainAllowed,
        DuplicateUserEmail,
        DuplicateUserEntry,
        UserNotExist
    }
}
