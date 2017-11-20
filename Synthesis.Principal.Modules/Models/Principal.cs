using System;
using Newtonsoft.Json;
using Synthesis.Nancy.MicroService.Serialization;

namespace Synthesis.PrincipalService.Models
{
    public class Principal
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public Guid ProjectId { get; set; }

        public string Name { get; set; }

        [IgnoreSerialization]
        public DateTime? CreatedDate { get; set; }

        public DateTime? LastAccessDate { get; set; }

    }
}
