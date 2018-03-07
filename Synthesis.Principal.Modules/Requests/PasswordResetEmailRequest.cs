namespace Synthesis.PrincipalService.Requests
{
    //Todo : Remove this class while integrating with email internal api
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