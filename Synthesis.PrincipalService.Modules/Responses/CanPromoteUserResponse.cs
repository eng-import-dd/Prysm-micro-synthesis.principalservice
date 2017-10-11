using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Responses
{
    public class CanPromoteUserResponse
    {
        public Guid? UserId { get; set; }
        public CanPromoteUserResultCode ResultCode { get; set; }
    }

    public enum CanPromoteUserResultCode
    {
        UserAccountAlreadyExists = 1,
        UserCanBePromoted = 2
    }
}
