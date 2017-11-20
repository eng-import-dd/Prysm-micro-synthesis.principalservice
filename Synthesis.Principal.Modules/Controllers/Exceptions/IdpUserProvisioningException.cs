using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class IdpUserProvisioningException : Exception
    {
        public IdpUserProvisioningException(string message): base(message)
        {
        }
    }
}
