using System;
using System.Collections.Generic;

namespace Synthesis.PrincipalService.Requests
{
    public class IdpUserRequest
    {
        public string EmailId { get; set; }
        public string FirstName { get; set; }
        public List<string> Groups { get; set; }
        public List<string> IdpMappedGroups { get; set; }
        public bool IsGuestUser { get; set; }
        public string LastName { get; set; }
        public Guid TenantId { get; set; }
        public Guid? UserId { get; set; }

        public static IdpUserRequest Example()
        {
            return new IdpUserRequest
            {
                UserId = Guid.NewGuid(),
                EmailId = "ExampleEmailId",
                FirstName = "ExampleFirstname",
                LastName = "ExampleLastname",
                Groups = new List<string> { "ExampleGroup" },
                TenantId = Guid.NewGuid(),
                IsGuestUser = false,
                IdpMappedGroups = new List<string> { "ExampleGroup" }
            };
        }
    }
}