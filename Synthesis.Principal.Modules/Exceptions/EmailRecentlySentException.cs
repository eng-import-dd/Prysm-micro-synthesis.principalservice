using System;

namespace Synthesis.PrincipalService.Exceptions
{
    public class EmailRecentlySentException : Exception
    {
        public EmailRecentlySentException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public EmailRecentlySentException(string message) : base(message)
        {
        }

        public EmailRecentlySentException()
        {
        }
    }
}
