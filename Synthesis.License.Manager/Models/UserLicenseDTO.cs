﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Synthesis.License.Manager.Models
{
    [Serializable]
    [DataContract]
    public sealed class UserLicenseDto
    {
        [DataMember]
        [Required]
        public string UserId { get; set; }

        [DataMember]
        [Required]
        public string AccountId { get; set; }

        [DataMember]
        public string Version { get; set; }

        [DataMember]
        public string LicenseType { get; set; }

        [DataMember]
        public DateTime? Expiration { get; set; }
    }
}
