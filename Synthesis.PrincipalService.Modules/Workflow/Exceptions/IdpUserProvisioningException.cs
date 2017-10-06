using System;

namespace Synthesis.PrincipalService.Workflow.Exceptions
{
    public class IdpUserProvisioningException : Exception
    {
        public IdpUserProvisioningException(string message): base(message)
        {
        }
    }
}
