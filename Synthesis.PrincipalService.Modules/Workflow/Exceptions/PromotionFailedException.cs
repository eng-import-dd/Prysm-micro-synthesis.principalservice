using System;

namespace Synthesis.PrincipalService.Workflow.Exceptions
{
    public class PromotionFailedException : Exception
    {
        public PromotionFailedException(string message): base(message)
        {
        }
    }
}