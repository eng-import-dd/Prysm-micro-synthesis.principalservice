using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.License.Manager.Interfaces;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleCrypto;
using Synthesis.PrincipalService.Utilities;
using Synthesis.PrincipalService.Workflow.Exceptions;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    /// <summary>
    /// Represents a controller for User resources.
    /// </summary>
    /// <seealso cref="Synthesis.PrincipalService.Workflow.Controllers.IUsersController" />
    public class UsersController : IUsersController
    {
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Group> _groupRepository;
        private readonly IValidator _createUserRequestValidator;
        private readonly IValidator _userIdValidator;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly ILicenseApi _licenseApi;
        private readonly IEmailUtility _emailUtility;
        private readonly IMapper _mapper;
        private readonly string _deploymentType;
        private const string OrgAdminRoleName = "Org_Admin";
        private const string BasicUserRoleName = "Basic_User";

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="licenseApi"></param>
        /// <param name="emailUtility"></param>
        /// <param name="mapper"></param>
        /// <param name="deploymentType"></param>
        public UsersController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILogger logger,
            ILicenseApi licenseApi,
            IEmailUtility emailUtility,
            IMapper mapper,
            string deploymentType)
        {
            _userRepository = repositoryFactory.CreateRepository<User>();
            _groupRepository = repositoryFactory.CreateRepository<Group>();
            _createUserRequestValidator = validatorLocator.GetValidator(typeof(CreateUserRequestValidator));
            _userIdValidator = validatorLocator.GetValidator(typeof(UserIdValidator));
            _eventService = eventService;
            _logger = logger;
            _licenseApi = licenseApi;
            _emailUtility = emailUtility;
            _mapper = mapper;
            _deploymentType = deploymentType;
        }

        public async Task<UserResponse> CreateUserAsync(CreateUserRequest model, Guid tenantId, Guid createdBy)
        {
            //TODO Check for CanManageUserLicenses permission if user.LicenseType != null
            
            var validationResult = await _createUserRequestValidator.ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Validation failed while attempting to create a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var user = _mapper.Map<CreateUserRequest, User>(model);

            if (IsBuiltInOnPremTenant(tenantId))
            {
                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(user.TenantId), "Users cannot be created under provisioning tenant") });
            }

            user.TenantId = tenantId;
            user.CreatedBy = createdBy;
            user.CreatedDate = DateTime.Now;
            user.FirstName = user.FirstName.Trim();
            user.LastName = user.LastName.Trim();

            var result = await CreateUserInDb(user);

            await AssignUserLicense(result, model.LicenseType);

            await _eventService.PublishAsync(EventNames.UserCreated, result);

            return _mapper.Map<User, UserResponse>(result);
        }

        public async Task<User> GetUserAsync(Guid id)
        {
            var validationResult = await _userIdValidator.ValidateAsync(id);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to retrieve a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _userRepository.GetItemAsync(id);

            if (result == null)
            {
                _logger.Warning($"A User resource could not be found for id {id}");
                throw new NotFoundException($"A User resource could not be found for id {id}");
            }

            return result;
        }

        public async Task<User> UpdateUserAsync(Guid userId, User userModel)
        {
            var userIdValidationResult = await _userIdValidator.ValidateAsync(userId);
            var userValidationResult = await _createUserRequestValidator.ValidateAsync(userModel);
            var errors = new List<ValidationFailure>();

            if (!userIdValidationResult.IsValid)
            {
                errors.AddRange(userIdValidationResult.Errors);
            }

            if (!userValidationResult.IsValid)
            {
                errors.AddRange(userValidationResult.Errors);
            }

            if (errors.Any())
            {
                _logger.Warning("Failed to validate the resource id and/or resource while attempting to update a User resource.");
                throw new ValidationFailedException(errors);
            }

            try
            {
                return await _userRepository.UpdateItemAsync(userId, userModel);
            }
            catch (DocumentNotFoundException)
            {
                return null;
            }
        }

        public async Task DeleteUserAsync(Guid id)
        {
            var validationResult = await _userIdValidator.ValidateAsync(id);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to delete a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            try
            {
                await _userRepository.DeleteItemAsync(id);

                _eventService.Publish(new ServiceBusEvent<Guid>
                {
                    Name = EventNames.UserDeleted,
                    Payload = id
                });
            }
            catch (DocumentNotFoundException)
            {
                // We don't really care if it's not found.
                // The resource not being there is what we wanted.
            }
        }

        public async Task<PromoteGuestResponse> PromoteGuestUserAsync(Guid userId, Guid tenantId , LicenseType licenseType, bool autoPromote = false)
        {
            var validationResult = await _userIdValidator.ValidateAsync(userId);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Validation failed while attempting to promote guest.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userAccountExistsResult = new PromoteGuestResponse
            {
                Message = $"User {userId} is not valid for promotion because they are already assigned to a tenant",
                UserId = userId,
                ResultCode = PromoteGuestResultCode.UserAlreadyPromoted
            };

            if (autoPromote)
            {
                var licenseAvailable = await IsLicenseAvailable(tenantId, licenseType);

                if (!licenseAvailable)
                {
                    throw new PromotionFailedException("Not promoting the user as there are no user licenses available");
                }
            }

            var user = await _userRepository.GetItemAsync(userId);

            var isValidResult = IsValidPromotionForTenant(user, tenantId);
            if (isValidResult != PromoteGuestResultCode.Success)
            {
                if (isValidResult == PromoteGuestResultCode.UserAlreadyPromoted)
                {
                    return userAccountExistsResult;
                }

                throw new PromotionFailedException("User is not valid for promotion");
            }

            var assignGuestResult = await AssignGuestUserToTenant(user, tenantId);
            if (assignGuestResult != PromoteGuestResultCode.Success)
            {
                throw new PromotionFailedException($"Failed to assign Guest User {userId} to tenant {tenantId}");
            }

            if (!autoPromote && licenseType != LicenseType.Default)
            {
                //Todo Check if the user has CanManageUserLicenses permission
                //var permissions = CollaborationService.GetGroupPermissionsForUser(UserId).Payload;
                //if (permissions == null || !permissions.Contains(PermissionEnum.CanManageUserLicenses))
                //{
                //    // Don't allow user to pick the license type without the CanManageUserLicenses permission
                //    licenseType = LicenseType.Default;
                //}
            }

            var assignLicenseResult = await _licenseApi.AssignUserLicenseAsync(new UserLicenseDto{
                AccountId = tenantId.ToString(),
                UserId = userId.ToString(),
                LicenseType = licenseType.ToString()
            });

            if (assignLicenseResult == null || assignLicenseResult.ResultCode != LicenseResponseResultCode.Success)
            {
                // If assignign a license fails, then we must disable the user
                await LockUser(userId, true);

                throw new LicenseAssignmentFailedException($"Assigned user {userId} to tenant {tenantId}, but failed to assign license", userId);
            }

            _emailUtility.SendWelcomeEmail(user.Email, user.FirstName);

            return new PromoteGuestResponse
            {
                Message = "",
                UserId = userId,
                ResultCode = PromoteGuestResultCode.Success
            };
        }

        public async Task<UserResponse> AutoProvisionRefreshGroups(IdpUserRequest model, Guid tenantId, Guid createddBy)
        {
            //TODO: Validating tenant id in below block of code is temporary. - Yusuf
            //SEC-76 branch has complete code to validate tenant id through validator classes.
            //Once code is merged, the following code will be updated to use tenant id validator.

           if (tenantId == Guid.Empty)
            {
                IEnumerable<ValidationFailure> errors = new []{new ValidationFailure("","") };
                _logger.Warning("Failed to validate the tenant id .");
                throw new ValidationFailedException(errors);
            }

            if (model.UserId == null || model.UserId == Guid.Empty)
            {
                return await AutoProvisionUserAsync(model, tenantId, createddBy);
            }

            var userId = model.UserId.Value;
            if (model.IsGuestUser)
            {
                var promoteGuestResponse = await PromoteGuestUserAsync(userId, model.TenantId, LicenseType.UserLicense, true);
                if (promoteGuestResponse?.ResultCode == PromoteGuestResultCode.Failed)
                {
                    throw new PromotionFailedException($"Failed to promote user {userId}");
                }
                _emailUtility.SendWelcomeEmail(model.EmailId, model.FirstName);
            }
            if (model.Groups != null)
            {
                await UpdateIdpUserGroupsAsync(model.UserId.Value, model);
            }
            var result = await _userRepository.GetItemAsync(model.UserId.Value);
            return _mapper.Map<User, UserResponse>(result);
        }

        private async Task<UserResponse> AutoProvisionUserAsync(IdpUserRequest model, Guid tenantId, Guid createddBy)
        {
            //We will create a long random password for the user and throw it away so that the Idp users can't login using local credentials
            var tempPassword = GenerateRandomPassword(64);
            var hash = HashAndSalt(tempPassword, out var salt);

            var user = new CreateUserRequest()
            {
                Email = model.EmailId,
                UserName = model.EmailId,
                FirstName = model.FirstName,
                LastName = model.LastName,
                LicenseType = LicenseType.UserLicense,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsIdpUser = true
            };

            var result = await CreateUserAsync(user, tenantId, createddBy);
            if (result != null && model.Groups !=null)
            {
                var groupResult = await UpdateIdpUserGroupsAsync(result.Id.Value, model);
                if (groupResult == null)
                {
                   throw  new IdpUserException($"Failed to update Idp user groups for user {result.Id.Value}");
                }
            }
            else
            {
                //if the user is created but the license allocation fails, CreateUserAsync locks the 
                //user account, but returns the usr object without setting the locked flag. We update it before returning the object
                if (result != null)
                {
                    result.IsLocked = true;
                }
            }

            return result;
        }

        private async Task<User> UpdateIdpUserGroupsAsync(Guid userId, IdpUserRequest model)
        {
            var currentGroupsResult = await _userRepository.GetItemAsync(userId);
            
            //TODO: GetGroupsForAccount(Guid accountId) has some logic related "PermissionsForGroup" and "protectedPermissions" in DatabaseServices class
            var accountGroupsResult = await _groupRepository.GetItemsAsync(g => g.TenantId == model.TenantId);

            var accountGroups = accountGroupsResult as IList<Group> ?? accountGroupsResult.ToList();
            foreach (var accountGroup in accountGroups)
            {
                if (model.IdpMappedGroups?.Contains(accountGroup.Name) == false)
                {
                    //in case IdpMappedGroups is specified, skip updating group memebership if this group is not mapped
                    continue;
                }

                if (model.Groups.Contains(accountGroup.Name))
                {
                    //Add the user to the group
                    if(currentGroupsResult.Groups.Contains(accountGroup.Id.Value))
                    {
                        continue; //Nothing to do if the user is already a member of the group
                    }

                    currentGroupsResult.Groups.Add(accountGroup.Id.Value);
                    var result = await _userRepository.UpdateItemAsync(userId, currentGroupsResult);
                    return result;
                }
                else
                {
                    //remove the user from the group
                    currentGroupsResult.Groups.Remove(accountGroup.Id.Value);
                    var result = await _userRepository.UpdateItemAsync(userId, currentGroupsResult);
                    return result;
                }
            }

            return currentGroupsResult;
        }

        private static string GenerateRandomPassword(int length)
        {
            const string valid = @"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890~!@#$%^&*()_+-={}|:<>?[]\;',./'";
            StringBuilder res = new StringBuilder();
            Random rnd = new Random();
            while (0 < length--)
            {
                res.Append(valid[rnd.Next(valid.Length)]);
            }
            return res.ToString();
        }

        private static string HashAndSalt(string pass, out string salt)
        {
            //hashing parameters
            const int saltSize = 64;
            const int hashIterations = 10000;

            ICryptoService cryptoService = new SimpleCrypto.PBKDF2();
            var hash = cryptoService.Compute(pass, saltSize, hashIterations);
            salt = cryptoService.Salt;
            return hash;
        }


        private async Task<bool> IsLicenseAvailable(Guid tenantId, LicenseType licenseType)
        {
            var summary = await _licenseApi.GetTenantLicenseSummaryAsync(tenantId);
            var item = summary.FirstOrDefault(x => string.Equals(x.LicenseName, licenseType.ToString(), StringComparison.CurrentCultureIgnoreCase));

            return item != null && item.TotalAvailable > 0;
        }

        private PromoteGuestResultCode IsValidPromotionForTenant(User user, Guid tenantId)
        {
            if (user?.Email == null)
            {
                return PromoteGuestResultCode.Failed;
            }

            if (user.TenantId != Guid.Empty)
            {
                return PromoteGuestResultCode.UserAlreadyPromoted;
            }

            var domain = user.Email.Substring(user.Email.IndexOf('@')+1);
            var hasMatchingTenantDomains = GeTenantEmailDomains(tenantId).Contains(domain);

            return hasMatchingTenantDomains ? PromoteGuestResultCode.Success : PromoteGuestResultCode.Failed;
        }

        private async Task<PromoteGuestResultCode> AssignGuestUserToTenant(User user, Guid tenantId)
        {
            user.TenantId = tenantId;
            await _userRepository.UpdateItemAsync(user.Id.Value, user);

            _eventService.Publish(new ServiceBusEvent<Guid>
            {
                Name = EventNames.UserPromoted,
                Payload = user.Id.Value
            });

            return PromoteGuestResultCode.Success;
        }




        private List<string> GeTenantEmailDomains(Guid tenantId)
        {
            //Todo Get Tenant domains from tenant Micro service
            return new List<string> { "test.com", "prysm.com" };
        }

        private bool IsBuiltInOnPremTenant(Guid tenantId)
        {
            if (string.IsNullOrEmpty(_deploymentType) || !_deploymentType.StartsWith("OnPrem"))
            {
                return false;
            }

            return tenantId.ToString().ToUpper() == "2D907264-8797-4666-A8BB-72FE98733385" ||
                   tenantId.ToString().ToUpper() == "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3";
        }


        private async Task<User> CreateUserInDb(User user)
        {
            var validationErrors = new List<ValidationFailure>();

            if (!await IsUniqueUsername(user.Id, user.UserName))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.UserName), "A user with that UserName already exists."));
            }

            if (!await IsUniqueEmail(user.Id, user.Email))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.Email), "A user with that email address already exists.") );
            }
            
            if (!string.IsNullOrEmpty(user.LdapId) && !await IsUniqueLdapId(user.Id, user.LdapId))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.LdapId), "Unable to provision user. The LDAP User Account is already in use."));
            }

            //TODO Check if it is a valid tenant
            //var tenant = dc.Accounts.Find(tenantId);
            //if (tenant == null)
            //{
            //    var ex = new Exception("Unable to provision user. The tanant could not be found with the given id.");
            //    LogError(ex);
            //    throw ex;
            //}

            if (validationErrors.Any())
            {
                throw new ValidationFailedException(validationErrors);
            }

            user.Groups = new List<Guid>();
            var basicUserGroupId = await GetBuiltInGroupId(user.TenantId, BasicUserRoleName);
            if (basicUserGroupId != null)
            {
                user.Groups.Add( basicUserGroupId.Value);
            }
            
            var result = await _userRepository.CreateItemAsync(user);
            
            return result;
        }

        private async Task AssignUserLicense(User user, LicenseType? licenseType)
        {
            if (user.Id == null || user.Id == Guid.Empty)
            {
                throw new ArgumentException("User Id is required for assiging license");
            }

            var licenseRequestDto = new UserLicenseDto
            {
                AccountId = user.TenantId.ToString(),
                LicenseType = (licenseType ?? LicenseType.Default).ToString(),
                UserId = user.Id?.ToString()
            };

            try
            {
                /* If the user is successfully created assign the license. */
                var assignedLicenseServiceResult = await _licenseApi.AssignUserLicenseAsync(licenseRequestDto);

                if (assignedLicenseServiceResult.ResultCode == LicenseResponseResultCode.Success)
                {
                    /* If the user is created and a license successfully assigned, mail and return the user. */
                    _emailUtility.SendWelcomeEmail(user.Email, user.FirstName);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, "Erro assigning license to user", ex);
            }
            /* If a license could not be obtained lock the user that was just created. */
            await LockUser(user.Id.Value, true);
            user.IsLocked = true;

            //Intimate the Org Admin of the user's teanant about locked user
            var orgAdmins = await GetTenantAdminsByIdAsync(user.TenantId);

            if (orgAdmins.Count > 0)
            {
                _emailUtility.SendUserLockedMail(orgAdmins, $"{user.FirstName} {user.LastName}" , user.Email);
            }
        }

        private async Task<List<User>> GetTenantAdminsByIdAsync(Guid userTenantId)
        {
            var adminGroupId = await GetBuiltInGroupId(userTenantId, OrgAdminRoleName);
            if(adminGroupId != null)
            { 
                var admins = await _userRepository.GetItemsAsync(u => u.TenantId == userTenantId && u.Groups.Contains(adminGroupId.Value));
                return admins.ToList();
            }

            return new List<User>();
        }

        private async Task<Guid?> GetBuiltInGroupId(Guid userTenantId, string groupName)
        {
            var groups = await _groupRepository.GetItemsAsync(g => g.TenantId == userTenantId && g.Name == groupName && g.IsLocked);
            return groups.FirstOrDefault()?.Id;
        }

        private async Task<User> LockUser(Guid userId, bool locked)
        {
            var user = await _userRepository.GetItemAsync(userId);
            user.IsLocked = locked;

            await _userRepository.UpdateItemAsync(userId, user);
            return user;
        }

        private async Task<bool> IsUniqueUsername(Guid? userId, string username)
        {
            var users = await _userRepository
                            .GetItemsAsync(u => userId == null || userId.Value == Guid.Empty
                                                    ? u.UserName == username
                                                    : u.Id != userId && u.UserName == username);
            return !users.Any();
        }

        private async Task<bool> IsUniqueEmail(Guid? userId, string email)
        {
            var users = await _userRepository
                            .GetItemsAsync(u => userId == null || userId.Value == Guid.Empty
                                                    ? u.Email == email
                                                    : u.Id != userId && u.Email == email);
            return !users.Any();
        }

        private async Task<bool> IsUniqueLdapId(Guid? userId, string ldapId)
        {
            var users = await _userRepository
                            .GetItemsAsync(u => userId == null || userId.Value == Guid.Empty
                                                    ? u.LdapId == ldapId
                                                    : u.Id != userId && u.LdapId == ldapId);
            return !users.Any();
        }
    }
}