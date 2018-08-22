using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Controllers
{
    /// <summary>
    /// Groups Controller Interface.
    /// </summary>
    public interface IGroupsController
    {
        Task<Group> CreateGroupAsync(Group group, Guid tenantId, Guid currentUserId, CancellationToken cancellationToken = default(CancellationToken));

        Task<Group> GetGroupByIdAsync(Guid groupId, Guid tenantId, CancellationToken cancellationToken = default(CancellationToken));

        Task<IEnumerable<Group>> GetGroupsForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default(CancellationToken));

        Task<bool> DeleteGroupAsync(Guid groupId, Guid tenantId, Guid currentUserId, CancellationToken cancellationToken = default(CancellationToken));

        Task<Group> UpdateGroupAsync(Group group, Guid userId, CancellationToken cancellationToken = default(CancellationToken));

        Task CreateBuiltInGroupsAsync(Guid tenantId, CancellationToken cancellationToken = default(CancellationToken));

        Task<Guid?> GetTenantIdForGroupIdAsync(Guid groupId, Guid? tenantId, CancellationToken cancellationToken = default(CancellationToken));
    }
}