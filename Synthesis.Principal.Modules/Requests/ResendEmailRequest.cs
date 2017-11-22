namespace Synthesis.PrincipalService.Requests
{
    public class ResendEmailRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }

        public static ResendEmailRequest Example()
        {
            return new ResendEmailRequest
            {
                Email = "example@email.com",
                FirstName = "ExampleFirstname"
            };
        }
    }
}