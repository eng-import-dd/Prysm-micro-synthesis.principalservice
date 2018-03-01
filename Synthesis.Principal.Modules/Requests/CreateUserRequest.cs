using System;
using Newtonsoft.Json;
using Synthesis.License.Manager.Models;
using Synthesis.PrincipalService.InternalApi.Models;

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
        public Guid TenantId { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string PasswordConfirmation { get; set; }
        public string ProjectAccessCode { get; set; }

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
                LdapId = "ExampleLdapId",
                IsIdpUser = false,
                LicenseType = InternalApi.Models.LicenseType.Default
            };
        }

        public static CreateUserRequest GuestExample()
        {
            return new CreateUserRequest
            {
                FirstName = "ExampleFirstname",
                LastName = "ExampleLastname",
                Email = "example@email.com",
                LdapId = "ExampleLdapId",
                Password = "SecurePassword123",
                PasswordConfirmation = "SecurePassword123",
                IsIdpUser = false,
                ProjectAccessCode = "1234567890",
                LicenseType = InternalApi.Models.LicenseType.Default
            };
        }
    }
}