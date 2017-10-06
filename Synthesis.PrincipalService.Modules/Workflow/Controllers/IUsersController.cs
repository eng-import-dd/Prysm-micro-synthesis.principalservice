using Synthesis.PrincipalService.Dao.Models;
using System;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Entity;
using Synthesis.License.Manager.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public interface IUsersController
    {
        Task<UserResponse> CreateUserAsync(CreateUserRequest model, Guid tenantId, Guid createdBy);

        Task<UserResponse> GetUserAsync(Guid userId);

        Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest model);

        Task DeleteUserAsync(Guid userId);

        Task<PromoteGuestResponse> PromoteGuestUserAsync(Guid userId, Guid tenantId, LicenseType licenseType, bool autoPromote = false);

        Task<PagingMetadata<BasicUserResponse>> GetUsersBasicAsync(Guid tenantId, Guid userId, GetUsersParams getUsersParams);

        Task<PagingMetadata<UserResponse>> GetUsersForAccountAsync(GetUsersParams getUsersParams, Guid tenantId, Guid currentUserId);

        Task<bool> LockOrUnlockUserAsync(Guid userId, bool isLocked);

        Task<PagingMetadata<UserResponse>> GetGuestUsersForTenantAsync(Guid tenantId, GetUsersParams getGuestUsersParams);
       

        Task<UserResponse> AutoProvisionRefreshGroups(IdpUserRequest model, Guid tenantId, Guid createdBy);
    }
}
