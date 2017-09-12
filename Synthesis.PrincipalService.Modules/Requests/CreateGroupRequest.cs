using System;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Requests
{
    /// <summary>
    /// Create Group Request Class.
    /// </summary>
    public class CreateGroupRequest
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public Guid TenantId { get; set; }

        public string Name { get; set; }

        public bool IsLocked { get; set; }

        public int UserCount { get; set; }

        public bool HasProtectedPermissions { get; set; }
    }
}
