using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class AssignUserToTenantException : Exception
    {
        public AssignUserToTenantException(string message) : base(message)
        {
        }
    }
}