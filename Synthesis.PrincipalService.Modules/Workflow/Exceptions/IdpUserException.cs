using System;

namespace Synthesis.PrincipalService.Workflow.Exceptions
{
    public class IdpUserException : Exception
    {
        public IdpUserException(string message): base(message)
        {
        }
    }
}
