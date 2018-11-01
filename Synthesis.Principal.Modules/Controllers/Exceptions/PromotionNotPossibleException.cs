using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class PromotionNotPossibleException : Exception
    {
        public PromotionNotPossibleException(string message) : base(message)
        {
        }
    }
}