using Synthesis.PrincipalService.Models;
using System.Collections.Generic;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Models
{
    public class LockUserRequest
    {
        public List<UserEmailRequest> OrgAdmins { get; set; }

        public string UserFullName { get; set; }

        public string UserEmail { get; set; }


        public static LockUserRequest Example()
        {
            return new LockUserRequest
            {
                UserEmail = "some@email.com",
                UserFullName = "username",
                OrgAdmins = new List<UserEmailRequest> { UserEmailRequest.Example() }
            };
        }
    }
}
