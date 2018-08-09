using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IUsersController
    {
        Task<User> CreateUserAsync(CreateUserRequest model, Guid createdBy, ClaimsPrincipal principal);

        Task<CreateGuestUserResponse> CreateGuestUserAsync(CreateUserRequest model);

        Task<User> GetUserAsync(Guid userId);

        Task<User> UpdateUserAsync(Guid userId, User userModel, Guid tenantId, ClaimsPrincipal claimsPrincipal);

        Task DeleteUserAsync(Guid userId);

        Task<CanPromoteUserResultCode> PromoteGuestUserAsync(Guid userId, Guid tenantId, LicenseType licenseType, ClaimsPrincipal claimsPrincipal, bool autoPromote = false);
        Task<PagingMetadata<BasicUser>> GetUsersBasicAsync(Guid tenantId, Guid userId, UserFilteringOptions userFilteringOptions);

        Task<int> GetUserCountAsync(Guid tenantId, Guid userId, UserFilteringOptions userFilteringOptions);

        Task<PagingMetadata<User>> GetUsersForTenantAsync(UserFilteringOptions userFilteringOptions, Guid tenantId, Guid currentUserId);

        Task<IEnumerable<UserNames>> GetNamesForUsers(IEnumerable<Guid> userIds);

        Task<bool> LockOrUnlockUserAsync(Guid userId, Guid tenantId, bool isLocked);

        Task<UserGroup> CreateUserGroupAsync(UserGroup model, Guid tenantId, Guid currentUserId);

        Task<List<Guid>> GetUserIdsByGroupIdAsync(Guid groupId, Guid tenantId, Guid userId);

        Task<List<Guid>> GetGroupIdsByUserIdAsync(Guid userId);

        Task<PagingMetadata<User>> GetGuestUsersForTenantAsync(Guid tenantId, UserFilteringOptions userFilteringOptions);

        Task<User> AutoProvisionRefreshGroupsAsync(IdpUserRequest model, Guid tenantId, Guid createdBy, ClaimsPrincipal claimsPrincipal);

        Task<CanPromoteUser> CanPromoteUserAsync(string email, Guid tenantId);

        Task<bool> RemoveUserFromPermissionGroupAsync(Guid userId, Guid groupId, Guid currentUserId);

        Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<Guid> userIds);

        Task<LicenseType?> GetLicenseTypeForUserAsync(Guid userId, Guid tenantId);

        Task<User> GetUserByUserNameOrEmailAsync(string username);

        Task<VerifyUserEmailResponse> VerifyEmailAsync(VerifyUserEmailRequest verifyRequest);

        Task SendGuestVerificationEmailAsync(GuestVerificationEmailRequest request);
    }
}