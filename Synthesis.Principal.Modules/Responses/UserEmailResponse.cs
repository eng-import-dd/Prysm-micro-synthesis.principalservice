using System;

namespace Synthesis.PrincipalService.Responses
{
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
