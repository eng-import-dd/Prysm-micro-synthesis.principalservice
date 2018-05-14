using System;

namespace Synthesis.PrincipalService.Exceptions
{
    class BuildEmailException : Exception
    {
        public BuildEmailException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public BuildEmailException(string message) : base(message)
        {
        }

        public BuildEmailException()
        {
        }
    }
}
