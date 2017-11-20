using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class LicenseAssignmentFailedException : Exception
    {
        public Guid UserId { get; }

        public LicenseAssignmentFailedException(string message, Guid userId) : base(message)
        {
            UserId = userId;
        }
    }
}