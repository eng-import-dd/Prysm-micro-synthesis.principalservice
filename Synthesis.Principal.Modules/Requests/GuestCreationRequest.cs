namespace Synthesis.PrincipalService.Requests
{
    public class GuestCreationRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string PasswordConfirmation { get; set; }
        public string ProjectAccessCode { get; set; }
        public bool IsIdpUser { get; set; }

        public static GuestCreationRequest Example()
        {
            return new GuestCreationRequest
            {
                FirstName = "ExampleFirstname",
                LastName = "ExampleLastname",
                Email = "example@email.com",
                Password = "SecurePassword123",
                PasswordConfirmation = "SecurePassword123",
                ProjectAccessCode = "1234",
                IsIdpUser = false
            };
        }
    }
}
