using System;
using System.Runtime.Serialization;

namespace Synthesis.PrincipalService.Requests
{
    public class CreateUserGroupRequest
    {
        [DataMember]
        public Guid UserId { get; set; }

        [DataMember]
        public Guid GroupId { get; set; }
    }
}
