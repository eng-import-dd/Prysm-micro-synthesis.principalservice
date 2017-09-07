using Synthesis.PrincipalService.Dao.Models;
using System;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public interface IUsersController
    {
        Task<UserResponse> CreateUserAsync(CreateUserRequest model, Guid tenantId, Guid createdBy);

        Task<UserResponse> GetUserAsync(Guid userId);

        Task<User> UpdateUserAsync(Guid userId, User model);

        Task DeleteUserAsync(Guid userId);

        /// <summary>
        /// Gets the users basic data.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="getUsersParams">The get users parameters.</param>
        /// <returns>Task object of List of User Basic Response.</returns>
        Task<PagingMetaData<UserResponse>> GetUsersBasicAsync(Guid tenantId, Guid userId, GetUsersParams getUsersParams);

        Task<PagingMetaData<UserResponse>> GetUsersForAccount(GetUsersParams getUsersParams, Guid tenantId, Guid currentUserId);
    }
}
