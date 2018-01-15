using System;
using System.Runtime.Serialization;
using Microsoft.Hadoop.Avro;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Models
{
    [DataContract]
    public class Project
    {
        [JsonProperty("id")]
        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        [NullableSchema]
        public Guid? OwnerId { get; set; }

        [DataMember]
        [NullableSchema]
        public Guid? TenantId { get; set; }

        [DataMember]
        [NullableSchema]
        public bool? IsGuestModeEnabled { get; set; }
    }
}
