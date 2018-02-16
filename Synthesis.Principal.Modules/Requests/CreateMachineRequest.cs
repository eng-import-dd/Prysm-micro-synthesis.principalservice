using System;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Requests
{
    public class CreateMachineRequest
    {
        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        public bool IsSelected { get; set; }
        public DateTime? LastOnline { get; set; }
        public string Location { get; set; }
        public string MachineKey { get; set; }
        public Guid ModifiedBy { get; set; }
        public Guid SettingProfileId { get; set; }
        public string SettingProfileName { get; set; }
        public string SynthesisVersion { get; set; }
        public Guid TenantId { get; set; }

        public static CreateMachineRequest Example()
        {
            return new CreateMachineRequest
            {
                Id = Guid.NewGuid(),
                MachineKey = "ExampleMachineKey",
                Location = "ExampleLocation",
                SettingProfileId = Guid.NewGuid(),
                SettingProfileName = "ExampleProfileName",
                IsSelected = false,
                TenantId = Guid.NewGuid(),
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                ModifiedBy = Guid.NewGuid(),
                SynthesisVersion = "ExampleVersion",
                LastOnline = DateTime.UtcNow
            };
        }
    }
}