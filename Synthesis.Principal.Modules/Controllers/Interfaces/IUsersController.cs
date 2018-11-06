using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers.Exceptions;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IUsersController
    {
        Task<User> CreateUserAsync(CreateUserRequest model, ClaimsPrincipal principal);

        Task<CreateGuestUserResponse> CreateGuestUserAsync(CreateUserRequest model);

        Task<User> GetUserAsync(Guid userId);

        Task<User> UpdateUserAsync(Guid userId, User userModel, Guid tenantId, ClaimsPrincipal claimsPrincipal);

        Task DeleteUserAsync(Guid userId);

        /// <summary>
        /// Begins an asynchronous operation that promotes a guest user to a fully licensed user.
        /// </summary>
        /// <param name="userId">Guid of the user to promote</param>
        /// <param name="tenantId">The Guid of the tenant to add the user to</param>
        /// <param name="licenseType">Type of license to assign to the user</param>
        /// <param name="claimsPrincipal">Principal who can manage licenses</param>
        /// <param name="autoPromote">Force using license type regardless of Claims Principal rights</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="ValidationFailedException">Thrown when invalid params are passed in</exception>
        /// <exception cref="LicenseNotAvailableException">Thrown when no license is available to assign to a user</exception>
        /// <exception cref="NotFoundException">Thrown when the user cannot be found</exception>
        /// <exception cref="UserAlreadyMemberOfTenantException">Thrown when the user has already been promoted to a licensed user</exception>
        /// <exception cref="EmailNotInTenantDomainException">Thrown when the users email domain is not an domain used by the tenant.</exception>
        /// <exception cref="AssignUserToTenantException">Thrown when there was error adding the user to the tenant</exception>
        /// <exception cref="LicenseAssignmentFailedException">Thrown when assigning a license to the user fails</exception>
        Task PromoteGuestUserAsync(Guid userId, Guid tenantId, LicenseType licenseType, ClaimsPrincipal claimsPrincipal, bool autoPromote = false);

        Task<PagingMetadata<BasicUser>> GetUsersBasicAsync(Guid tenantId, Guid userId, UserFilteringOptions userFilteringOptions);

        Task<int> GetUserCountAsync(Guid tenantId, Guid userId, UserFilteringOptions userFilteringOptions);

        Task<PagingMetadata<User>> GetUsersForTenantAsync(UserFilteringOptions userFilteringOptions, Guid tenantId, Guid currentUserId);

        Task<IEnumerable<UserNames>> GetNamesForUsersAsync(IEnumerable<Guid> userIds);

        Task<bool> LockOrUnlockUserAsync(Guid userId, Guid tenantId, bool isLocked);

        Task<UserGroup> CreateUserGroupAsync(UserGroup model, Guid currentUserId);

        Task<List<Guid>> GetUserIdsByGroupIdAsync(Guid groupId, Guid currentUserId);

        Task<List<Guid>> GetGroupIdsByUserIdAsync(Guid userId);

        Task<PagingMetadata<User>> GetGuestUsersForTenantAsync(Guid tenantId, UserFilteringOptions userFilteringOptions);

        Task<User> AutoProvisionRefreshGroupsAsync(IdpUserRequest model, Guid tenantId, ClaimsPrincipal claimsPrincipal);

        Task<CanPromoteUser> CanPromoteUserAsync(string email, Guid tenantId);

        Task<bool> RemoveUserFromPermissionGroupAsync(Guid userId, Guid groupId, Guid currentUserId);

        Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<Guid> userIds);

        Task<LicenseType?> GetLicenseTypeForUserAsync(Guid userId, Guid tenantId);

        Task<User> GetUserByUserNameOrEmailAsync(string username);

        Task<VerifyUserEmailResponse> VerifyEmailAsync(VerifyUserEmailRequest verifyRequest);

        Task SendGuestVerificationEmailAsync(GuestVerificationEmailRequest request);
    }
}