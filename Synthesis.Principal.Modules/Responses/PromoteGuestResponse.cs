using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Synthesis.PrincipalService.Responses
{
    public class PromoteGuestResponse
    {
        public string Message { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public PromoteGuestResultCode ResultCode { get; set; }

        public Guid? UserId { get; set; }

        public static PromoteGuestResponse Example()
        {
            return new PromoteGuestResponse
            {
                Message = "ExampleMessage",
                ResultCode = PromoteGuestResultCode.Success,
                UserId = Guid.NewGuid()
            };
        }
    }

    public enum PromoteGuestResultCode
    {
        Failed = 0,
        Success = 1,
        UserAlreadyPromoted = 2,
        FailedToAssignLicense = 3
    }
}