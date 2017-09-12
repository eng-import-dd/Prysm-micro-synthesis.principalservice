using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Synthesis.PrincipalService.Enums;

namespace Synthesis.PrincipalService.Responses
{
    public class UserInviteResponse
    {
        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public Guid TenantId { get; set; }

        public DateTime? LastInvitedDate { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public InviteUserStatus Status { get; set; }
    }
}
