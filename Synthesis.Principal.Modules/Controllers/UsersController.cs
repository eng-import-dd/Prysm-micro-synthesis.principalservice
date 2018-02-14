using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
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
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Exceptions;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Utilities;
using Synthesis.PrincipalService.Validators;

namespace Synthesis.PrincipalService.Controllers
{
    /// <summary>
    /// Represents a controller for User resources.
    /// </summary>
    /// <seealso cref="IUsersController" />
    public class UsersController : IUsersController
    {
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Group> _groupRepository;
        private readonly IValidatorLocator _validatorLocator;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly ILicenseApi _licenseApi;
        private readonly IEmailApi _emailApi;
        private readonly IMapper _mapper;
        private readonly string _deploymentType;
        private const string OrgAdminRoleName = "Org_Admin";
        private const string BasicUserRoleName = "Basic_User";
        private readonly ITenantApi _tenantApi;
        private readonly IPasswordUtility _passwordUtility;
        private readonly IRepository<UserInvite> _userInviteRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="licenseApi"></param>
        /// <param name="passwordUtility"></param>
        /// <param name="mapper"></param>
        /// <param name="deploymentType"></param>
        /// <param name="tenantApi"></param>
        /// <param name="emailApi"></param>
        public UsersController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            ILicenseApi licenseApi,
            IEmailApi emailApi,
            IPasswordUtility passwordUtility,
            IMapper mapper,
            string deploymentType,
            ITenantApi tenantApi)
        {
            _userRepository = repositoryFactory.CreateRepository<User>();
            _groupRepository = repositoryFactory.CreateRepository<Group>();
            _userInviteRepository = repositoryFactory.CreateRepository<UserInvite>();
            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _licenseApi = licenseApi;
            _emailApi = emailApi;
            _passwordUtility = passwordUtility;
            _mapper = mapper;
            _deploymentType = deploymentType;
            _tenantApi = tenantApi;
        }

        public async Task<UserResponse> CreateUserAsync(CreateUserRequest model, Guid tenantId, Guid createdBy)
        {
            //TODO Check for CanManageUserLicenses permission if user.LicenseType != null

            var validationResult = _validatorLocator.Validate<CreateUserRequestValidator>(model);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to create a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var user = _mapper.Map<CreateUserRequest, User>(model);

            if (IsBuiltInOnPremTenant(tenantId))
            {
                _logger.Error("Validation failed while attempting to create a User resource.");
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

        public async Task<UserResponse> GetUserAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(id);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _userRepository.GetItemAsync(id);

            if (result == null)
            {
                _logger.Error($"A User resource could not be found for id {id}");
                throw new NotFoundException($"A User resource could not be found for id {id}");
            }

            return _mapper.Map<User, UserResponse>(result);
        }

        /// <inheritdoc />
        public async Task<PagingMetadata<BasicUserResponse>> GetUsersBasicAsync(Guid tenantId, Guid userId, GetUsersParams getUsersParams)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userListResult = await GetAccountUsersFromDb(tenantId, userId, getUsersParams);
            if (userListResult == null)
            {
                _logger.Error($"Users resource could not be found for input data.");
                throw new NotFoundException($"Users resource could not be found for input data.");
            }

            var basicUserResponse = _mapper.Map<PagingMetadata<User>, PagingMetadata<BasicUserResponse>>(userListResult);
            return basicUserResponse;
        }

        public async Task<PagingMetadata<UserResponse>> GetUsersForAccountAsync(GetUsersParams getUsersParams, Guid tenantId, Guid currentUserId)
        {
            var userIdValidationResult = _validatorLocator.Validate<UserIdValidator>(currentUserId);
            var tenantIdValidationresult = _validatorLocator.Validate<TenantIdValidator>(tenantId);
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
                _logger.Error("Failed to validate the resource id and/or resource while attempting to get a User resource.");
                throw new ValidationFailedException(errors);
            }

            var usersInAccount = await _userRepository.GetItemsAsync(u => u.TenantId == tenantId);
            if (!usersInAccount.Any())
            {
                _logger.Error("Users for the account could not be found");
                throw new NotFoundException($"Users for the account could not be found");
            }

            var users = await GetAccountUsersFromDb(tenantId, currentUserId, getUsersParams);
            var userResponse = _mapper.Map<PagingMetadata<User>, PagingMetadata<UserResponse>>(users);
            return userResponse;

        }

        public async Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest userModel)
        {
            TrimNameOfUser(userModel);
            var errors = new List<ValidationFailure>();
            if (!await IsUniqueUsername(userId, userModel.UserName))
            {
                errors.Add(new ValidationFailure(nameof(userModel.UserName), "A user with that UserName already exists."));
            }
            var userIdValidationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            var userValidationResult = _validatorLocator.Validate<UpdateUserRequestValidator>(userModel);
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
                _logger.Error("Failed to validate the resource id and/or resource while attempting to update a User resource.");
                throw new ValidationFailedException(errors);
            }
            /* Only allow the user to modify the license type if they have permission.  If they do not have permission, ensure the license type has not changed. */
            //TODO: For this GetGroupPermissionsForUser method should be implemented which is in collaboration service.
            var user = _mapper.Map<UpdateUserRequest, User>(userModel);
            return await UpdateUserInDb(user, userId);
        }

        public async Task<CanPromoteUserResponse> CanPromoteUserAsync(string email)
        {
            var validationResult = _validatorLocator.Validate<EmailValidator>(email);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userList = await _userRepository.GetItemsAsync(u => u.Email.Equals(email));
            var existingUser = userList.FirstOrDefault();
            if (existingUser == null)
            {
                _logger.Error("User not found with that email.");
                throw new NotFoundException("User not found with that email.");
            }

            var isValidForPromotion = await IsValidPromotionForTenant(existingUser, existingUser.TenantId);
            if (isValidForPromotion != PromoteGuestResultCode.UserAlreadyPromoted && isValidForPromotion != PromoteGuestResultCode.Failed)
            {
                return new CanPromoteUserResponse
                {
                    ResultCode = CanPromoteUserResultCode.UserCanBePromoted,
                    UserId = existingUser.Id
                };
            }

            _logger.Error("User already in an account");
            return new CanPromoteUserResponse
            {
                ResultCode = CanPromoteUserResultCode.UserAccountAlreadyExists
            };
        }

        public async Task DeleteUserAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(id);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to delete a User resource.");
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

        public async Task<UserResponse> CreateGuestAsync(CreateUserRequest request, Guid tenantId, Guid createdBy)
        {
            // Trim up the names
            request.FirstName = request.FirstName?.Trim();
            request.LastName = request.LastName?.Trim();

            // IDP users need special password setup
            if (request.IsIdpUser.GetValueOrDefault())
            {
                var throwAwayPassword = _passwordUtility.GenerateRandomPassword(64);
                request.Password = throwAwayPassword;
                request.PasswordConfirmation = throwAwayPassword;
            }

            // Validate incoming params
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(GuestCreationRequestValidator), request },
                { typeof(TenantIdValidator), tenantId },
                { typeof(UserIdValidator), createdBy }
            });

            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            // Does a user already exist that uses email or has a username equal to the email?
            var existingUser = await _userRepository.GetItemAsync(x => x.Email == request.Email || x.UserName == request.Email);
            if (existingUser != null)
            {
                throw new UserExistsException($"A user already exists for email = {request.Email}");
            }

            // Has an invite been sent to this user?
            var invite = await _userInviteRepository.GetItemAsync(x => x.Email == request.Email);
            if (invite != null)
            {
                throw new UserNotInvitedException("The user has not been invited yet");
            }

            // Create a new user for the guest
            var user = new User
            {
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                Email = request.Email,
                FirstName = request.FirstName,
                Groups = new List<Guid>(),
                IsIdpUser = request.IsIdpUser,
                IsLocked = false,
                LastAccessDate = DateTime.UtcNow,
                LastName = request.LastName,
                TenantId = tenantId,
                UserName = request.Email
            };
            var result = await _userRepository.CreateItemAsync(user);
            _eventService.Publish(EventNames.UserCreated, result);

            // Done
            return _mapper.Map<User, UserResponse>(result);
        }

        public async Task<PromoteGuestResponse> PromoteGuestUserAsync(Guid userId, Guid tenantId, LicenseType licenseType, bool autoPromote = false)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);

            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to promote guest.");
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
                    _logger.Error(string.Format(ErrorMessages.UserPromotionFailed, userId));
                    throw new PromotionFailedException("Not promoting the user as there are no user licenses available");
                }
            }

            var user = await _userRepository.GetItemAsync(userId);

            var isValidResult = await IsValidPromotionForTenant(user, tenantId);
            if (isValidResult != PromoteGuestResultCode.Success)
            {
                if (isValidResult == PromoteGuestResultCode.UserAlreadyPromoted)
                {
                    return userAccountExistsResult;
                }

                _logger.Error(string.Format(ErrorMessages.UserPromotionFailed, userId));
                throw new PromotionFailedException("User is not valid for promotion");
            }

            var assignGuestResult = await AssignGuestUserToTenant(user, tenantId);
            if (assignGuestResult != PromoteGuestResultCode.Success)
            {
                _logger.Error(string.Format(ErrorMessages.UserPromotionFailed, userId));
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

            var assignLicenseResult = await _licenseApi.AssignUserLicenseAsync(new UserLicenseDto
            {
                AccountId = tenantId.ToString(),
                UserId = userId.ToString(),
                LicenseType = licenseType.ToString()
            });

            if (assignLicenseResult == null || assignLicenseResult.ResultCode != LicenseResponseResultCode.Success)
            {
                // If assignign a license fails, then we must disable the user
                await LockUser(userId, true);
                var errorMessage = $"Assigned user {userId} to tenant {tenantId}, but failed to assign license";

                _logger.Error(errorMessage);
                throw new LicenseAssignmentFailedException(errorMessage, userId);
            }

            await _emailApi.SendWelcomeEmail(new UserEmailRequest { Email = user.Email, FirstName = user.FirstName });

            return new PromoteGuestResponse
            {
                Message = "",
                UserId = userId,
                ResultCode = PromoteGuestResultCode.Success
            };
        }

        public async Task<UserResponse> AutoProvisionRefreshGroupsAsync(IdpUserRequest model, Guid tenantId, Guid createddBy)
        {
            var validationResult = _validatorLocator.Validate<TenantIdValidator>(tenantId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the tenant id.");
                throw new ValidationFailedException(validationResult.Errors);
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
                    _logger.Error(string.Format(ErrorMessages.UserPromotionFailed, userId));
                    throw new PromotionFailedException($"Failed to promote user {userId}");
                }
                await _emailApi.SendWelcomeEmail(new UserEmailRequest { Email = model.EmailId, FirstName = model.FirstName });
            }
            if (model.Groups != null)
            {
                await UpdateIdpUserGroupsAsync(model.UserId.Value, model);
            }
            var result = await _userRepository.GetItemAsync(model.UserId.Value);
            return _mapper.Map<User, UserResponse>(result);
        }

        public async Task<Guid> GetTenantIdByUserEmailAsync(string email)
        {
            var userNameValidationResult = _validatorLocator.Validate<UserNameValidator>(email);

            if (!userNameValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the email address id while attempting to retrieve a tenant id.");
                throw new ValidationFailedException(userNameValidationResult.Errors);
            }


            var result = await _userRepository.GetItemsAsync(u => u.Email.Equals(email) || u.UserName.Equals(email));
            var userWithEmail = result.ToList().FirstOrDefault();

            if (userWithEmail == null)
            {
                _logger.Error($"User resource could not be found for input email {email}.");
                throw new NotFoundException($"User resource could not be found for input email {email}.");
            }

            if (userWithEmail.TenantId == Guid.Empty)
            {
                //TODO: Tenant Management Service Call here to get tenaant Id - Yusuf
                //Legacy Code
                /*
                    * int lastIndexOfAtTheRate = email.LastIndexOf("@");

                    if (lastIndexOfAtTheRate != -1)
                    {
                        string domain = email.Substring(lastIndexOfAtTheRate + 1);

                        accountIdList = from ad in sdc.AccountDomains
                                            where ad.Domain == domain
                                            select ad.AccountID;

                        accountId = await accountIdList.FirstOrDefaultAsync();
                        if (accountId != Guid.Empty)
                            return accountId;
                    */
            }

            return userWithEmail.TenantId;
        }

        private async Task<UserResponse> AutoProvisionUserAsync(IdpUserRequest model, Guid tenantId, Guid createddBy)
        {
            var user = new CreateUserRequest
            {
                Email = model.EmailId,
                UserName = model.EmailId,
                FirstName = model.FirstName,
                LastName = model.LastName,
                LicenseType = LicenseType.UserLicense,
                IsIdpUser = true
            };

            var result = await CreateUserAsync(user, tenantId, createddBy);
            if (result != null && model.Groups != null)
            {
                var groupResult = await UpdateIdpUserGroupsAsync(result.Id.GetValueOrDefault(), model);
                if (groupResult == null)
                {
                    _logger.Error($"An error occurred updating user groups for user {result.Id.GetValueOrDefault()}");
                    throw new IdpUserProvisioningException($"Failed to update Idp user groups for user {result.Id.GetValueOrDefault()}");
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
                    if (currentGroupsResult.Groups.Contains(accountGroup.Id.GetValueOrDefault()))
                    {
                        continue; //Nothing to do if the user is already a member of the group
                    }

                    currentGroupsResult.Groups.Add(accountGroup.Id.GetValueOrDefault());
                    var result = await _userRepository.UpdateItemAsync(userId, currentGroupsResult);
                    return result;
                }
                else
                {
                    //remove the user from the group
                    currentGroupsResult.Groups.Remove(accountGroup.Id.GetValueOrDefault());
                    var result = await _userRepository.UpdateItemAsync(userId, currentGroupsResult);
                    return result;
                }
            }

            return currentGroupsResult;
        }

        public async Task<bool> ResendUserWelcomeEmailAsync(string email, string firstName)
        {
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(EmailValidator), email },
                { typeof(NameValidator), firstName }
            });

            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the guest verification request.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            return await _emailApi.SendWelcomeEmail(new UserEmailRequest { Email = email, FirstName = firstName });
        }

        public async Task<User> GetUserByUserNameOrEmailAsync(string username)
        {
            var unameValidationResult = _validatorLocator.Validate<UserNameValidator>(username);
            if (!unameValidationResult.IsValid)
            {
                _logger.Error("Email/UserName is either empty or invalid.");
                throw new ValidationException("Email/UserName is either empty or invalid");
            }

            var userList = await _userRepository.GetItemsAsync(u => u.Email.Equals(username) || u.UserName.Equals(username));
            var existingUser = userList.ToList().FirstOrDefault();
            if (existingUser == null)
            {
                _logger.Error("User not found with that Email/Username.");
                throw new NotFoundException("User not found with that Email/Username.");
            }

            return existingUser;
        }

        private async Task<bool> IsLicenseAvailable(Guid tenantId, LicenseType licenseType)
        {
            var summary = await _licenseApi.GetTenantLicenseSummaryAsync(tenantId);
            var item = summary.FirstOrDefault(x => string.Equals(x.LicenseName, licenseType.ToString(), StringComparison.CurrentCultureIgnoreCase));

            return item != null && item.TotalAvailable > 0;
        }

        private async Task<PromoteGuestResultCode> IsValidPromotionForTenant(User user, Guid tenantId)
        {
            if (user?.Email == null)
            {
                return PromoteGuestResultCode.Failed;
            }

            if (user.TenantId != Guid.Empty)
            {
                return PromoteGuestResultCode.UserAlreadyPromoted;
            }

            var domain = user.Email.Substring(user.Email.IndexOf('@') + 1);
            var tenantEmailDomains = await CommonApiUtility.GetTenantDomains(_tenantApi, tenantId);

            return tenantEmailDomains.Contains(domain) ? PromoteGuestResultCode.Success : PromoteGuestResultCode.Failed;
        }

        private async Task<PromoteGuestResultCode> AssignGuestUserToTenant(User user, Guid tenantId)
        {
            user.TenantId = tenantId;
            await _userRepository.UpdateItemAsync(user.Id.GetValueOrDefault(), user);

            _eventService.Publish(new ServiceBusEvent<Guid>
            {
                Name = EventNames.UserPromoted,
                Payload = user.Id.GetValueOrDefault()
            });

            return PromoteGuestResultCode.Success;
        }

        public async Task<PagingMetadata<UserResponse>> GetGuestUsersForTenantAsync(Guid tenantId, GetUsersParams getGuestUsersParams)
        {
            var validationResult = _validatorLocator.Validate<TenantIdValidator>(tenantId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the tenant id.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var criteria = new List<Expression<Func<User, bool>>>();
            Expression<Func<User, string>> orderBy;
            criteria.Add(u => u.TenantId == Guid.Empty);

            var tenantemailDomain = await CommonApiUtility.GetTenantDomains(_tenantApi, tenantId);


            criteria.Add(u => tenantemailDomain.Contains(u.EmailDomain));

            if (!string.IsNullOrEmpty(getGuestUsersParams.SearchValue))
            {
                criteria.Add(x =>
                                    x != null &&
                                    (x.FirstName.ToLower() + " " + x.LastName.ToLower()).Contains(
                                                                                                getGuestUsersParams.SearchValue.ToLower()) ||
                                    x != null && x.Email.ToLower().Contains(getGuestUsersParams.SearchValue.ToLower()) ||
                                    x != null && x.UserName.ToLower().Contains(getGuestUsersParams.SearchValue.ToLower()));
            }
            if (string.IsNullOrWhiteSpace(getGuestUsersParams.SortColumn))
            {
                orderBy = u => u.FirstName;
            }
            else
            {
                switch (getGuestUsersParams.SortColumn.ToLower())
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
                        orderBy = u => u.FirstName;
                        break;
                }
            }

            var queryparams = new OrderedQueryParameters<User, string>()
            {
                Criteria = criteria,
                OrderBy = orderBy,
                SortDescending = getGuestUsersParams.SortDescending,
                ContinuationToken = getGuestUsersParams.ContinuationToken,
                ChunkSize = getGuestUsersParams.PageSize
            };

            var guestUsersInTenantResult = await _userRepository.GetOrderedPaginatedItemsAsync(queryparams);
            var guestUsersInTenant = guestUsersInTenantResult.Items.ToList();
            var filteredUserCount = guestUsersInTenant.Count;
            var returnMetaData = new PagingMetadata<UserResponse>
            {
                CurrentCount = filteredUserCount,
                List = _mapper.Map<List<User>, List<UserResponse>>(guestUsersInTenant),
                SearchValue = getGuestUsersParams.SearchValue,
                ContinuationToken = guestUsersInTenantResult.ContinuationToken,
                IsLastChunk = guestUsersInTenantResult.IsLastChunk
            };

            return returnMetaData;
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
                validationErrors.Add(new ValidationFailure(nameof(user.Email), "A user with that email address already exists."));
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
                _logger.Error($"Validation failed creating user {user.Id}");
                throw new ValidationFailedException(validationErrors);
            }

            user.Groups = new List<Guid>();
            var basicUserGroupId = await GetBuiltInGroupId(user.TenantId, BasicUserRoleName);
            if (basicUserGroupId != null)
            {
                user.Groups.Add(basicUserGroupId.Value);
            }

            var result = await _userRepository.CreateItemAsync(user);

            return result;
        }

        private async Task<UserResponse> UpdateUserInDb(User user, Guid id)
        {
            var existingUser = await _userRepository.GetItemAsync(id);
            if (existingUser == null)
            {
                _logger.Error($"An error occurred retrieving user {id}");
                throw new NotFoundException($"A User resource could not be found for id {id}");
            }

            if (string.IsNullOrEmpty(user.UserName))
            {
                user.UserName = existingUser.UserName;
            }
            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Email = user.Email;
            existingUser.IsLocked = user.IsLocked;
            existingUser.IsIdpUser = user.IsIdpUser;

            try
            {
                await _userRepository.UpdateItemAsync(id, existingUser);
            }
            catch (Exception ex)
            {
                _logger.Error("User Updation failed", ex);
                throw;
            }

            return _mapper.Map<User, UserResponse>(existingUser);
        }
        private async Task AssignUserLicense(User user, LicenseType? licenseType)
        {
            if (user.Id == null || user.Id == Guid.Empty)
            {
                var errorMessage = "User Id is required for assiging license";

                _logger.Error(errorMessage);
                throw new ArgumentException(errorMessage);
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
                    await _emailApi.SendWelcomeEmail(new UserEmailRequest { Email = user.Email, FirstName = user.FirstName });
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error assigning license to user", ex);
            }
            /* If a license could not be obtained lock the user that was just created. */
            await LockUser(user.Id.Value, true);
            user.IsLocked = true;

            //Intimate the Org Admin of the user's teanant about locked user
            var orgAdmins = await GetTenantAdminsByIdAsync(user.TenantId);

            if (orgAdmins.Count > 0)
            {
                var orgAdminReq = _mapper.Map<List<User>, List<UserEmailRequest>>(orgAdmins);
                await _emailApi.SendUserLockedMail(new LockUserRequest{OrgAdmins = orgAdminReq,UserEmail = user.Email,UserFullName = $"{user.FirstName} {user.LastName}" });
            }
        }

        private async Task<List<User>> GetTenantAdminsByIdAsync(Guid userTenantId)
        {
            var adminGroupId = await GetBuiltInGroupId(userTenantId, OrgAdminRoleName);
            if (adminGroupId != null)
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
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to delete a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }
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

        #region User Group Methods

        public async Task<User> CreateUserGroupAsync(CreateUserGroupRequest model, Guid tenantId, Guid userId)
        {
            var validationErrors = new List<ValidationFailure>();

            var validationResult = _validatorLocator.Validate<CreateUserGroupRequestValidator>(model);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to create a User group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            // We need to verify that the group they want to add a user to is a group within their account
            // and we also need to verify that the user they want to add to a group is also within their account.
            var existingUser = await _userRepository.GetItemAsync(model.UserId);

            if (existingUser == null)
            {
                validationErrors.Add(new ValidationFailure(nameof(existingUser), $"Unable to find the user with the id {model.UserId}"));
                throw new ValidationFailedException(validationErrors);
            }

            var userTenantId = existingUser.TenantId;

            if (userTenantId == Guid.Empty || userTenantId != tenantId)
            {
                throw new InvalidOperationException();
            }

            //TODO: User Permission check here - Yusuf
            #region Legacy code - Will be removed after implementation
            /*
            * if (UserId == accessedUserId)
                return ResultCode.Success;

            if (accessedUserId == Guid.Empty || CollaborationService.GetUserById(accessedUserId).Payload == null)
                return ResultCode.RecordNotFound;

            * var userPermissions = new Lazy<List<PermissionEnum>>(InitUserPermissionsList);

               if (userPermissions.Value.Contains(requiredPermission) && AccountId == CollaborationService.GetAccountIdForUserId(accessedUserId).Payload)
                return ResultCode.Success;

               return ResultCode.Unauthorized;
            */


            #endregion

            //TODO: Reject adds to the SuperAdmin group if requesting user isn't a SuperAdmin - Yusuf
            #region Legacy code - Will be removed after implementation
            /*
             *
             *  if (userGroupDto.GroupId == CollaborationService.SuperAdminGroupId && !CollaborationService.IsSuperAdmin(UserId))
                {
                    return new ServiceResult<UserGroupDTO>
                    {
                        Payload = null,
                        Message = "CreateUserGroup",
                        ResultCode = ResultCode.RecordNotFound // Don't send unauth because we don't want to expose that the superadmin group exists, send NotFound instead
                    };
                }
             */
            #endregion

            if (existingUser.Groups.Contains(model.GroupId))
            {
                validationErrors.Add(new ValidationFailure(nameof(existingUser), $"User group {model.GroupId} already exists for User id {model.UserId}"));
                throw new ValidationFailedException(validationErrors);
            }

            return await CreateUserGroupInDb(model, existingUser);
        }

        public async Task<List<Guid>> GetGroupUsersAsync(Guid groupId, Guid tenantId, Guid userId)
        {
            var validationResult = _validatorLocator.Validate<GroupIdValidator>(groupId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a User group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _userRepository.GetItemsAsync(u => u.Groups.Contains(groupId) && u.TenantId == tenantId);

            if (result == null)
            {
                _logger.Error($"A User group resource could not be found for id {groupId}");
                throw new NotFoundException($"A User group resource could not be found for id {groupId}");
            }

            //TODO: Access Checks - Yusuf
            //if (groupId == CollaborationService.SuperAdminGroupId && !CollaborationService.IsSuperAdmin(UserId))

            return result.Select(user => user.Id.GetValueOrDefault()).ToList();
        }

        public async Task<List<Guid>> GetGroupsForUserAsync(Guid userId)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a User group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userWithGivenUserId = await _userRepository.GetItemAsync(userId);
            if (userWithGivenUserId != null)
            {
                return userWithGivenUserId.Groups;
            }

            throw new NotFoundException($"A User group resource could not be found for id {userId}");
        }

        public async Task<bool> RemoveUserFromPermissionGroupAsync(Guid userId, Guid groupId, Guid currentUserId)
        {
            var userIdValidationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            var groupIdValidationResult = _validatorLocator.Validate<GroupIdValidator>(groupId);
            var countOfSuperAdmins = 0;
            if (!userIdValidationResult.IsValid || !groupIdValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to remove the User from group.");
                throw new ValidationFailedException(userIdValidationResult.Errors);
            }

            if (!IsSuperAdmin(currentUserId))
            {
                return false;
            }

            var user = await _userRepository.GetItemAsync(userId);
            if (user == null)
            {
                throw new DocumentNotFoundException("User doesn't exist with userId: " + userId);
            }

            var groupUsers = await _userRepository.GetItemsAsync(u => u.Groups.Contains(groupId));
            countOfSuperAdmins += groupUsers.Count(u => IsSuperAdmin(u.Id ?? Guid.Empty) && !u.IsLocked);
            if (countOfSuperAdmins <= 1)
            {
                _logger.Error("Cannot delete the last non locked super admin of this group.");
                return false;
            }

            user.Groups.Remove(groupId);
            await _userRepository.UpdateItemAsync(userId, user);
            return true;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetUsersByIdsAsync(IEnumerable<Guid> userIds)
        {
            var validationResult = _validatorLocator.Validate<GetUsersByIdValidator>(userIds);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userIdList = userIds.ToList();

            return await _userRepository.GetItemsAsync(u => userIdList.Contains(u.Id ?? Guid.Empty));
        }

        // ReSharper disable once UnusedParameter.Local
        private bool IsSuperAdmin(Guid userId)
        {
            //var userGroups = GetUserGroupsForUser(userId).Payload;
            //return userGroups.Any(x => x.GroupId.Equals(SuperAdminGroupId));
            //TODO: Put code here to check User Group - Charan
            return true;
        }

        private async Task<User> CreateUserGroupInDb(CreateUserGroupRequest createUserGroupRequest, User existingUser)
        {
            try
            {
                existingUser.Groups.Add(createUserGroupRequest.GroupId);
                await _userRepository.UpdateItemAsync(createUserGroupRequest.UserId, existingUser);
                return existingUser;
            }
            catch (Exception ex)
            {
                _logger.Error("", ex);
                throw;
            }
        }

        #endregion

        #region User License Type Method

        public async Task<LicenseType?> GetLicenseTypeForUserAsync(Guid userId, Guid tenantId)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a User license type resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userTenantId = (await _userRepository.GetItemAsync(userId)).TenantId;

            if (userTenantId == Guid.Empty)
            {
                throw new NotFoundException($"A User resource could not be found for id {userId}");
            }

            if (userTenantId != tenantId)
            {
                throw new InvalidOperationException();
            }

            //TODO: Check valid access for user here - Yusuf
            //Code required to check permission to make the API call IF the requesting user is not the requested user
            // var userPermissions = new Lazy<List<PermissionEnum>>(InitUserPermissionsList);
            //if (userPermissions.Value.Contains(requiredPermission)

            return await GetUserLicenseType(userId, tenantId);
        }

        #endregion

        private async Task<LicenseType?> GetUserLicenseType(Guid userId, Guid accountId)
        {
            List<UserLicenseDto> userLicenses;

            userLicenses = (await _licenseApi.GetUserLicenseDetailsAsync(accountId, userId)).LicenseAssignments;

            if (userLicenses == null || userLicenses.Count == 0)
            {
                return null;
            }

            return (LicenseType)Enum.Parse(typeof(LicenseType), userLicenses[0].LicenseType);
        }

        private async Task<bool> UpdateLockUserDetailsInDb(Guid id, bool isLocked)
        {
            var validationErrors = new List<ValidationFailure>();
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
                    /* If the user is being locked, remove the associated license. */
                    var removeUserLicenseResult = await _licenseApi.ReleaseUserLicenseAsync(licenseRequestDto);

                    if (removeUserLicenseResult.ResultCode != LicenseResponseResultCode.Success)
                    {
                        validationErrors.Add(new ValidationFailure(nameof(existingUser), "Unable to remove license for the user"));
                    }
                }
                else // unlock
                {
                    /* If not locked, request a license. */
                    var assignUserLicenseResult = await _licenseApi.AssignUserLicenseAsync(licenseRequestDto);

                    if (assignUserLicenseResult.ResultCode != LicenseResponseResultCode.Success)
                    {

                        validationErrors.Add(new ValidationFailure(nameof(existingUser), "Unable to assign license to the user"));
                    }
                }

                if (validationErrors.Any())
                {
                    _logger.Error($"Validation failed updating lock state for user {id}");
                    throw new ValidationFailedException(validationErrors);
                }

                await LockUser(id, isLocked);

                return true;
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
            if (getUsersParams == null)
            {
                getUsersParams = new GetUsersParams
                {
                    SearchValue = "",
                    OnlyCurrentUser = false,
                    IncludeInactive = false,
                    SortColumn = "FirstName",
                    SortDescending = false,
                    IdpFilter = IdpFilter.All
                };
            }
            var criteria = new List<Expression<Func<User, bool>>>();
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
                Criteria = criteria,
                OrderBy = orderBy,
                SortDescending = getUsersParams.SortDescending,
                ContinuationToken = getUsersParams.ContinuationToken ?? ""
            };
            var usersInAccountsResult = await _userRepository.GetOrderedPaginatedItemsAsync(queryparams);

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