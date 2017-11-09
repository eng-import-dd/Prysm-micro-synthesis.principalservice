using Newtonsoft.Json;
using Synthesis.License.Manager.Models;
using System;
using System.Linq;
using Nancy;
using Newtonsoft.Json.Converters;

namespace Synthesis.PrincipalService.Responses
{
    public class UserResponse
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public Guid TenantId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public string UserName { get; set; }

        public bool IsLocked { get; set; }
        
        public string LdapId { get; set; }

        public bool? IsIdpUser { get; set; }

        public LicenseType? LicenseType { get; set; }
        
        public Guid? CreatedBy { get; set; }

        public DateTime? CreatedDate { get; set; }

        public DateTime? LastAccessDate { get; set; }

        public string FullName => $"{FirstName} {LastName}";

        public string Initials => $"{FirstName?.ToUpper().FirstOrDefault()}{LastName?.ToUpper().FirstOrDefault()}";
    }
}
