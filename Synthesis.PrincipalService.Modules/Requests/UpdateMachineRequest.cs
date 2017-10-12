using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Requests
{
    public class UpdateMachineRequest
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public string MachineKey { get; set; }

        public string Location { get; set; }

        public Guid SettingProfileId { get; set; }

        public DateTime DateModified { get; set; }

        public Guid ModifiedBy { get; set; }
    }
}
