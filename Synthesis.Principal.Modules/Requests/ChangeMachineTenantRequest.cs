using System;
using Synthesis.Serialization;

namespace Synthesis.PrincipalService.Requests
{
    public class ChangeMachineTenantRequest
    {
        [ApiMember]
        public Guid Id { get; set; }

        [ApiMember]
        public Guid TenantId { get; set; }

        [ApiMember]
        public Guid SettingProfileId { get; set; }
    }
}