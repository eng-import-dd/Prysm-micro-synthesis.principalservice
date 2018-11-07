using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class UserAlreadyMemberOfTenantException : Exception
    {
        public UserAlreadyMemberOfTenantException(string message) : base(message)
        {
        }
    }
}