using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Synthesis.License.Manager.Models;

namespace Synthesis.License.Manager.Models
{
    /// <summary>
    /// Bulk user license assignment transfer object
    /// </summary>
    [Serializable]
    [DataContract]
    public class BulkLicenseDTO
    {
        [DataMember]
        [Required]
        public Guid AccountId { get; set; }

        [DataMember]
        [Required]
        public List<Guid> UserIdList;

        [DataMember]
        [Required]              
        public LicenseType licenseType;
    }
}
