using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Synthesis.License.Manager.Models;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Requests
{
    public class PromoteGuestRequest
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public LicenseType LicenseType { get; set; }

        public Guid UserId { get; set; }

        public static PromoteGuestRequest Example()
        {
            return new PromoteGuestRequest
            {
                LicenseType = LicenseType.Default,
                UserId = Guid.NewGuid()
            };
        }
    }
}