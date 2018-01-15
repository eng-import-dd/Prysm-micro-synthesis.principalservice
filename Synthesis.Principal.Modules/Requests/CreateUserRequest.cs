using System;
using Newtonsoft.Json;
using Synthesis.License.Manager.Models;

namespace Synthesis.PrincipalService.Requests
{
    public class CreateUserRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }

        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public bool? IsIdpUser { get; set; }
        public string LastName { get; set; }
        public string LdapId { get; set; }
        public LicenseType? LicenseType { get; set; }
        public string Password { get; set; }
        public Guid TenantId { get; set; }
        public string UserName { get; set; }

        public static CreateUserRequest Example()
        {
            return new CreateUserRequest
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                FirstName = "ExampleFirstname",
                LastName = "ExampleLastname",
                Email = "example@email.com",
                UserName = "ExampleUsername",
                Password = "SomeSecurePassword1234",
                LdapId = "ExampleLdapId",
                IsIdpUser = false,
                LicenseType = License.Manager.Models.LicenseType.Default
            };
        }
    }
}