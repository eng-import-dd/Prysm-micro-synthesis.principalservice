using System;

namespace Synthesis.PrincipalService.Dao.Models
{
    public class InvitedUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public DateTime LastInvitedDate { get; set; }
    }
}