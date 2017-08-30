using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Synthesis.License.Manager.Models
{
    [Serializable]
    [DataContract]
    public sealed class LicenseDTO 
    {
        [DataMember]
        [Required]
        public Guid AccountId { get; set; }

        [DataMember]
        [Required]
        public string LicenseName { get; set; }       

        [DataMember]
        public DateTime? Expiration { get; set; }

        [DataMember]
        public long FeatureCount { get; set; } 

        [DataMember]
        public string Version { get; set; }

        [DataMember]
        public bool Perpetual { get; set; }

        [DataMember]
        public bool UnCounted { get; set; }

        [DataMember]
        public string SerialNumber { get; set; }

        [DataMember]
        public DateTime? StartDate { get; set; }

        [DataMember]
        public string VendorString { get; set; }

        [DataMember]
        public string Notice { get; set; }


        //[DataMember]
        //[Obsolete("Not apprpriate for the license, UserId is an assignment attribute")]
        //public string UserId { get; set; }

        [DataMember]
        [Obsolete("Obtained from license summary")]
        public long TotalPurchased { get; set; }

        [DataMember]
        [Obsolete("Obtained from license summary")]
        public long TotalAllocated { get; set; }

        [DataMember]
        [Obsolete("Obtained from license summary")]
        public long TotalAvailable { get; set; }
    }
}
