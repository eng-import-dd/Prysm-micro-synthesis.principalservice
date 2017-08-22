using System;
using Newtonsoft.Json;


namespace Synthesis.PrincipalService.Dao.Models
{
    public class Principalservice
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        public string Name { get; set; }

        public DateTime? CreatedDate { get; set; }

        public DateTime? LastAccessDate { get; set; }

    }
}
