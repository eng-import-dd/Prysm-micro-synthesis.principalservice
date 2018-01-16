namespace Synthesis.PrincipalService.Requests
{
    public class GuestVerificationEmailRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ProjectAccessCode { get; set; }
    }
}
