using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Responses
{
    public class UserInviteResponse
    {
        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public Guid TenantId { get; set; }

        public DateTime? LastInvitedDate { get; set; }

        public bool? IsUserEmailFormatInvalid { get; set; }

        public bool? IsUserEmailDomainFree { get; set; }

        public bool? IsUserEmailDomainAllowed { get; set; }

        public bool? IsDuplicateUserEmail { get; set; }

        public bool? IsDuplicateUserEntry { get; set; }
    }
}
