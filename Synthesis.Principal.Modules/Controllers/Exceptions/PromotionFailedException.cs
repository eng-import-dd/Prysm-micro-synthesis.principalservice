using System;

namespace Synthesis.PrincipalService.Controllers.Exceptions
{
    public class PromotionFailedException : Exception
    {
        public PromotionFailedException(string message) : base(message)
        {
        }

        public PromotionFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}