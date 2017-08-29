using System;
using Newtonsoft.Json;


namespace Synthesis.PrincipalService.Dao.Models
{
    public class User
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public string Name { get; set; }

        public DateTime? CreatedDate { get; set; }

        public DateTime? LastAccessDate { get; set; }

    }
}
