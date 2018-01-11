namespace Synthesis.PrincipalService.Requests
{
    public class PasswordResetEmailRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string Link { get; set; }

        public static PasswordResetEmailRequest Example()
        {
            return new PasswordResetEmailRequest
            {
                Email = "example@email.com",
                FirstName = "ExampleFirstname",
                Link ="http://app.prysm.com/"
            };
        }
    }
}