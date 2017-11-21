using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.License.Manager.Models;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Controllers
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

        Task<User> CreateUserGroupAsync(CreateUserGroupRequest model, Guid tenantId, Guid userId);

        Task<List<Guid>> GetGroupUsersAsync(Guid groupId, Guid tenantId, Guid userId);

        Task<List<Guid>> GetGroupsForUserAsync(Guid userId);

        Task<PagingMetadata<UserResponse>> GetGuestUsersForTenantAsync(Guid tenantId, GetUsersParams getGuestUsersParams);
       
        Task<UserResponse> AutoProvisionRefreshGroupsAsync(IdpUserRequest model, Guid tenantId, Guid createdBy);

        Task<CanPromoteUserResponse> CanPromoteUserAsync(string email);

        Task<bool> ResendUserWelcomeEmailAsync(string email, string firstName);

        Task<Guid> GetTenantIdByUserEmailAsync(string email);

        Task<bool> RemoveUserFromPermissionGroupAsync(Guid userId, Guid groupId,  Guid currentUserId);

        Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<Guid> userIds);

        Task<LicenseType?> GetLicenseTypeForUserAsync(Guid userId, Guid tenantId);

        Task<User> GetUserByUserNameOrEmailAsync(string username);
    }
}
