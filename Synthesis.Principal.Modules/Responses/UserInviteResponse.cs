using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Synthesis.PrincipalService.Responses
{
    public class UserInviteResponse
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public DateTime? LastInvitedDate { get; set; }
        public string LastName { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public InviteUserStatus Status { get; set; }

        public Guid TenantId { get; set; }

        public static UserInviteResponse Example()
        {
            return new UserInviteResponse
            {
                Email = "example@email.com",
                FirstName = "ExampleFirstname",
                LastName = "ExampleLastname",
                TenantId = Guid.NewGuid(),
                LastInvitedDate = DateTime.UtcNow,
                Status = InviteUserStatus.Success
            };
        }
    }
}