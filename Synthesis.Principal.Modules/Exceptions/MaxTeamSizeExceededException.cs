using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Synthesis.Principal.Modules.Test")]
namespace Synthesis.PrincipalService.Exceptions
{
    class MaxTeamSizeExceededException : Exception
    {
        public MaxTeamSizeExceededException(string message) : base(message)
        {

        }

        public MaxTeamSizeExceededException()
        {

        }
    }
}
