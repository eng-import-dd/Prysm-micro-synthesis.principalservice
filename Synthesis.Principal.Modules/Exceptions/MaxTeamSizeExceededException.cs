using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
