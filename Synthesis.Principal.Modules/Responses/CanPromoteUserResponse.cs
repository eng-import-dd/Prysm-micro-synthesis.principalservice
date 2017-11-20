using System;

namespace Synthesis.PrincipalService.Responses
{
    public class CanPromoteUserResponse
    {
        public CanPromoteUserResultCode ResultCode { get; set; }
        public Guid? UserId { get; set; }
    }

    public enum CanPromoteUserResultCode
    {
        UserAccountAlreadyExists = 1,
        UserCanBePromoted = 2
    }
}