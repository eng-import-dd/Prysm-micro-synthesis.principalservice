using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.License.Manager
{
    public enum ResultCode
    {
        Failed = 0,
        Success = 1,
        RedisConnectionFailed = 1000,
        RedisDataTypeMismatch = 1001,
        RecordNotFound = 1002,
        ArgumentNull = 1003,
        InsufficientPermissions = 1004,
        Unauthorized = 1005,
        AccessDeniedToSystemSetting = 1007,
        InvalidClientCertificate = 1008,
        ValidClientCertificate = 1009
    }
}
