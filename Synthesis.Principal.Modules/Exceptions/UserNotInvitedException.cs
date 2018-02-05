using System;

namespace Synthesis.PrincipalService.Exceptions
{
    public class UserNotInvitedException : Exception
    {
        public UserNotInvitedException(string message) : base (message)
        {
        }

        public UserNotInvitedException()
        {
        }
    }
}
