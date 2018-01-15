using System;
using Synthesis.PrincipalService.Enums;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Requests
{
    public class GuestVerificationResponse
    {
        public Guid? AccountId { get; set; }
        public Project AssociatedProject { get; set; }
        public string ProjectAccessCode { get; set; }
        public string ProjectName { get; set; }
        public VerifyGuestResponseCode ResultCode { get; set; }
        public Guid? UserId { get; set; }
        public string Username { get; set; }
    }
}
