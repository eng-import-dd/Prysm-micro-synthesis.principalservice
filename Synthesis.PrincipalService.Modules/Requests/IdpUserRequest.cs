using System;
using System.Collections.Generic;

namespace Synthesis.PrincipalService.Requests
{
    public class IdpUserRequest
    {
        public Guid? UserId { get; set; }

        public string EmailId { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public List<string> Groups { get; set; }

        public Guid TenantId { get; set; }

        public bool IsGuestUser { get; set; }

        public List<string> IdpMappedGroups { get; set; }
    }
}
