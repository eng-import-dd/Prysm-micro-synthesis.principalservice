using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.License.Manager.Models
{
    [Serializable]
    [DataContract]
    public class LicenseResponse
    {
        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public LicenseResponseResultCode ResultCode { get; set; }

        [DataMember]
        public List<LicenseDto> Licenses { get; set; }
        
        public DateTime? PurchasesLastUpdated { get; set; }

        [DataMember]
        public string UserId { get; set; }

        [DataMember]
        public string AccountId { get; set; }
    }

    public enum LicenseResponseResultCode
    {
        Success,
        Failed,
        LicenseExpired,
        LicenseDoesNotExist,
        AccountCannotBeLicensed,
        NonLicensedUser
    }
}
