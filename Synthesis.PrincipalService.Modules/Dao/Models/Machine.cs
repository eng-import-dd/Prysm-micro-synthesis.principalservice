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
        public Guid MachineId { get; set; }

        [DataMember]
        public string MachineKey { get; set; }

        [DataMember]
        public string Location { get; set; }

        [DataMember]
        public Guid? SettingProfileId { get; set; }

        [DataMember]
        public string SettingProfileName { get; set; }

        [DataMember]
        public bool IsSelected { get; set; }

        [DataMember]
        public Guid AccountId { get; set; }

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

        [DataMember]
        public bool CurrentlyOnline { get; set; }

        [DataMember]
        public TimeSpan AveragePingTime { get; set; }

        [Obsolete("Must keep because 2.3/2.4/2.5 clients have a hard reference")]
        [DataMember]
        public bool? IsUserLicenseRequired { get; set; }
    }
}
