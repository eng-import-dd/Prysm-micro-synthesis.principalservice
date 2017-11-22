using System;
using System.Runtime.Serialization;

namespace Synthesis.PrincipalService.Requests
{
    public class CreateUserGroupRequest
    {
        [DataMember]
        public Guid GroupId { get; set; }

        [DataMember]
        public Guid UserId { get; set; }

        public static CreateUserGroupRequest Example()
        {
            return new CreateUserGroupRequest
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid()
            };
        }
    }
}