using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Synthesis.PrincipalService.Responses
{
    public class PromoteGuestResponse
    {
        public Guid? UserId { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public PromoteGuestResultCode ResultCode { get; set; }

        public string Message { get; set; }
    }

    public enum PromoteGuestResultCode
    {
        Failed = 0,
        Success = 1,
        UserAlreadyPromoted = 2,
        FailedToAssignLicense = 3
    }
}
