using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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

        [JsonProperty("is_guest")]
        public bool IsGuestUser { get; set; }

        [JsonProperty("idp_mapped_groups")]
        public List<string> IdpMappedGroups { get; set; }
    }
}
