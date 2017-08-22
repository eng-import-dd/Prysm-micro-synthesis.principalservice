namespace Synthesis.PrincipalService.Constants
{
    public enum DocumentDbCodes
    {
        NotFound
    }

    public class ResponseReasons
    {
        // Internal server errors
        public const string InternalServerErrorCreatePrincipalservice = "An error occurred while creating the Principalservice";

        public const string InternalServerErrorDeletePrincipalservice = "An error occurred deleting the Principalservice";
        public const string InternalServerErrorGetPrincipalservice = "An error occurred retrieving the Principalservice";
        public const string InternalServerErrorGetPrincipalservices = "An error occurred retrieving the Principalservice";
        public const string InternalServerErrorUpdatePrincipalservice = "An error occurred updating the Principalservice";

        // Not found
        public const string NotFoundPrincipalservice = "Principalservice Not Found";
    }
}