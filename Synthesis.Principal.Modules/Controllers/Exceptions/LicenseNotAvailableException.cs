using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class LicenseNotAvailableException : Exception
    {
        public LicenseNotAvailableException(string message) : base(message)
        {
        }
    }
}