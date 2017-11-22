using System;
using Newtonsoft.Json;
using Synthesis.Nancy.MicroService.Serialization;

namespace Synthesis.PrincipalService.Models
{
    public class Principal
    {
        [IgnoreSerialization]
        public DateTime? CreatedDate { get; set; }

        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public DateTime? LastAccessDate { get; set; }
        public string Name { get; set; }
        public Guid ProjectId { get; set; }

        public static Principal Example()
        {
            return new Principal
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                Name = "ExampleName",
                CreatedDate = DateTime.UtcNow,
                LastAccessDate = DateTime.UtcNow
            };
        }
    }
}