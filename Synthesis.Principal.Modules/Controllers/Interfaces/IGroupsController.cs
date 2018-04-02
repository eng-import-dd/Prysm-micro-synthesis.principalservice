using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Controllers
{
    /// <summary>
    /// Groups Controller Interface.
    /// </summary>
    public interface IGroupsController
    {
        /// <summary>
        /// Creates the group asynchronous.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <returns>
        /// Group object.
        /// </returns>
        Task<Group> CreateGroupAsync(Group group, Guid tenantId, Guid userId);

        Task<Group> GetGroupByIdAsync(Guid groupId, Guid tenantId);

        Task<IEnumerable<Group>> GetGroupsForTenantAsync(Guid tenantId, Guid userId);

        Task<bool> DeleteGroupAsync(Guid groupId, Guid userId);

        Task<Group> UpdateGroupAsync(Group group, Guid tenantId, Guid userId);
        Task<Group> CreateDefaultGroupAsync(Guid tenantId);
    }
}
