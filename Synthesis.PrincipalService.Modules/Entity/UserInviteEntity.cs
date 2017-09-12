using System;
using Synthesis.PrincipalService.Enums;

namespace Synthesis.PrincipalService.Entity
{
    public class UserInviteEntity
    {
        public string Email { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public Guid TenantId { get; set; }

        public DateTime? LastInvitedDate { get; set; }

        public InviteUserStatus Status { get; set; }
    }
}
