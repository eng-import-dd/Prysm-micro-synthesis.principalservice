using System;

namespace Synthesis.PrincipalService.Exceptions
{
    public class EmailAlreadyVerifiedException : Exception
    {
        public EmailAlreadyVerifiedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public EmailAlreadyVerifiedException(string message) : base(message)
        {
        }

        public EmailAlreadyVerifiedException()
        {
        }
    }
}
