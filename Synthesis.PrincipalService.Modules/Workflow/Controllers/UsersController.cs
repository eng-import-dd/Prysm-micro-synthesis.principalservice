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
using System.Threading.Tasks;
using Nancy;
using Synthesis.Nancy.MicroService.Security;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Utilities;

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

            var userListResult = GetAccountUsersFromDb(tenantId, userId, getUsersParams);
            if(userListResult == null)
            {
                _logger.Warning($"Users resource could not be found for input data.");
                throw new NotFoundException($"Users resource could not be found for input data.");
            }

            var basicUserResponse = _mapper.Map<PagingMetadata<User>, PagingMetadata<BasicUserResponse>>(userListResult);
            return basicUserResponse;
        }

        public async Task<PagingMetadata<BasicUserResponse>> GetUsersForAccountAsync(GetUsersParams getUsersParams, Guid tenantId, Guid currentUserId)
        {
            var userIdValidationResult = await _userIdValidator.ValidateAsync(currentUserId);
            var userValidationResult = await _createUserRequestValidator.ValidateAsync(currentUserId);
            var errors = new List<ValidationFailure>();

            if (!userIdValidationResult.IsValid)
            {
                errors.AddRange(userIdValidationResult.Errors);
            }
            if (errors.Any())
            {
                _logger.Warning("Failed to validate the resource id and/or resource while attempting to get a User resource.");
                throw new ValidationFailedException(errors);
            }

            try
            {
                var usersInAccount = await _userRepository.GetItemsAsync(u => u.TenantId == tenantId);
                if (usersInAccount == null)
                {
                    _logger.Warning($"Users for the account could not be found");
                    throw new NotFoundException($"Users for the account could not be found");
                }

                var users = GetAccountUsersFromDb(tenantId, currentUserId, getUsersParams);
                var userResponse =_mapper.Map<PagingMetadata<User>, PagingMetadata<BasicUserResponse>>(users);
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
            #region Check Group Permissions for a user and allow him to modify License type
            //if (!CollaborationService.GetGroupPermissionsForUser(UserId).Payload.Contains(PermissionEnum.CanManageUserLicenses))
            //{
            //    ServiceResult<LicenseType?> existingLicenseResult;

            //    try
            //    {
            //        existingLicenseResult = await _licenseService.GetUserLicenseType(id, AccountId);
            //    }
            //    catch (FailedToConnectToLicenseServiceException failedToConnectException)
            //    {
            //        _loggingService.LogError(LogTopic.LICENSING, failedToConnectException, "Failed to update user because a connection could not be made to the license service.");
            //        return new ServiceResult<SynthesisUserDTO>
            //        {
            //            Payload = null,
            //            Message = "Failed to update user because a connection could not be made to the license service.",
            //            ResultCode = ResultCode.Failed
            //        };
            //    }

            //    if (existingLicenseResult.ResultCode == ResultCode.Success)
            //    {
            //        if (userDto.LicenseType != existingLicenseResult.Payload)
            //        {
            //            return new ServiceResult<SynthesisUserDTO>
            //            {
            //                Payload = null,
            //                Message = "You are not authorized to manage license types",
            //                ResultCode = ResultCode.Success
            //            };
            //        }
            //    }
            //    else
            //    {
            //        return new ServiceResult<SynthesisUserDTO>
            //        {
            //            Payload = null,
            //            Message = existingLicenseResult.Message,
            //            ResultCode = existingLicenseResult.ResultCode
            //        };
            //    }
            //}
            #endregion

            try
            {
                var user = _mapper.Map<UpdateUserRequest, User>(userModel);
                return await UpdateUserInDb(user, userId);
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, "Could not update the user", ex);
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
           var errors = new List<ValidationFailure>();
            
            var existingUser = await _userRepository.GetItemAsync(id);
            if (existingUser == null)
            {
                throw new NotFoundException($"A User resource could not be found for id {id}");
            }

            if (string.IsNullOrEmpty(user.UserName))
            {
                user.UserName = existingUser.UserName;
            }
            if (!await IsUniqueUsername(id, user.UserName))
            {
                errors.Add(new ValidationFailure(nameof(user.UserName), "A user with that UserName already exists."));
            }
            if (errors.Any())
            {
                _logger.Warning("Failed to validate the resource id and/or resource while attempting to update a User resource.");
                throw new ValidationFailedException(errors);
            }

            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Email = user.Email;
            existingUser.PasswordAttempts = user.PasswordAttempts ?? user.PasswordAttempts;
            existingUser.IsLocked = user.IsLocked;
            existingUser.IsIdpUser = user.IsIdpUser ?? user.IsIdpUser;
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
        public PagingMetadata<User> GetAccountUsersFromDb(Guid accountId, Guid? currentUserId, GetUsersParams getUsersParams)
        {
            try
            {
                if (accountId == Guid.Empty)
                {
                    var ex = new ArgumentException("accountId");
                    _logger.LogMessage(LogLevel.Error, ex);
                    throw ex;
                }

                IEnumerable<User> users = new List<User>();
                var usersInAccounts = _userRepository.GetItemsAsync(u => u.TenantId == accountId);
                    var userCountTotal = getUsersParams.OnlyCurrentUser
                                             ? 1
                                             : usersInAccounts.Result.ToList().Count;

                //Todo: Convert all the queries to existing Documentdb supported queries

                if (getUsersParams.UserGroupingType.Equals(UserGroupingType.Project))
                {
                    //ToDo: Get the users in the project-Dependency on project service
                    #region Dependancy on project service
                    //if (getUsersParams.ExcludeUsersInGroup)
                    //{
                    //    users = (from uc in sdc.UserAccounts
                    //             join u in sdc.SynthesisUsers on uc.SynthesisUserID equals u.UserID

                    //             let usersInProject = (from uc2 in sdc.UserAccounts
                    //                                   join u2 in sdc.SynthesisUsers on uc2.SynthesisUserID equals u2.UserID
                    //                                   join up2 in sdc.UserProjects on uc2.SynthesisUserID equals up2.UserID
                    //                                   where uc2.AccountID == accountId && up2.ProjectID == getUsersParams.UserGroupingId
                    //                                   select u2)

                    //             where uc.AccountID == accountId && !usersInProject.Any(x => x.UserID == u.UserID)
                    //             select u);
                    //}
                    //else
                    //{
                    //    users = (from uc in sdc.UserAccounts
                    //             join u in sdc.SynthesisUsers on uc.SynthesisUserID equals u.UserID
                    //             join up in sdc.UserProjects on uc.SynthesisUserID equals up.UserID
                    //             where uc.AccountID == accountId && up.ProjectID == getUsersParams.UserGroupingId
                    //             select u);
                    //}
                    #endregion
                }
                else if (getUsersParams.UserGroupingType.Equals(UserGroupingType.Permission))
                {
                    
                    if (getUsersParams.ExcludeUsersInGroup)
                    {
                        users = usersInAccounts.Result.Where(u => !u.Groups.Contains(getUsersParams.UserGroupingId) );
                        
                    }
                    else
                    {
                        users = usersInAccounts.Result.Where(u => u.Groups.Contains(getUsersParams.UserGroupingId));
                    }
                    
                }
                else
                {
                    users = usersInAccounts.Result;
                }
                if (getUsersParams.OnlyCurrentUser)
                {
                    users = users.Where(u => u.Id == currentUserId);
                }

                if (!getUsersParams.IncludeInactive)
                {
                    users = users.Where( u => !u.IsLocked);
                }
                switch (getUsersParams.IdpFilter)
                {
                    case IdpFilter.IdpUsers:
                        users = users.Where(u => u.IsIdpUser == true);
                        break;
                    case IdpFilter.LocalUsers:
                        users = users.Where(u => u.IsIdpUser == false);
                        break;
                    case IdpFilter.NotSet:
                        users = users.Where(u => u.IsIdpUser == null);
                        break;
                }

                if (!string.IsNullOrEmpty(getUsersParams.SearchValue))
                {
                    users = users.Where
                        (x =>
                             x != null &&
                             (x.FirstName.ToLower() + " " + x.LastName.ToLower()).Contains(
                                                                                           getUsersParams.SearchValue.ToLower()) ||
                             x != null && x.Email.ToLower().Contains(getUsersParams.SearchValue.ToLower()) ||
                             x != null && x.UserName.ToLower().Contains(getUsersParams.SearchValue.ToLower())
                        );
                }
                if (string.IsNullOrWhiteSpace(getUsersParams.SortColumn))
                {
                    // LINQ to Entities requires calling OrderBy before using .Skip and .Take methods
                    users = users.OrderBy(x => x.FirstName);
                }
                else
                {
                    if (getUsersParams.SortOrder == DataSortOrder.Ascending)
                    {
                        switch (getUsersParams.SortColumn.ToLower())
                        {
                            case "firstname":
                                users = users.OrderBy(x => x.FirstName);
                                break;

                            case "lastname":
                                users = users.OrderBy(x => x.LastName);
                                break;

                            case "email":
                                users = users.OrderBy(x => x.Email);
                                break;

                            case "username":
                                users = users.OrderBy(x => x.UserName);
                                break;

                            default:
                                // LINQ to Entities requires calling OrderBy before using .Skip and .Take methods
                                users = users.OrderBy(x => x.FirstName);
                                break;

                        }
                    }
                    else
                    {
                        switch (getUsersParams.SortColumn.ToLower())
                        {
                            case "firstname":
                                users = users.OrderByDescending(x => x.FirstName);
                                break;

                            case "lastname":
                                users = users.OrderByDescending(x => x.LastName);
                                break;

                            case "email":
                                users = users.OrderByDescending(x => x.Email);
                                break;

                            case "username":
                                users = users.OrderByDescending(x => x.UserName);
                                break;

                            default:
                                // LINQ to Entities requires calling OrderBy before using .Skip and .Take methods
                                users = users.OrderByDescending(x => x.FirstName);
                                break;
                        }
                    }
                }

                var filteredUserCount = users.Count();
                if (getUsersParams.PageSize > 0)
                {
                    var pageNumber = getUsersParams.PageNumber > 0 ? getUsersParams.PageNumber - 1 : 0;
                    users = users.Skip(pageNumber * getUsersParams.PageSize).Take(getUsersParams.PageSize);
                }
                var resultingUsers = users.ToList();
                var returnMetaData = new PagingMetadata<User>
                {
                    TotalCount = userCountTotal,
                    CurrentCount = filteredUserCount,
                    List = resultingUsers,
                    SearchFilter = getUsersParams.SearchValue,
                    CurrentPage = getUsersParams.PageNumber
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