using System;
using System.Runtime.Serialization;
using Microsoft.Hadoop.Avro;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Models
{
    [DataContract]
    public class Machine
    {
        [DataMember]
        public DateTime DateCreated { get; set; }

        [DataMember]
        public DateTime DateModified { get; set; }

        [DataMember]
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [DataMember]
        public DateTime? LastOnline { get; set; }

        [DataMember]
        public string Location { get; set; }

        [DataMember]
        public string MachineKey { get; set; }

        [DataMember]
        public Guid ModifiedBy { get; set; }

        [DataMember]
        [NullableSchema]
        public Guid? SettingProfileId { get; set; }

        [DataMember]
        public string SynthesisVersion { get; set; }

        [DataMember]
        public Guid TenantId { get; set; }

        public static Machine Example()
        {
            return new Machine();
        }
    }
}