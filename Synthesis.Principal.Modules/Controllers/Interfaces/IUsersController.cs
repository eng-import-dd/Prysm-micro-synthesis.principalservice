using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.InternalApi.Models;
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

        Task<Entity.PagingMetadata<BasicUserResponse>> GetUsersBasicAsync(Guid tenantId, Guid userId, GetUsersParams getUsersParams);

        Task<Entity.PagingMetadata<UserResponse>> GetUsersForAccountAsync(GetUsersParams getUsersParams, Guid tenantId, Guid currentUserId);

        Task<IEnumerable<UserNames>> GetNamesForUsers(IEnumerable<Guid> userIds);

        Task<bool> LockOrUnlockUserAsync(Guid userId, bool isLocked);

        Task<User> CreateUserGroupAsync(CreateUserGroupRequest model, Guid tenantId, Guid userId);

        Task<List<Guid>> GetGroupUsersAsync(Guid groupId, Guid tenantId, Guid userId);

        Task<List<Guid>> GetGroupsForUserAsync(Guid userId);

        Task<Entity.PagingMetadata<UserResponse>> GetGuestUsersForTenantAsync(Guid tenantId, GetUsersParams getGuestUsersParams);

        Task<UserResponse> AutoProvisionRefreshGroupsAsync(IdpUserRequest model, Guid tenantId, Guid createdBy);

        Task<CanPromoteUser> CanPromoteUserAsync(string email, Guid tenantId);

        Task<bool> ResendUserWelcomeEmailAsync(string email, string firstName);

        Task<bool> RemoveUserFromPermissionGroupAsync(Guid userId, Guid groupId, Guid currentUserId);

        Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<Guid> userIds);

        Task<LicenseType?> GetLicenseTypeForUserAsync(Guid userId, Guid tenantId);

        Task<User> GetUserByUserNameOrEmailAsync(string username);

        Task<UserResponse> CreateGuestAsync(CreateUserRequest request, Guid tenantId, Guid createdBy);
    }
}