using System;
using System.Collections.Generic;
using System.Linq;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IUserSearchBuilder
    {
        IQueryable<User> BuildSearchQuery(Guid? currentUserId, List<Guid> userIds, GetUsersParams searchOptions);
    }
}