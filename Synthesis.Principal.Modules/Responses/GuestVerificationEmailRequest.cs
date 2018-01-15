using System;
using System.Collections.Generic;
namespace Synthesis.PrincipalService.Responses
{
    public class GuestVerificationEmailRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ProjectAccessCode { get; set; }
    }
}
