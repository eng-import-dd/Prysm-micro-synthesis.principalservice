using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IUsersController
    {
        Task<User> CreateUserAsync(CreateUserRequest model, Guid createdBy);

        Task<User> CreateGuestUserAsync(CreateUserRequest model);

        Task<User> GetUserAsync(Guid userId);

        Task<User> UpdateUserAsync(Guid userId, User model);

        Task DeleteUserAsync(Guid userId);

        Task<CanPromoteUserResultCode> PromoteGuestUserAsync(Guid userId, Guid tenantId, LicenseType licenseType, bool autoPromote = false);

        Task<PagingMetadata<BasicUser>> GetUsersBasicAsync(Guid tenantId, Guid userId, UserFilteringOptions userFilteringOptions);

        Task<PagingMetadata<User>> GetUsersForTenantAsync(UserFilteringOptions userFilteringOptions, Guid tenantId, Guid currentUserId);

        Task<IEnumerable<UserNames>> GetNamesForUsers(IEnumerable<Guid> userIds);

        Task<bool> LockOrUnlockUserAsync(Guid userId, bool isLocked);

        Task<UserGroup> CreateUserGroupAsync(UserGroup model, Guid tenantId, Guid userId);

        Task<List<Guid>> GetUserIdsByGroupIdAsync(Guid groupId, Guid tenantId, Guid userId);

        Task<List<Guid>> GetGroupIdsByUserIdAsync(Guid userId);

        Task<PagingMetadata<User>> GetGuestUsersForTenantAsync(Guid tenantId, UserFilteringOptions userFilteringOptions);

        Task<User> AutoProvisionRefreshGroupsAsync(IdpUserRequest model, Guid tenantId, Guid createdBy);

        Task<CanPromoteUser> CanPromoteUserAsync(string email, Guid tenantId);

        Task<bool> RemoveUserFromPermissionGroupAsync(Guid userId, Guid groupId, Guid currentUserId);

        Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<Guid> userIds);

        Task<LicenseType?> GetLicenseTypeForUserAsync(Guid userId, Guid tenantId);

        Task<User> GetUserByUserNameOrEmailAsync(string username);

        Task SendGuestVerificationEmailAsync(GuestVerificationEmailRequest request);
    }
}