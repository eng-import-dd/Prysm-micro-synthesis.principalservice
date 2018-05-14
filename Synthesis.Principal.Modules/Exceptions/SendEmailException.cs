using System;

namespace Synthesis.PrincipalService.Exceptions
{
    public class SendEmailException : Exception
    {
        public SendEmailException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public SendEmailException(string message) : base(message)
        {
        }

        public SendEmailException()
        {
        }
    }
}
