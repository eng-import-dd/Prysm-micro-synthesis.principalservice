using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Enums;

namespace Synthesis.PrincipalService.Requests
{
    public class GetUsersParams: ServerSidePagingParams
    {
        public UserGroupingTypeEnum UserGroupingType { get; set; }
        public Guid UserGroupingId { get; set; }
        public bool ExcludeUsersInGroup { get; set; }
        public bool OnlyCurrentUser { get; set; }
        public bool IncludeInactive { get; set; }

        public IdpFilterEnum IdpFilter { get; set; }
    }
}
