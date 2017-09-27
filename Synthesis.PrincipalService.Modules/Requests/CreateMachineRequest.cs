using System;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Requests
{
    public class CreateMachineRequest
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
         
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
       
    }
}
