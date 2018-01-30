namespace Synthesis.PrincipalService.Constants
{
    public class ErrorMessages
    {
        public const string UserPromotionFailed = "An error occurred promoting the user {0}";
        public const string UserExists = "Could not create a guest user because a user with the same email address already exists.";
        public const string UserNotInvited = "Could not create a guest user because the user has not been invited yet.";
    }
}
