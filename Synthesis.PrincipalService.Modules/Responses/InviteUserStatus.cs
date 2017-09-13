namespace Synthesis.PrincipalService.Responses
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
