using System.Collections.Generic;
using Synthesis.EmailService.InternalApi.Models;

namespace Synthesis.PrincipalService.Models
{
    //Todo : Remove this class while integrating with email internal api
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
