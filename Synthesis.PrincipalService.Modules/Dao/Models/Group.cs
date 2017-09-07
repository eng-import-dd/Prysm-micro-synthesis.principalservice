using Newtonsoft.Json;
using System;

namespace Synthesis.PrincipalService.Dao.Models
{
    public class Group
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public string Name { get; set; }

        public bool IsLocked { get; set; }
    }
}
