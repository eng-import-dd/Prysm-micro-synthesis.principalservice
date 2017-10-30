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
        public const string InternalServerErrorGetUsers = "An error occurred retrieving the Users";
        public const string InternalServerErrorUpdateUser = "An error occurred updating the User";
        public const string InternalServerLockUser = "An error occurred Locking/Unlocking the User";
        public const string InternalServerErrorCreateMachine = "An error occurred while creating the Machine";
        public const string InternalServerErrorGetGuestUser = "An error occurred retriving the Guest Users";
        public const string InternalServerErrorUpdateMachine = "An error occurred updating the Machine";
        public const string InternalServerErrorGetMachine = "An error occurred retrieving the Machine";
        public const string InternalServerErrorResendWelcomeMail = "An error occurred while sending mail";

        public const string InternalServerErrorGetLicenseTypeForUser = "An error occurred while retrieving user license type. Faile to connect to License Service.";

        // Not found
        public const string NotFoundUser = "User Not Found";
        public const string NotFoundUsers = "Users Not Found";    
        public const string NotFoundMachine = "Machine Not Found";

        public const string NotFoundGroup = "Group Not Found";

        public const string UserGroupNotFound = "User Group Not Found";

        public const string TenantNotFound = "Tenant Not Found";

        //Promote Guest
        public const string PromotionFailed = "Failed to promote the user";
        public static string LicenseAssignmentFailed = "Failed to assign the license";

        //Idp User errors
        public const string IdpUserAutoProvisionError = "An error occurred during auto provision of Idp user.";

    }
}