using System;

namespace Synthesis.PrincipalService.Workflow.Exceptions
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