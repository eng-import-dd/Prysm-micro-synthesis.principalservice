using System;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Workflow.Controllers
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
    }
}
