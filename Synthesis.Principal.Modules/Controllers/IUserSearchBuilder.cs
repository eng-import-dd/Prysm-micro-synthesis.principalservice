using System;
using System.Collections.Generic;
using System.Linq;
using Synthesis.PrincipalService.InternalApi.Models;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IUserSearchBuilder
    {
        Task<IQueryable<User>> BuildSearchQueryAsync(Guid? currentUserId, List<Guid> userIds, UserFilteringOptions filteringOptions);
    }
}