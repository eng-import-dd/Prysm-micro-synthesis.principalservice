using System;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Models
{
    public class Group
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }
        public bool IsLocked { get; set; }
        public string Name { get; set; }
        public Guid TenantId { get; set; }

        public static Group Example()
        {
            return new Group
            {
                Id = Guid.NewGuid(),
                IsLocked = false,
                Name = "group",
                TenantId = Guid.NewGuid()
            };
        }
    }
}