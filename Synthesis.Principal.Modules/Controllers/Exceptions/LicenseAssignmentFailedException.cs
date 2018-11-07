using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class LicenseAssignmentFailedException : Exception
    {
        public LicenseAssignmentFailedException(string message) : base(message)
        {
        }
    }
}