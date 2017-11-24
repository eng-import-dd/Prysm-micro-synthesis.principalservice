using System;

namespace Synthesis.PrincipalService.Controllers
{
    public class PromotionFailedException : Exception
    {
        public PromotionFailedException(string message): base(message)
        {
        }
    }
}