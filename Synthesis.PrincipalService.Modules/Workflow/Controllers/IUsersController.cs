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

        Task<UserResponse> UpdateUserAsync(Guid userId, CreateUserRequest model);

        Task DeleteUserAsync(Guid userId);

        Task<PagingMetadata<BasicUserResponse>> GetUsersBasicAsync(Guid tenantId, Guid userId, GetUsersParams getUsersParams);

        Task<PagingMetadata<BasicUserResponse>> GetUsersForAccountAsync(GetUsersParams getUsersParams, Guid tenantId, Guid currentUserId);
    }
}
