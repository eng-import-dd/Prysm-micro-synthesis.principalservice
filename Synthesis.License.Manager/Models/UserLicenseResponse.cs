using System;
using System.Collections.Generic;
using System.Linq;

namespace Synthesis.License.Manager.Models
{
    public class UserLicenseResponse
    {
        public string Message { get; set; }
        public string AccountId { get; set; }
        public UserLicenseResponseResultCode ResultCode { get; set; }
        public List<UserLicenseDto> LicenseAssignments { get; set; }
    }

    public enum UserLicenseResponseResultCode
    {
        Success,
        Failed,
        NoLicensedUsers,
        UserLicenseNotAvailable
    }
}
