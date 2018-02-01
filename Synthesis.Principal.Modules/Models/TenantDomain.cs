using Microsoft.Hadoop.Avro;
using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace Synthesis.PrincipalService.Models
{
    public class TenantDomain
    {
        [DataMember(Name = "CreatedDate"), NullableSchema]
        public DateTime? CreatedDate { get; set; }

        [DataMember(Name = "Domain"), NullableSchema]
        public string Domain { get; set; }

        [JsonProperty("id")]
        [DataMember(Name = "Id"), NullableSchema]
        public Guid? Id { get; set; }

        [DataMember(Name = "LastAccessDate"), NullableSchema]
        public DateTime? LastAccessDate { get; set; }

        [DataMember(Name = "TenantId"), NullableSchema]
        public Guid TenantId { get; set; }
    }
}
