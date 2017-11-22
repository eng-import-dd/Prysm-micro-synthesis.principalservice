using System;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Responses
{
    public class MachineResponse
    {
        public TimeSpan AveragePingTime { get; set; }
        public bool CurrentlyOnline { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        public bool IsSelected { get; set; }
        public DateTime? LastOnline { get; set; }
        public string Location { get; set; }
        public string MachineKey { get; set; }
        public Guid ModifiedBy { get; set; }
        public Guid? SettingProfileId { get; set; }
        public string SynthesisVersion { get; set; }
        public Guid TenantId { get; set; }

        public static MachineResponse Example()
        {
            return new MachineResponse
            {
                Id = Guid.NewGuid(),
                MachineKey = "ExampleMachineKey",
                Location = "ExampleLocation",
                SettingProfileId = Guid.NewGuid(),
                IsSelected = false,
                TenantId = Guid.NewGuid(),
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                ModifiedBy = Guid.NewGuid(),
                SynthesisVersion = "ExampleVersion",
                LastOnline = DateTime.UtcNow,
                CurrentlyOnline = false,
                AveragePingTime = TimeSpan.FromMilliseconds(10)
            };
        }
    }
}