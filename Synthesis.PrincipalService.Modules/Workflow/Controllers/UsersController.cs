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
using System.Linq.Expressions;
using System.Runtime.Remoting.Contexts;
using System.Threading.Tasks;
using Nancy;
using Synthesis.Nancy.MicroService.Security;
using Synthesis.PrincipalService.Entity;
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
        private readonly IValidator _tenantIdValidator;
        private readonly IValidator _updateUserRequestValidator;
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
            _updateUserRequestValidator = validatorLocator.GetValidator(typeof(UpdateUserRequestValidator));
            _userIdValidator = validatorLocator.GetValidator(typeof(UserIdValidator));
            _tenantIdValidator = validatorLocator.GetValidator(typeof(TenantIdValidator));
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

        /// <inheritdoc />
        public async Task<UserResponse> GetUserAsync(Guid id)
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
            return _mapper.Map<User, UserResponse>(result);
        }

        /// <inheritdoc />
        public async Task<PagingMetadata<BasicUserResponse>> GetUsersBasicAsync(Guid tenantId, Guid userId, GetUsersParams getUsersParams)
        {
            var validationResult = await _userIdValidator.ValidateAsync(userId);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to retrieve a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userListResult = await GetAccountUsersFromDb(tenantId, userId, getUsersParams);
            if(userListResult == null)
            {
                _logger.Warning($"Users resource could not be found for input data.");
                throw new NotFoundException($"Users resource could not be found for input data.");
            }

            var basicUserResponse = _mapper.Map<PagingMetadata<User>, PagingMetadata<BasicUserResponse>>(userListResult);
            return basicUserResponse;
        }

        public async Task<PagingMetadata<UserResponse>> GetUsersForAccountAsync(GetUsersParams getUsersParams, Guid tenantId, Guid currentUserId)
        {
            var userIdValidationResult = await _userIdValidator.ValidateAsync(currentUserId);
            var tenantIdValidationresult = await _tenantIdValidator.ValidateAsync(tenantId);
            var errors = new List<ValidationFailure>();

            if (!userIdValidationResult.IsValid)
            {
                errors.AddRange(userIdValidationResult.Errors);
            }
            if (!tenantIdValidationresult.IsValid)
            {
                errors.AddRange(tenantIdValidationresult.Errors);
            }
            if (errors.Any())
            {
                _logger.Warning("Failed to validate the resource id and/or resource while attempting to get a User resource.");
                throw new ValidationFailedException(errors);
            }

            try
            {
                var usersInAccount = await _userRepository.GetItemsAsync(u => u.TenantId == tenantId);
                if (!usersInAccount.Any())
                {
                    _logger.Warning($"Users for the account could not be found");
                    throw new NotFoundException($"Users for the account could not be found");
                }

                var users = await GetAccountUsersFromDb(tenantId, currentUserId, getUsersParams);
                var userResponse =_mapper.Map<PagingMetadata<User>, PagingMetadata<UserResponse>>(users);
                return userResponse;
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, ex);
                return null;
            }

        }
        public async Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest userModel)
        {
            
            TrimNameOfUser(userModel);
            var errors=new List<ValidationFailure>();
            if (!await IsUniqueUsername(userId, userModel.UserName))
            {
                errors.Add(new ValidationFailure(nameof(userModel.UserName), "A user with that UserName already exists."));
            }
            var userIdValidationResult = await _userIdValidator.ValidateAsync(userId);
            var userValidationResult = await _updateUserRequestValidator.ValidateAsync(userModel);
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
            /* Only allow the user to modify the license type if they have permission.  If they do not have permission, ensure the license type has not changed. */
            //TODO: For this GetGroupPermissionsForUser method should be implemented which is in collaboration service.
            try
            {
                var user = _mapper.Map<UpdateUserRequest, User>(userModel);
                return await UpdateUserInDb(user, userId);
            }
            catch (DocumentNotFoundException ex)
            {
                _logger.LogMessage(LogLevel.Error, "Could not find the user to update", ex);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, "Could not update the user", ex);
                throw;
            }
            
        }

        public async Task<CanPromoteUserResponse> CanPromoteUserAsync(string email)
        {
            var isValidEmail = EmailValidator.IsValid(email);
            if (!isValidEmail)
            {
                _logger.Warning("Email is either empty or invalid.");
                throw new ValidationException("Email is either empty or invalid");
            }

            try
            {
                var userList = await _userRepository.GetItemsAsync(u => u.Email.Equals(email));
                var existingUser = userList.ToList().FirstOrDefault();
                if (existingUser==null)
                {
                    _logger.Error("User not found with that email.");
                    return new CanPromoteUserResponse
                    {
                        ResultCode = CanPromoteUserResultCode.UserDoesNotExist,
                    };
                }

                var isValidForPromotion = IsValidPromotionForTenant(existingUser, existingUser.TenantId);
                if (isValidForPromotion != PromoteGuestResultCode.UserAlreadyPromoted && isValidForPromotion != PromoteGuestResultCode.Failed)
                {
                    return new CanPromoteUserResponse
                    {
                        ResultCode = CanPromoteUserResultCode.UserCanBePromoted,
                        UserId = existingUser.Id
                    };
                }

                _logger.Warning("User already in an account");
                return new CanPromoteUserResponse
                {
                    ResultCode = CanPromoteUserResultCode.UserAccountAlreadyExists
                };
            }
            catch (Exception ex)
            {
                _logger.Error("User not found with that email.", ex);
                throw;
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

        private async Task<UserResponse> UpdateUserInDb(User user, Guid id)
        {
            var existingUser = await _userRepository.GetItemAsync(id);
            if (existingUser == null)
            {
                throw new NotFoundException($"A User resource could not be found for id {id}");
            }

            if (string.IsNullOrEmpty(user.UserName))
            {
                user.UserName = existingUser.UserName;
            }
            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Email = user.Email;
            existingUser.PasswordAttempts = user.PasswordAttempts;
            existingUser.IsLocked = user.IsLocked;
            existingUser.IsIdpUser = user.IsIdpUser;
            if (!string.IsNullOrEmpty(user.PasswordHash) && user.PasswordHash != existingUser.PasswordHash)
            {
                existingUser.PasswordHash = user.PasswordHash;
                existingUser.PasswordLastChanged = DateTime.Now;
            }
            try
            {
                await _userRepository.UpdateItemAsync(id, existingUser);
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, "User Updation failed", ex);
                throw;
            }

            return _mapper.Map<User, UserResponse>(existingUser);
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
        public async Task<bool> LockOrUnlockUserAsync(Guid userId, bool locked)
        {
            var validationResult = await _userIdValidator.ValidateAsync(userId);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to delete a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }
            try
            {
                //TODO: dependency on group implementation to get all the groupIds
                #region  Get superadmin group ids

                // if trying to lock a user we need to check if it is a superAdmin user
                //if (isLocked && _collaborationService.IsSuperAdmin(userId))
                //{
                //    // Determine the number of non locked superAdmin users
                //    var superAdminUserIds = _collaborationService.GetUserGroupsForGroup(_collaborationService.SuperAdminGroupId).Payload.Select(x => x.UserId);
                //    var countOfNonLockedSuperAdmins = superAdminUserIds.Count(id => !_collaborationService.GetUserById(id).Payload.IsLocked ?? false);

                //    // reject locking the last non-locked superAdmin user
                //    if (countOfNonLockedSuperAdmins <= 1)
                //    {
                //        return new ServiceResult<bool>
                //        {
                //            Payload = false,
                //            Message = "LockUser: Failed to lock user", // Do not disclose that this user is in the superadmin group
                //            ResultCode = ResultCode.Failed
                //        };
                //    }
                //}


                #endregion
                return await UpdateLockUserDetailsInDb(userId, locked);
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, "Couldn't Lock the user: ", ex);
                throw;
            }
        }

        private async Task<bool> UpdateLockUserDetailsInDb(Guid id, bool isLocked)
        {
            var validationErrors = new List<ValidationFailure>();
            try
            {
                var existingUser = await _userRepository.GetItemAsync(id);
                if (existingUser == null)
                {
                    validationErrors.Add(new ValidationFailure(nameof(existingUser), "Unable to find th euser with the user id"));
                }
                else
                {
                    var licenseRequestDto = new UserLicenseDto
                    {
                        UserId = id.ToString(),
                        AccountId = existingUser.TenantId.ToString()
                    };

                    if (isLocked)
                    {
                        try
                        {
                            /* If the user is being locked, remove the associated license. */
                            var removeUserLicenseResult = await _licenseApi.ReleaseUserLicenseAsync(licenseRequestDto);

                            if (removeUserLicenseResult.ResultCode != LicenseResponseResultCode.Success)
                            {
                                validationErrors.Add(new ValidationFailure(nameof(existingUser), "Unable to remove license for the user"));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogMessage(LogLevel.Error, "Erro removing license from user", ex);
                            throw;
                        }
                    }
                    else // unlock
                    {
                        try
                        {
                            /* If not locked, request a license. */
                            var assignUserLicenseResult = await _licenseApi.AssignUserLicenseAsync(licenseRequestDto);

                            if (assignUserLicenseResult.ResultCode != LicenseResponseResultCode.Success)
                            {

                                validationErrors.Add(new ValidationFailure(nameof(existingUser), "Unable to assign license to the user"));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogMessage(LogLevel.Error, "Erro assigning license to user", ex);
                            throw;

                        }
                    }
                

                if (validationErrors.Any())
                {
                    throw new ValidationFailedException(validationErrors);
                }

            await LockUser(id, isLocked);

            return true;
                }
            }
        catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, "Erro occured while updating locked/unlocked field in the DB", ex);

            }

            return false;
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
        public async Task<PagingMetadata<User>> GetAccountUsersFromDb(Guid tenantId, Guid? currentUserId, GetUsersParams getUsersParams)
        {
            try
            {
                if (getUsersParams == null)
                {
                    getUsersParams = new GetUsersParams
                    {
                        SearchValue = "",
                        OnlyCurrentUser = false,
                        IncludeInactive = false,
                        SortColumn = "FirstName",
                        SortDescending = false,
                        IdpFilter = IdpFilter.All,
                    };
                }
                var  criteria = new List<Expression<Func<User, bool>>>();
                Expression<Func<User, string>> orderBy;
                criteria.Add(u => u.TenantId == tenantId);

                if (getUsersParams.OnlyCurrentUser)
                {
                    criteria.Add(u => u.Id == currentUserId);
                }

                if (!getUsersParams.IncludeInactive)
                {
                    criteria.Add(u => !u.IsLocked);
                }
                switch (getUsersParams.IdpFilter)
                {
                    case IdpFilter.IdpUsers:
                        criteria.Add(u => u.IsIdpUser == true);
                        break;
                    case IdpFilter.LocalUsers:
                        criteria.Add(u => u.IsIdpUser == false);
                        break;
                    case IdpFilter.NotSet:
                        criteria.Add(u => u.IsIdpUser == null);
                        break;
                }

                if (!string.IsNullOrEmpty(getUsersParams.SearchValue))
                {
                    criteria.Add(x =>
                             x != null &&
                             (x.FirstName.ToLower() + " " + x.LastName.ToLower()).Contains(
                                                                                           getUsersParams.SearchValue.ToLower()) ||
                             x != null && x.Email.ToLower().Contains(getUsersParams.SearchValue.ToLower()) ||
                             x != null && x.UserName.ToLower().Contains(getUsersParams.SearchValue.ToLower()));
                }
                if (string.IsNullOrWhiteSpace(getUsersParams.SortColumn))
                {
                    orderBy = u => u.FirstName;
                }
                else
                {
                        switch (getUsersParams.SortColumn.ToLower())
                        {
                            case "firstname":
                                orderBy = u => u.FirstName;
                                break;

                            case "lastname":
                                orderBy = u => u.LastName;
                                break;

                            case "email":
                                orderBy = u => u.Email;
                                break;

                            case "username":
                                orderBy = u => u.UserName;
                                break;

                            default:
                                // LINQ to Entities requires calling OrderBy before using .Skip and .Take methods
                                orderBy = u => u.FirstName;
                                break;
                    }
                }
                var queryparams = new OrderedQueryParameters<User, string>
                {
                    Criteria =criteria,
                    OrderBy = orderBy,
                    SortDescending = getUsersParams.SortDescending,
                    ContinuationToken = getUsersParams.ContinuationToken??""
                };
                var usersInAccountsResult = await _userRepository.GetOrderedPaginatedItemsAsync(queryparams);
                if (!usersInAccountsResult.Items.Any())
                {
                    throw new NotFoundException("Users for this account could not be found");
                }
                var usersInAccounts = usersInAccountsResult.Items.ToList();
                var filteredUserCount = usersInAccounts.Count;
                var resultingUsers = usersInAccounts;
                var returnMetaData = new PagingMetadata<User>
                {
                    CurrentCount = filteredUserCount,
                    List = resultingUsers,
                    SearchValue = getUsersParams.SearchValue,
                    ContinuationToken = usersInAccountsResult.ContinuationToken,
                    IsLastChunk = usersInAccountsResult.IsLastChunk
                };

                return returnMetaData;
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, ex+" Failed to get users for account");
                throw;
            }
        }

        private void TrimNameOfUser(UpdateUserRequest user)
        {
            if (!string.IsNullOrEmpty(user.FirstName))
            {
                user.FirstName = user.FirstName.Trim();
            }
            if (!string.IsNullOrEmpty(user.LastName))
            {
                user.LastName = user.LastName.Trim();
            }
        }

    }
}