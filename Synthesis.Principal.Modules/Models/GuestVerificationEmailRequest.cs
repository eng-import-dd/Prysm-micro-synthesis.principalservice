namespace Synthesis.PrincipalService.Models
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
                Email = "abc@xyz.com",
                FirstName = "First Name",
                LastName = "Last Name"
            };
        }
    }
}
