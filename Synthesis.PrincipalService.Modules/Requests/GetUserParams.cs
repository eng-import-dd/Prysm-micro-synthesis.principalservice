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
        public UserGroupingType UserGroupingType { get; set; }
        public Guid UserGroupingId { get; set; }
        public bool ExcludeUsersInGroup { get; set; }
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

    /// <summary>
    /// UserGroupingType is used to filter the get users for account call to include or exclude users from a particular grouping
    /// </summary>
    public enum UserGroupingType
    {
        /// <summary>
        /// No group
        /// </summary>
        None,
        /// <summary>
        /// Project group
        /// </summary>
        Project,
        /// <summary>
        /// Permission group
        /// </summary>
        Permission
    }
}
