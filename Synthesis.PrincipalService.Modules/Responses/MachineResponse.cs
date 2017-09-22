using System;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Responses
{
    public class MachineResponse
    {
        [JsonProperty("id")]
        public Guid MachineId { get; set; }

        public string MachineKey { get; set; }

        public string Location { get; set; }

        public Guid? SettingProfileId { get; set; }

        public string SettingProfileName { get; set; }

        public bool IsSelected { get; set; }

        public Guid AccountId { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateModified { get; set; }

        public Guid ModifiedBy { get; set; }

        public string SynthesisVersion { get; set; }

        public DateTime? LastOnline { get; set; }

        public bool CurrentlyOnline { get; set; }

        public TimeSpan AveragePingTime { get; set; }

        [Obsolete("Must keep because 2.3/2.4/2.5 clients have a hard reference")]
        public bool? IsUserLicenseRequired { get; set; }
    }
}
