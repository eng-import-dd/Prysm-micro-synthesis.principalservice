using System;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Requests
{
    public class UpdateMachineRequest
    {
        public DateTime DateModified { get; set; }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        public string Location { get; set; }
        public string MachineKey { get; set; }
        public Guid ModifiedBy { get; set; }
        public Guid SettingProfileId { get; set; }
        public Guid TenantId { get; set; }

        public static UpdateMachineRequest Example()
        {
            return new UpdateMachineRequest
            {
                DateModified = DateTime.UtcNow,
                Id = Guid.NewGuid(),
                Location = "ExampleLocation",
                MachineKey = "ExampleMachineKey",
                ModifiedBy = Guid.NewGuid(),
                SettingProfileId = Guid.NewGuid(),
                TenantId = Guid.NewGuid()
            };
        }
    }
}