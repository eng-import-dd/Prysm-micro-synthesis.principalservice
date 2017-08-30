using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Synthesis.License.Manager.Models
{
    [Serializable]
    [DataContract]
    public class LicenseSummaryDTO 
    {
        [DataMember]
        [Required]
        public string LicenseName { get; set; }

        [DataMember]
        public string Version { get; set; }

        [DataMember]
        public long TotalPurchased { get; set; }

        [DataMember]
        public long TotalAllocated { get; set; }

        [DataMember]
        public long TotalAvailable { get; set; }
    }
}
