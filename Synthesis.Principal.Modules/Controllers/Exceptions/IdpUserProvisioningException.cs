using System;

namespace Synthesis.PrincipalService.Controllers
{
    public class IdpUserProvisioningException : Exception
    {
        public IdpUserProvisioningException(string message): base(message)
        {
        }
    }
}
