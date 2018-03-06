using System;

namespace Synthesis.PrincipalService.Responses
{
    //Todo : Remove this class while integrating with email internal api
    public class UserEmailResponse
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public DateTime? LastInvitedDate { get; set; }
        public string LastName { get; set; }

        public static UserEmailResponse Example()
        {
            return new UserEmailResponse
            {
                Email = "example@email.com",
                FirstName = "ExampleFirstname",
                LastName = "ExampleLastname",
                LastInvitedDate = DateTime.UtcNow,
            };
        }
    }
}
