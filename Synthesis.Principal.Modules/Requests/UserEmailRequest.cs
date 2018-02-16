namespace Synthesis.PrincipalService.Requests
{
    public class UserEmailRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public static UserEmailRequest Example()
        {
            return new UserEmailRequest
            {
                Email = "some@email.com",
                FirstName = "First",
                LastName = "Last"
            };
        }
    }
}
