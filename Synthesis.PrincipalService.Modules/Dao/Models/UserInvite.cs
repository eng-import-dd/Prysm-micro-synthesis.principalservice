using System;
using Newtonsoft.Json;
using Synthesis.License.Manager.Models;

namespace Synthesis.PrincipalService.Dao.Models
{
    public class UserInvite
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public Guid TenantId { get; set; }
        public DateTime LastInvitedDate { get; set; }
    }
}