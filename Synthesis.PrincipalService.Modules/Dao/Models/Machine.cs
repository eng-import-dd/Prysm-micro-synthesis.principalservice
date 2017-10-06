using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.Hadoop.Avro;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Dao.Models
{
    [DataContract]
    public class Machine
    {
        [DataMember]
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [DataMember]
        public string MachineKey { get; set; }

        [DataMember]
        public string Location { get; set; }

        [DataMember]
        [NullableSchema]
        public Guid? SettingProfileId { get; set; }

        [DataMember]
        public Guid TenantId { get; set; }

        [DataMember]
        public DateTime DateCreated { get; set; }

        [DataMember]
        public DateTime DateModified { get; set; }

        [DataMember]
        public Guid ModifiedBy { get; set; }

        [DataMember]
        public string SynthesisVersion { get; set; }

        [DataMember]
        public DateTime? LastOnline { get; set; }
    }
}
