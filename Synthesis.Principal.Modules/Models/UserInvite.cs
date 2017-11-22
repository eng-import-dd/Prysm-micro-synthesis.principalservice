using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Models
{
    [DataContract]
    public class UserInvite
    {
        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string FirstName { get; set; }

        [DataMember]
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        [DataMember]
        public DateTime LastInvitedDate { get; set; }

        [DataMember]
        public string LastName { get; set; }

        [DataMember]
        public Guid TenantId { get; set; }

        public static UserInvite Example()
        {
            return new UserInvite
            {
                Email = "Example@email.com",
                FirstName = "ExampleFirstname",
                Id = Guid.NewGuid(),
                LastInvitedDate = DateTime.UtcNow,
                LastName = "ExampleLastname",
                TenantId = Guid.NewGuid()
            };
        }
    }
}