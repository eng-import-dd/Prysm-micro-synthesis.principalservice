using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Entity;

namespace Synthesis.PrincipalService.Requests
{
    public class GetUsersParams: ServerSidePagingParams
    {
        public bool OnlyCurrentUser { get; set; }
        public bool IncludeInactive { get; set; }

        public IdpFilter IdpFilter { get; set; }
    }

    public enum IdpFilter
    {
        All,
        IdpUsers,
        LocalUsers,
        NotSet
    }
}
