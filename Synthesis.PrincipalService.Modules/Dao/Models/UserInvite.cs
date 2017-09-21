using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Synthesis.License.Manager.Models;

namespace Synthesis.PrincipalService.Dao.Models
{
    [DataContract]
    public class UserInvite
    {
        [DataMember]
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        [DataMember]
        public string FirstName { get; set; }

        [DataMember]
        public string LastName { get; set; }

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public Guid TenantId { get; set; }

        [DataMember]
        public DateTime LastInvitedDate { get; set; }
    }
}