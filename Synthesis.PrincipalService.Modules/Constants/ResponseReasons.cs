namespace Synthesis.PrincipalService.Constants
{
    public enum DocumentDbCodes
    {
        NotFound
    }

    public class ResponseReasons
    {
        // Internal server errors
        public const string InternalServerErrorCreateUser = "An error occurred while creating the User";

        public const string InternalServerErrorDeleteUser = "An error occurred deleting the User";
        public const string InternalServerErrorGetUser = "An error occurred retrieving the User";
        public const string InternalServerErrorGetUsers = "An error occurred retrieving the User";
        public const string InternalServerErrorUpdateUser = "An error occurred updating the User";

        // Not found
        public const string NotFoundUser = "User Not Found";
    }
}