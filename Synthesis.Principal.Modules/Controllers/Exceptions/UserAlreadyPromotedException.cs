using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class UserAlreadyPromotedException : Exception
    {
        public UserAlreadyPromotedException(string message) : base(message)
        {
        }
    }
}