using System;
using System.Linq;
using Newtonsoft.Json;
using Synthesis.License.Manager.Models;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Responses
{
    public class UserResponse
    {
        public string FullName => $"{FirstName} {LastName}";
        public string Initials => $"{FirstName?.ToUpper().FirstOrDefault()}{LastName?.ToUpper().FirstOrDefault()}";
        public Guid? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }

        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public bool? IsIdpUser { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastAccessDate { get; set; }
        public string LastName { get; set; }
        public string LdapId { get; set; }
        public LicenseType? LicenseType { get; set; }
        public string UserName { get; set; }

        public static UserResponse Example()
        {
            return new UserResponse
            {
                CreatedBy = Guid.NewGuid(),
                CreatedDate = DateTime.UtcNow,
                Email = "example@email.com",
                FirstName = "ExampleFirstname",
                Id = Guid.NewGuid(),
                IsIdpUser = false,
                IsLocked = false,
                LastAccessDate = DateTime.UtcNow,
                LastName = "ExampleLastname",
                LdapId = "ExampleLdapId",
                LicenseType = InternalApi.Models.LicenseType.Default,
                UserName = "ExampleUsername"
            };
        }
    }
}