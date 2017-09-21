using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Synthesis.License.Manager.Models;

namespace Synthesis.PrincipalService.Requests
{
    public class PromoteGuestRequest
    {
        public Guid UserId { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public LicenseType LicenseType { get; set; }
    }
}
