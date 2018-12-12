namespace Synthesis.PrincipalService.Constants
{
    public class ErrorMessages
    {
        public const string UserPromotionFailed = "An error occurred promoting the user {0}";
        public const string UserExists = "Could not create a guest user because a user with the same email address already exists.";
        public const string UserNotInvited = "Could not create a guest user because the user has not been invited yet.";
        public const string TenantMappingFailed = "Could not create user because adding user to tenant failed";
        public const string SetPasswordFailed = "Could not create user because setting the user's password failed";
        public const string EmailAlreadyVerified = "Did not send email because the email address is already verified";
        public const string EmailRecentlySent = "Did not send email becuase it was already sent recently";
        public const string SendEmailFailed = "An error occurred while tryign to send an email";
        public const string MaxTeamSizeExceeded = "Could not add user to the team as maximum team size is reached";
    }
}
