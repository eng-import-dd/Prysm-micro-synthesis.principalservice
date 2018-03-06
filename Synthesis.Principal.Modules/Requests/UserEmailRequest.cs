namespace Synthesis.PrincipalService.Requests
{
    //Todo : Remove this class while integrating with email internal api
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
