namespace Synthesis.PrincipalService.Constants
{
    public enum DocumentDbCodes
    {
        NotFound
    }

    public static class ResponseReasons
    {
        // Internal server errors
        public const string InternalServerErrorCreateUser = "An error occurred while creating the User";

        public const string InternalServerErrorDeleteUser = "An error occurred deleting the User";
        public const string InternalServerErrorGetUser = "An error occurred retrieving the User";
        public const string InternalServerErrorGetUsers = "An error occurred retrieving the User";
        public const string InternalServerErrorUpdateUser = "An error occurred updating the User";
        public const string InternalServerErrorCreateMachine = "An error occurred while creating the Machine";

        // Not found
        public const string NotFoundUser = "User Not Found";

        //Promote Guest
        public const string PromotionFailed = "Failed to promote the user";
        public static string LicenseAssignmentFailed = "Failed to assign the license";

    }
}