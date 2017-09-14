using Synthesis.License.Manager.Models;
using System;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Requests
{
    public class CreateUserRequest
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public Guid TenantId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }
        
        public string UserName { get; set; }

        public string PasswordHash { get; set; }

        public string PasswordSalt { get; set; }

        public string LdapId { get; set; }

        public bool? IsIdpUser { get; set; }

        public bool IsLocked { get; set; }

        public LicenseType? LicenseType { get; set; }

        public int? PasswordAttempts { get; set; }
    }
}
