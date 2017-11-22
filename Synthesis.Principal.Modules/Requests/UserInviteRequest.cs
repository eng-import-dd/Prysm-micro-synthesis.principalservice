namespace Synthesis.PrincipalService.Requests
{
    public class UserInviteRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public static UserInviteRequest Example()
        {
            return new UserInviteRequest
            {
                Email = "some@email.com",
                FirstName = "First",
                LastName = "Last"
            };
        }
    }
}