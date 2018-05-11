using System;

namespace Synthesis.PrincipalService.Exceptions
{
    class IdentityPasswordException : Exception
    {
        public IdentityPasswordException(string message) : base(message)
        {
        }

        public IdentityPasswordException()
        {
        }
    }
}
