namespace Synthesis.PrincipalService.Requests
{
    public class GuestVerificationEmailRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ProjectAccessCode { get; set; }

        public static GuestVerificationEmailRequest Example()
        {
            return new GuestVerificationEmailRequest
            {
                FirstName = "ExampleFirstname",
                LastName = "ExampleLastname",
                Email = "example@email.com",
                ProjectAccessCode = "1234567890",
            };
        }
    }
}
