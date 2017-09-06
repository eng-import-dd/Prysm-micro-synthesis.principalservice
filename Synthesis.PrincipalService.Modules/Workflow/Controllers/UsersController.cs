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
using Synthesis.PrincipalService.Utility;
using Synthesis.PrincipalService.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Enums;
using Synthesis.PrincipalService.Entity;

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
        public UsersController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILogger logger,
            ILicenseApi licenseApi,
            IEmailUtility emailUtility,
            IMapper mapper)
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

            _eventService.Publish(EventNames.UserCreated, result);

            return _mapper.Map<User, UserResponse>(result);
        }

        /// <summary>
        /// Gets the user using asynchronous.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>Task object of User Response.</returns>
        /// <exception cref="ValidationFailedException"></exception>
        /// <exception cref="NotFoundException"></exception>
        public async Task<UserResponse> GetUserAsync(Guid id)
        {
            var validationResult = await _userIdValidator.ValidateAsync(id);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to retrieve a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }
            
            var result = await _userRepository.GetItemAsync(id);
            _eventService.Publish(EventNames.UserRetrieved, result);

            if (result == null)
            {
                _logger.Warning($"A User resource could not be found for id {id}");
                throw new NotFoundException($"A User resource could not be found for id {id}");
            }
            return _mapper.Map<User, UserResponse>(result);
        }

        /// <inheritdoc />
        /// <summary>
        /// Gets the users basic data.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        /// <param name="userId">The user identifier.</param>
        /// <param name="getUsersParams">The get users parameters.</param>
        /// <returns>
        /// Task object of List of User Basic Response.
        /// </returns>
        /// <exception cref="T:Synthesis.Nancy.MicroService.NotFoundException"></exception>
        public async Task<PagingMetaData<UserResponse>> GetUsersBasicAsync(Guid tenantId, Guid userId, GetUsersParams getUsersParams)
        {
            var userListResult = GetAccountUsersFromDb(tenantId, userId, getUsersParams);
            var users = await _userRepository.GetItemsAsync(u => u.Id != null); //Revisit this line: Charan
            if(userListResult == null)
            {
                _logger.Warning($"Users resource could not be found for input data.");
                throw new NotFoundException($"Users resource could not be found for input data.");
            }

            try
            {
                var basicUserResponse = _mapper.Map<PagingMetaData<User>, PagingMetaData<UserResponse>>(userListResult);
                return basicUserResponse;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            //var basicUserResponse = userListResult.Users.Select(user => _mapper.Map<User, UserBasicResponse>(user)).ToList();
            //return basicUserResponse;
        }

        public async Task<PagingMetaData<UserResponse>> GetUsersForAccount(GetUsersParams getUsersParams, Guid tenantId)
        {
            try
            {
                var usersInAccount = await _userRepository.GetItemsAsync(u => u.TenantId == tenantId);
                if (usersInAccount == null)
                {
                    _logger.Warning($"Users could not be found");
                    throw new NotFoundException($"Users could not be found");
                }

                //TODO: find the current user id
                var users = GetAccountUsersFromDb(tenantId, usersInAccount.FirstOrDefault().Id, getUsersParams);
                var userResponse =_mapper.Map<PagingMetaData<User>, PagingMetaData<UserResponse>>(users);
                return userResponse;
            }
            catch (Exception ex)
            {
                _logger.LogMessage(LogLevel.Error, ex);
                return null;
            }

            
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


        private bool IsBuiltInOnPremTenant(Guid tenantId)
        {
            //TODO Identify if this is an on prem deployment
            //if (!_cloudSettings.DeploymentTypes.HasFlag(DeploymentTypes.OnPrem))
            {
                return false;
            }

            //return tenantId.ToString().ToUpper() == "2D907264-8797-4666-A8BB-72FE98733385" ||
            //       tenantId.ToString().ToUpper() == "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3";
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
        public PagingMetaData<User> GetAccountUsersFromDb(Guid accountId, Guid? currentUserId, GetUsersParams getUsersParams)
        {
            try
            {
                if (accountId == Guid.Empty)
                {
                    var ex = new ArgumentException("accountId");
                    _logger.LogMessage(LogLevel.Error, ex);
                    throw ex;
                }

                int userCountTotal;
                int filteredUserCount;
                List<User> resultingUsers;

                var usersInAccounts = _userRepository.GetItemsAsync(u => u.TenantId == accountId);
                //ToDo: check how to create DB context
               // IRepository<User> sdc;
                    
                    userCountTotal = getUsersParams.OnlyCurrentUser
                                     ? 1
                                     : usersInAccounts.Result.ToList().Count;

                //from uc in sdc.UserAccounts
                //    join u in sdc.SynthesisUsers on uc.SynthesisUserID equals u.UserID
                //    where uc.AccountID == accountId
                //    select u
                IEnumerable<User> users=new List<User>();

                //Todo: Convert all the queries to existing Documentdb supported queries

                if (getUsersParams.UserGroupingType.Equals(UserGroupingTypeEnum.Project))
                {
                    //ToDo: implement the logic suitable for Document DB
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
                }
                else if (getUsersParams.UserGroupingType.Equals(UserGroupingTypeEnum.Permission))
                {
                    //if (getUsersParams.ExcludeUsersInGroup)
                    //{
                    //    users = (from uc in sdc.UserAccounts
                    //             join u in sdc.SynthesisUsers on uc.SynthesisUserID equals u.UserID

                    //             let usersInPermissionGroup = (from uc2 in sdc.UserAccounts
                    //                                           join u2 in sdc.SynthesisUsers on uc2.SynthesisUserID equals u.UserID
                    //                                           join ug2 in sdc.UserGroups on uc2.SynthesisUserID equals ug2.UserId
                    //                                           where uc2.AccountID == accountId && ug2.GroupId == getUsersParams.UserGroupingId
                    //                                           select u)

                    //             where
                    //             uc.AccountID == accountId && !usersInPermissionGroup.Any(x => x.UserID == u.UserID)
                    //             select u);
                    //}
                    //else
                    //{
                    //    users = (from uc in sdc.UserAccounts
                    //             join u in sdc.SynthesisUsers on uc.SynthesisUserID equals u.UserID
                    //             join ug in sdc.UserGroups on uc.SynthesisUserID equals ug.UserId
                    //             where uc.AccountID == accountId && ug.GroupId == getUsersParams.UserGroupingId
                    //             select u);
                    //}
                }
                else
                {
                    users = usersInAccounts.Result;
                }
                if (getUsersParams.OnlyCurrentUser)
                {
                    users = users.Where(u => u.Id == currentUserId);
                }

                //ToDo: Revisit must
                //if (!getUsersParams.IncludeInactive)
                //{
                //    users = users.Where(u => !u.IsLocked ?? true);
                //}
                switch (getUsersParams.IdpFilter)
                {
                    case IdpFilterEnum.IdpUsers:
                        users = users.Where(u => u.IsIdpUser == true);
                        break;
                    case IdpFilterEnum.LocalUsers:
                        users = users.Where(u => u.IsIdpUser == false);
                        break;
                    case IdpFilterEnum.NotSet:
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

                filteredUserCount = users.Count();
                if (getUsersParams.PageSize > 0)
                {
                    var pageNumber = getUsersParams.PageNumber > 0 ? getUsersParams.PageNumber - 1 : 0;
                    users = users.Skip(pageNumber * getUsersParams.PageSize).Take(getUsersParams.PageSize);
                }
                resultingUsers = users.ToList();

                var userListDto = resultingUsers.Select(synUser => new User()
                {
                    Id = synUser.Id,
                    Email = synUser.Email,
                    FirstName = synUser.FirstName,
                    LastName = synUser.LastName,
                    UserName = synUser.UserName,
                    IsLocked = synUser.IsLocked,
                    LastLogin = synUser.LastLogin,
                    LdapId = synUser.LdapId,
                    PasswordAttempts = synUser.PasswordAttempts,
                    IsIdpUser = synUser.IsIdpUser,
                    PasswordHash = null,
                    PasswordSalt = null
                }).ToList();

                var returnMetaData = new PagingMetaData<User>
                {
                    TotalCount = userCountTotal,
                    CurrentCount = filteredUserCount,
                    List = userListDto,
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
    }
}