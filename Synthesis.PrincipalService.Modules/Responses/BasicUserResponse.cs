using System;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Responses
{
    public class BasicUserResponse
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public DateTime? LastLogin { get; set; }

        public string UserName { get; set; }

        public bool IsLocked { get; set; }

        public int? PasswordAttempts { get; set; }

        public string LdapId { get; set; }

        public bool? IsIdpUser { get; set; }

    }
}
