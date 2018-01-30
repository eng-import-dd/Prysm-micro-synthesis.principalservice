using System;

namespace Synthesis.PrincipalService.Exceptions
{
    public class UserExistsException : Exception
    {
        public UserExistsException(string message) : base (message)
        {
        }

        public UserExistsException()
        {
        }
    }
}
