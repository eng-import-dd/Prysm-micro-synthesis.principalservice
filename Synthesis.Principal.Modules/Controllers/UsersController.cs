using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Castle.Core.Internal;
using Castle.Core.Resource;
using FluentValidation;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.EventBus;
using Synthesis.Http.Microservice;
using Synthesis.IdentityService.InternalApi.Api;
using Synthesis.IdentityService.InternalApi.Models;
using Synthesis.License.Manager.Interfaces;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Synthesis.PolicyEvaluator.Permissions;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Email;
using Synthesis.PrincipalService.Exceptions;
using Synthesis.PrincipalService.Extensions;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Enums;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Services;
using Synthesis.PrincipalService.Validators;
using Synthesis.TenantService.InternalApi.Api;

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
        private readonly ITenantApi _tenantApi;
        private readonly IIdentityUserApi _identityUserApi;
        private readonly IUserSearchBuilder _searchBuilder;
        private readonly IQueryRunner<User> _queryRunner;
        private readonly IEmailSendingService _emailSendingService;
        private readonly IPolicyEvaluator _policyEvaluator;
        private readonly ISuperAdminService _superAdminService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="licenseApi">The license API.</param>
        /// <param name="emailApi">The email API.</param>
        /// <param name="emailSendingService">The email sending service.</param>
        /// <param name="mapper">The mapper.</param>
        /// <param name="deploymentType">Type of the deployment.</param>
        /// <param name="queryRunner"></param>
        /// <param name="tenantApi">The tenant API.</param>
        /// <param name="searchBuilder"></param>
        /// <param name="policyEvaluator"></param>
        /// <param name="superAdminService"></param>
        /// <param name="identityUserApi"></param>
        public UsersController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            ILicenseApi licenseApi,
            IEmailApi emailApi,
            IEmailSendingService emailSendingService,
            IMapper mapper,
            string deploymentType,
            IUserSearchBuilder searchBuilder,
            IQueryRunner<User> queryRunner,
            ITenantApi tenantApi,
            IPolicyEvaluator policyEvaluator,
            ISuperAdminService superAdminService,
            IIdentityUserApi identityUserApi)
        {
            _userRepository = repositoryFactory.CreateRepository<User>();
            _groupRepository = repositoryFactory.CreateRepository<Group>();
            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _licenseApi = licenseApi;
            _emailApi = emailApi;
            _mapper = mapper;
            _deploymentType = deploymentType;
            _searchBuilder = searchBuilder;
            _queryRunner = queryRunner;
            _tenantApi = tenantApi;
            _identityUserApi = identityUserApi;
            _policyEvaluator = policyEvaluator;
            _emailSendingService = emailSendingService;
            _superAdminService = superAdminService;
        }

        public async Task<User> CreateUserAsync(CreateUserRequest model, Guid createdBy, ClaimsPrincipal principal)
        {
            var validationResult = _validatorLocator.Validate<CreateUserRequestValidator>(model);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to create a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            if (!await CanManageUserLicensesAsync(principal))
            {
                model.LicenseType = LicenseType.Default;
            }

            // Convert to non-null Guid. If had been null or empty, would fail validation before this point.
            var tenantId = model.TenantId.GetValueOrDefault();

            if (IsBuiltInOnPremTenant(tenantId))
            {
                _logger.Error("Validation failed while attempting to create a User resource.");
                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(model.TenantId), "Users cannot be created under provisioning tenant") });
            }

            var newUser = new User
            {
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = model.Email?.ToLower(),
                EmailVerificationId = Guid.NewGuid(),
                Username = model.Username?.ToLower(),
                Groups = model.Groups ?? new List<Guid>(),
                IdpMappedGroups = model.IdpMappedGroups,
                Id = model.Id,
                IsIdpUser = model.IsIdpUser,
                IsLocked = false,
                LastAccessDate = DateTime.UtcNow,
                LdapId = model.LdapId,
                LicenseType = model.LicenseType
            };

            newUser.Groups.AddRange(new List<Guid>
            {
                (await GetBuiltInGroupId(tenantId, GroupType.Default)).GetValueOrDefault(),
                (await GetBuiltInGroupId(tenantId, GroupType.Basic)).GetValueOrDefault()
            });

            var result = await CreateUserInDb(newUser, tenantId);

            if (result.Id == null)
            {
                throw new ResourceException("User was incorrectly created with a null Id");
            }

            if (model.UserType == UserType.Enterprise)
            {
                await AssignUserLicense(result, newUser.LicenseType, tenantId, true);
            }

            var response = await _tenantApi.AddUserToTenantAsync(tenantId, (Guid)result.Id);
            if (!response.IsSuccess())
            {
                await _userRepository.DeleteItemAsync((Guid)result.Id);
                throw new TenantMappingException($"Adding the user to the tenant with Id {tenantId} failed. The user was removed from the database");
            }

            var setPasswordResponse = await _identityUserApi.SetPasswordAsync(new IdentityUser
            {
                Password = model.Password,
                PasswordHash = model.PasswordHash,
                PasswordSalt = model.PasswordSalt,
                UserId = (Guid)result.Id
            });

            if (!setPasswordResponse.IsSuccess())
            {
                await _userRepository.DeleteItemAsync((Guid)result.Id);

                var removeUserResponse = await _tenantApi.RemoveUserFromTenantAsync(tenantId, (Guid)result.Id);
                if (removeUserResponse.ResponseCode != HttpStatusCode.NoContent)
                {
                    throw new IdentityPasswordException($"Setting the user's password failed. The user entry was removed from the database, but the attempt to remove the user with id {(Guid)result.Id} from their tenant with id {model.TenantId} failed.");
                }

                throw new IdentityPasswordException("Setting the user's password failed. The user was removed from the database and from the tenant they were mapped to.");
            }

            await _eventService.PublishAsync(EventNames.UserCreated, result);

            return result;
        }

        public async Task<User> GetUserAsync(Guid id)
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

            return result;
        }

        public async Task<IEnumerable<UserNames>> GetNamesForUsers(IEnumerable<Guid> userIds)
        {
            var validationResult = _validatorLocator.Validate<GeUserNamesByIdsValidator>(userIds);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the userids");
                throw new ValidationFailedException(validationResult.Errors);
            }

            IEnumerable<Guid?> nullableUserIds = userIds.Select(x => new Guid?(x));
            var result = await _userRepository.GetItemsAsync(user => nullableUserIds.Contains(user.Id));

            return result.Select(x => new UserNames()
            {
                FirstName = x.FirstName,
                LastName = x.LastName,
                Id = x.Id.ToGuid()
            });
        }

        /// <inheritdoc />
        public async Task<PagingMetadata<BasicUser>> GetUsersBasicAsync(Guid tenantId, Guid userId, UserFilteringOptions userFilteringOptions)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userListResult = await GetTenantUsersFromDb(tenantId, userId, userFilteringOptions);
            if (userListResult == null)
            {
                _logger.Error($"Users resource could not be found for input data.");
                throw new NotFoundException($"Users resource could not be found for input data.");
            }

            var basicUserResponse = _mapper.Map<PagingMetadata<User>, PagingMetadata<BasicUser>>(userListResult);
            return basicUserResponse;
        }

        public async Task<int> GetUserCountAsync(Guid tenantId, Guid userId, UserFilteringOptions userFilteringOptions)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userListResult = await GetTenantUsersFromDb(tenantId, userId, userFilteringOptions);
            if (userListResult == null)
            {
                return 0;
            }

            return userListResult.FilteredRecords;
        }

        public async Task<PagingMetadata<User>> GetUsersForTenantAsync(UserFilteringOptions userFilteringOptions, Guid tenantId, Guid currentUserId)
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

            var tenantUsers = await _tenantApi.GetUserIdsByTenantIdAsync(tenantId);
            if (!tenantUsers.IsSuccess())
            {
                throw new Exception("Unable to find users for the tenant id");
            }

            if (!(tenantUsers.Payload?.Any()).GetValueOrDefault())
            {
                return new PagingMetadata<User>
                {
                    IsLastChunk = true,
                    FilteredRecords = 0,
                    List = new List<User>(),
                    SearchValue = userFilteringOptions.SearchValue,
                    SortColumn = userFilteringOptions.SortColumn,
                    SortDescending = userFilteringOptions.SortDescending,
                    TotalRecords = 0
                };
            }

            var usersInTenant = await _userRepository.GetItemsAsync(u => tenantUsers.Payload.Contains(u.Id ?? Guid.Empty));
            if (!usersInTenant.Any())
            {
                _logger.Error("Users for the tenant could not be found");
                throw new NotFoundException($"Users for the tenant could not be found");
            }

            return await GetTenantUsersFromDb(tenantId, currentUserId, userFilteringOptions);
        }

        public async Task<User> UpdateUserAsync(Guid userId, User userModel, ClaimsPrincipal claimsPrincipal)
        {
            TrimNameOfUser(userModel);
            var errors = new List<ValidationFailure>();
            if (!await IsUniqueUsername(userId, userModel.Username))
            {
                errors.Add(new ValidationFailure(nameof(userModel.Username), "A user with that Username already exists."));
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

            return await UpdateUserInDb(userModel, userId, claimsPrincipal);
        }

        public async Task<CanPromoteUser> CanPromoteUserAsync(string email, Guid tenantId)
        {
            var validationResult = _validatorLocator.Validate<EmailValidator>(email);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            email = email?.ToLower();
            var userList = await _userRepository.GetItemsAsync(u => u.Email.Equals(email));
            var existingUser = userList.FirstOrDefault();
            if (existingUser == null)
            {
                _logger.Error("User not found with that email.");
                throw new NotFoundException("User not found with that email.");
            }

            var isValidForPromotion = await IsValidPromotionForTenant(existingUser, tenantId);
            if (isValidForPromotion != CanPromoteUserResultCode.UserAccountAlreadyExists && isValidForPromotion != CanPromoteUserResultCode.PromotionNotPossible)
            {
                return new CanPromoteUser
                {
                    ResultCode = CanPromoteUserResultCode.UserCanBePromoted,
                    UserId = existingUser.Id
                };
            }

            _logger.Error("User already in a tenant");
            return new CanPromoteUser
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

        public async Task<CreateGuestUserResponse> CreateGuestUserAsync(CreateUserRequest model)
        {
            // Trim up the names
            model.FirstName = model.FirstName?.Trim();
            model.LastName = model.LastName?.Trim();
            model.Email = model.Email?.ToLower();
            model.Username = model.Username?.ToLower();

            // Validate the input
            var validationResult = _validatorLocator.Validate<CreateGuestUserRequestValidator>(model);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            // Does a user already exist that uses email or has a username equal to the email?
            var existingUser = await _userRepository.GetItemAsync(x => x.Email == model.Email || x.Username == model.Email);
            if (existingUser != null)
            {
                throw new UserExistsException($"A user already exists for email = {model.Email}");
            }

            // Create a new user for the guest
            var user = new User
            {
                CreatedDate = DateTime.UtcNow,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                EmailVerificationId = Guid.NewGuid(),
                Username = model.Username,
                Id = model.Id,
                IsEmailVerified = false,
                IsIdpUser = model.IsIdpUser,
                IsLocked = false,
                LastAccessDate = DateTime.UtcNow,
                Groups = new List<Guid>()
            };

            user.Groups.AddRange(new List<Guid>
            {
                (await GetBuiltInGroupId(null, GroupType.Default)).GetValueOrDefault()
            });

            // Create the user in the DB
            var guestUser = await _userRepository.CreateItemAsync(user);

            try
            {
                // Set the password
                var setPasswordResponse = await _identityUserApi.SetPasswordAsync(new IdentityUser { Password = model.Password, UserId = guestUser.Id.GetValueOrDefault() });
                if (!setPasswordResponse.IsSuccess())
                {
                    throw new IdentityPasswordException("Setting the user's password failed. The user was removed from the database and from the tenant they were mapped to.");
                }
            }
            catch (Exception)
            {
                // ReSharper disable once PossibleInvalidOperationException
                await _userRepository.DeleteItemAsync((Guid)guestUser.Id);
                throw;
            }

            var createGuestUserResponse = new CreateGuestUserResponse
            {
                User = guestUser,
                IsEmailVerificationRequired = model.EmailVerificationRequired
            };

            _eventService.Publish(EventNames.UserCreated, guestUser);

            // Send the verification email
            var emailResult = await _emailSendingService.SendGuestVerificationEmailAsync(model.FirstName,
                                                                                         model.Email,
                                                                                         model.Redirect,
                                                                                         guestUser.EmailVerificationId);
            if (!emailResult.IsSuccess())
            {
                _logger.Error($"Sending guest verification email failed. ReasonPhrase={emailResult.ReasonPhrase} ErrorResponse={emailResult.ErrorResponse}");
            }
            else
            {
                guestUser.VerificationEmailSentAt = DateTime.UtcNow;
                // ReSharper disable once PossibleInvalidOperationException
                await _userRepository.UpdateItemAsync((Guid)guestUser.Id, guestUser);
            }

            return createGuestUserResponse;
        }

        public async Task SendGuestVerificationEmailAsync(GuestVerificationEmailRequest request)
        {
            // Validate the input
            var validationResult = _validatorLocator.Validate<GuestVerificationEmailRequestValidator>(request);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var user = await _userRepository.GetItemAsync(x => x.Email == request.Email);

            if (user == null)
            {
                throw new NotFoundException($"No user found with the email: {request.Email}");
            }

            if (user.IsEmailVerified != null && (bool)user.IsEmailVerified)
            {
                throw new EmailAlreadyVerifiedException($"User with email: {request.Email} has already been verified");
            }

            if (user.VerificationEmailSentAt > DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(1)))
            {
                throw new EmailRecentlySentException($"Verification email has been sent to {request.Email} within the last minute");
            }

            var response = await _emailSendingService.SendGuestVerificationEmailAsync(request.FirstName,
                                                                                      request.Email,
                                                                                      request.Redirect,
                                                                                      user.EmailVerificationId);
            if (response.IsSuccess())
            {
                user.VerificationEmailSentAt = DateTime.UtcNow;

                // ReSharper disable once PossibleInvalidOperationException
                await _userRepository.UpdateItemAsync((Guid)user.Id, user);
            }
        }

        public async Task<CanPromoteUserResultCode> PromoteGuestUserAsync(Guid userId, Guid tenantId, LicenseType licenseType, ClaimsPrincipal claimsPrincipal, bool autoPromote = false)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);

            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to promote guest.");
                throw new ValidationFailedException(validationResult.Errors);
            }

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
            if (isValidResult != CanPromoteUserResultCode.UserCanBePromoted)
            {
                if (isValidResult == CanPromoteUserResultCode.UserAccountAlreadyExists)
                {
                    return CanPromoteUserResultCode.UserAccountAlreadyExists;
                }

                _logger.Error(string.Format(ErrorMessages.UserPromotionFailed, userId));
                throw new PromotionFailedException("User is not valid for promotion");
            }

            var assignGuestResult = await AssignGuestUserToTenant(user, tenantId);
            if (assignGuestResult != CanPromoteUserResultCode.UserCanBePromoted)
            {
                _logger.Error(string.Format(ErrorMessages.UserPromotionFailed, userId));
                throw new PromotionFailedException($"Failed to assign Guest User {userId} to tenant {tenantId}");
            }

            if (!autoPromote && licenseType != LicenseType.Default && !await CanManageUserLicensesAsync(claimsPrincipal))
            {
                licenseType = LicenseType.Default;
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

            await SendWelcomeEmailAsync(user.Email, user.FirstName);

            return CanPromoteUserResultCode.UserCanBePromoted;
        }

        public async Task<User> AutoProvisionRefreshGroupsAsync(IdpUserRequest model, Guid tenantId, Guid createdBy, ClaimsPrincipal claimsPrincipal)
        {
            var validationResult = _validatorLocator.Validate<TenantIdValidator>(tenantId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the tenant id.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            if (model.UserId == null || model.UserId == Guid.Empty)
            {
                return await AutoProvisionUserAsync(model, tenantId, createdBy, claimsPrincipal);
            }

            var userId = model.UserId.Value;
            if (model.IsGuestUser)
            {
                try
                {
                    await PromoteGuestUserAsync(userId, model.TenantId, LicenseType.UserLicense, claimsPrincipal, true);
                    await SendWelcomeEmailAsync(model.EmailId, model.FirstName);
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format(ErrorMessages.UserPromotionFailed, userId), ex);
                    throw new PromotionFailedException($"Failed to promote user {userId}");
                }
            }

            if (model.Groups != null)
            {
                await UpdateIdpUserGroupsAsync(model.UserId.Value, model);
            }
            var result = await _userRepository.GetItemAsync(model.UserId.Value);
            return result;
        }

        private async Task<User> AutoProvisionUserAsync(IdpUserRequest model, Guid tenantId, Guid createddBy, ClaimsPrincipal claimsPrincipal)
        {
            var createUserRequest = new CreateUserRequest
            {
                Email = model.EmailId,
                Username = model.EmailId,
                FirstName = model.FirstName,
                LastName = model.LastName,
                LicenseType = LicenseType.UserLicense,
                IsIdpUser = true,
                Password = model.Password,
                TenantId = tenantId,
                UserType = UserType.Enterprise
            };

            var result = await CreateUserAsync(createUserRequest, createddBy, claimsPrincipal);
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
            var tenantGroupsResult = await _groupRepository.GetItemsAsync(g => g.TenantId == model.TenantId);

            var tenantGroups = tenantGroupsResult as IList<Group> ?? tenantGroupsResult.ToList();
            foreach (var tenantGroup in tenantGroups)
            {
                if (model.IdpMappedGroups?.Contains(tenantGroup.Name) == false)
                {
                    //in case IdpMappedGroups is specified, skip updating group memebership if this group is not mapped
                    continue;
                }

                if (model.Groups.Contains(tenantGroup.Id.ToString()))
                {
                    //Add the user to the group
                    if (currentGroupsResult.Groups.Contains(tenantGroup.Id.GetValueOrDefault()))
                    {
                        continue; //Nothing to do if the user is already a member of the group
                    }
                    currentGroupsResult.Groups.Add(tenantGroup.Id.GetValueOrDefault());
                }
                else
                {
                    //remove the user from the group
                    currentGroupsResult.Groups.Remove(tenantGroup.Id.GetValueOrDefault());
                }
            }

            await _userRepository.UpdateItemAsync(userId, currentGroupsResult);

            return currentGroupsResult;
        }

        public async Task<User> GetUserByUserNameOrEmailAsync(string username)
        {
            var unameValidationResult = username.Contains("@") ? _validatorLocator.Validate<EmailValidator>(username) : _validatorLocator.Validate<UserNameValidator>(username);

            if (!unameValidationResult.IsValid)
            {
                _logger.Error("Email/Username is either empty or invalid.");
                throw new ValidationException("Email/Username is either empty or invalid");
            }

            username = username.ToLower();
            var userList = await _userRepository.GetItemsAsync(u => u.Email == username || u.Username == username);
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

        private async Task<CanPromoteUserResultCode> IsValidPromotionForTenant(User user, Guid tenantId)
        {
            if (user?.Email == null)
            {
                return CanPromoteUserResultCode.PromotionNotPossible;
            }

            if (user.Id != null)
            {
                var result = await _tenantApi.GetTenantIdsForUserIdAsync(user.Id ?? Guid.Empty);

                if (!result.Payload.IsNullOrEmpty() && result.Payload.Contains(tenantId))
                {
                    return CanPromoteUserResultCode.UserAccountAlreadyExists;
                }
            }

            var domain = user.Email.Substring(user.Email.IndexOf('@') + 1);
            var tenantDomainsResponse = await _tenantApi.GetTenantDomainsAsync(tenantId);
            if (!tenantDomainsResponse.IsSuccess())
            {
                return CanPromoteUserResultCode.PromotionNotPossible;
            }

            return tenantDomainsResponse.Payload.Any(d => d.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
                ? CanPromoteUserResultCode.UserCanBePromoted
                : CanPromoteUserResultCode.PromotionNotPossible;
        }

        private async Task<CanPromoteUserResultCode> AssignGuestUserToTenant(User user, Guid tenantId)
        {
            if (user.Id != null)
            {
                var result = await _tenantApi.AddUserToTenantAsync(tenantId, (Guid)user.Id);

                if (!result.IsSuccess())
                {
                    return CanPromoteUserResultCode.PromotionNotPossible;
                }

                _eventService.Publish(new ServiceBusEvent<Guid>
                {
                    Name = EventNames.UserPromoted,
                    Payload = user.Id.GetValueOrDefault()
                });

                return CanPromoteUserResultCode.UserCanBePromoted;
            }

            return CanPromoteUserResultCode.PromotionNotPossible;
        }

        public async Task<PagingMetadata<User>> GetGuestUsersForTenantAsync(Guid tenantId, UserFilteringOptions userFilteringOptions)
        {
            var validationResult = _validatorLocator.Validate<TenantIdValidator>(tenantId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the tenant id.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var criteria = new List<Expression<Func<User, bool>>>();
            Expression<Func<User, string>> orderBy;

            var userIdsInTenant = await _tenantApi.GetUserIdsByTenantIdAsync(tenantId);
            if (userIdsInTenant.IsSuccess())
            {
                var userids = userIdsInTenant.Payload.ToList();
                criteria.Add(u => !userids.Contains(u.Id.Value));
            }

            var tenantDomainsResponse = await _tenantApi.GetTenantDomainsAsync(tenantId);
            if (tenantDomainsResponse.IsSuccess() && tenantDomainsResponse.Payload.Any())
            {
                var tenantDomains = tenantDomainsResponse.Payload.Select(d => d.Domain).ToList();
                criteria.Add(u => tenantDomains.Contains(u.EmailDomain));
            }

            if (!string.IsNullOrEmpty(userFilteringOptions.SearchValue))
            {
                criteria.Add(x =>
                    x != null &&
                    (x.FirstName.ToLower() + " " + x.LastName.ToLower()).Contains(
                        userFilteringOptions.SearchValue.ToLower()) ||
                    x != null && x.Email.ToLower().Contains(userFilteringOptions.SearchValue.ToLower()) ||
                    x != null && x.Username.ToLower().Contains(userFilteringOptions.SearchValue.ToLower()));
            }

            if (string.IsNullOrWhiteSpace(userFilteringOptions.SortColumn))
            {
                orderBy = u => u.FirstName;
            }
            else
            {
                switch (userFilteringOptions.SortColumn.ToLower())
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
                        orderBy = u => u.Username;
                        break;

                    default:
                        orderBy = u => u.FirstName;
                        break;
                }
            }

            var queryparams = new OrderedQueryParameters<User, string>
            {
                Criteria = criteria,
                OrderBy = orderBy,
                SortDescending = userFilteringOptions.SortDescending,
                ContinuationToken = userFilteringOptions.ContinuationToken,
                ChunkSize = userFilteringOptions.PageSize
            };

            var guestUsersInTenantResult = await _userRepository.GetOrderedPaginatedItemsAsync(queryparams);
            var guestUsersInTenant = guestUsersInTenantResult.Items.ToList();
            var filteredUserCount = guestUsersInTenant.Count;
            var returnMetaData = new PagingMetadata<User>
            {
                FilteredRecords = filteredUserCount,
                List = guestUsersInTenant,
                SearchValue = userFilteringOptions.SearchValue,
                ContinuationToken = guestUsersInTenantResult.ContinuationToken,
                IsLastChunk = guestUsersInTenantResult.IsLastChunk
            };

            return returnMetaData;
        }

        private bool IsBuiltInOnPremTenant(Guid? tenantId)
        {
            if (tenantId == null || string.IsNullOrEmpty(_deploymentType) || !_deploymentType.StartsWith("OnPrem"))
            {
                return false;
            }

            return tenantId.ToString().ToUpper() == "2D907264-8797-4666-A8BB-72FE98733385" ||
                tenantId.ToString().ToUpper() == "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3";
        }

        private async Task<User> CreateUserInDb(User user, Guid tenantId)
        {
            var validationErrors = new List<ValidationFailure>();

            if (!await IsUniqueUsername(user.Id, user.Username))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.Username), "A user with that Username already exists."));
            }

            if (!await IsUniqueEmail(user.Id, user.Email))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.Email), "A user with that email address already exists."));
            }

            if (!string.IsNullOrEmpty(user.LdapId) && !await IsUniqueLdapId(user.Id, user.LdapId))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.LdapId), "Unable to provision user. The LDAP User Account is already in use."));
            }

            if (tenantId == Guid.Empty)
            {
                validationErrors.Add(new ValidationFailure(nameof(tenantId), "The tenant Id cannot be an empty Guid."));
            }

            if (validationErrors.Any())
            {
                _logger.Error($"Validation failed creating user {user.Id}");
                throw new ValidationFailedException(validationErrors);
            }

            var result = await _userRepository.CreateItemAsync(user);
            return result;
        }

        private async Task<User> UpdateUserInDb(User user, Guid id, ClaimsPrincipal claimsPrincipal)
        {
            var existingUser = await _userRepository.GetItemAsync(id);
            if (existingUser == null)
            {
                _logger.Error($"An error occurred retrieving user {id}");
                throw new NotFoundException($"A User resource could not be found for id {id}");
            }

            if (string.IsNullOrEmpty(user.Username))
            {
                user.Username = existingUser.Username?.ToLower();
            }
            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Email = user.Email?.ToLower();
            existingUser.IsLocked = user.IsLocked;
            existingUser.IsIdpUser = user.IsIdpUser;

            var tenantId = claimsPrincipal.GetTenantId();
            var userLicense = await GetLicenseTypeForUserAsync(id, claimsPrincipal.GetTenantId());

            if (user.LicenseType != userLicense && await CanManageUserLicensesAsync(claimsPrincipal))
            {
                await AssignUserLicense(user, user.LicenseType, tenantId);
            }

            try
            {
                await _userRepository.UpdateItemAsync(id, existingUser);
            }
            catch (Exception ex)
            {
                _logger.Error("User Updation failed", ex);
                throw;
            }

            return existingUser;
        }

        private async Task AssignUserLicense(User user, LicenseType? licenseType, Guid tenantId, bool sendWelcomeEmail = false)
        {
            if (user.Id == null || user.Id == Guid.Empty)
            {
                var errorMessage = "User Id is required for assiging license";

                _logger.Error(errorMessage);
                throw new ArgumentException(errorMessage);
            }

            var licenseRequestDto = new UserLicenseDto
            {
                AccountId = tenantId.ToString(),
                LicenseType = (licenseType ?? LicenseType.Default).ToString(),
                UserId = user.Id?.ToString()
            };

            try
            {
                /* If the user is successfully created assign the license. */
                var assignedLicenseServiceResult = await _licenseApi.AssignUserLicenseAsync(licenseRequestDto);

                if (assignedLicenseServiceResult.ResultCode == LicenseResponseResultCode.Success && sendWelcomeEmail)
                {
                    await SendWelcomeEmailAsync(user.Email, user.FirstName);
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

            var orgAdmins = await GetTenantAdminsByIdAsync(tenantId);

            if (orgAdmins.Count > 0)
            {
                var orgAdminReq = _mapper.Map<List<User>, List<UserEmailRequest>>(orgAdmins);
                await _emailApi.SendUserLockedMail(new LockUserRequest { OrgAdmins = orgAdminReq, UserEmail = user.Email, UserFullName = $"{user.FirstName} {user.LastName}" });
            }
        }

        private async Task SendWelcomeEmailAsync(string email, string firstName)
        {
            try
            {
                await _emailApi.SendWelcomeEmail(new UserEmailRequest { Email = email, FirstName = firstName });
            }
            catch (Exception ex)
            {
                _logger.Error("Welcome email not sent", ex);
            }
        }

        private async Task<List<User>> GetTenantAdminsByIdAsync(Guid userTenantId)
        {
            var adminGroupId = await GetBuiltInGroupId(userTenantId, GroupType.TenantAdmin);
            if (adminGroupId != null)
            {
                var userIds = await _tenantApi.GetUserIdsByTenantIdAsync(userTenantId);
                if (!userIds.IsSuccess())
                {
                    _logger.Error($"Error fetching user ids for the tenant id: {userTenantId}");
                    throw new NotFoundException($"Error fetching user ids for the tenant id: {userTenantId}");
                }
                var admins = await _userRepository.GetItemsAsync(u => userIds.Payload.ToList().Contains(u.Id.Value) && u.Groups.Contains(adminGroupId.Value));
                return admins.ToList();
            }

            return new List<User>();
        }

        private async Task<Guid?> GetBuiltInGroupId(Guid? tenantId, GroupType groupType)
        {
            try
            {
                var group = (await _groupRepository
                    .GetItemsAsync(g => g.Type == groupType && (g.TenantId == null || g.TenantId == tenantId)))
                    .SingleOrDefault();

                if (group == null)
                {
                    throw new Exception($"{groupType.ToString()} group does not exist for tenant {tenantId}");
                }

                return group.Id.GetValueOrDefault();
            }
            catch (InvalidOperationException ex)
            {
                throw new Exception($"Multiple {groupType.ToString()} groups exist for tenant {tenantId}", ex);
            }
        }

        private async Task<User> LockUser(Guid userId, bool locked)
        {
            var user = await _userRepository.GetItemAsync(userId);
            user.IsLocked = locked;

            await _userRepository.UpdateItemAsync(userId, user);
            return user;
        }

        public async Task<bool> LockOrUnlockUserAsync(Guid userId, Guid tenantId, bool locked)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to delete a User resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            if (locked && await _superAdminService.IsLastRemainingSuperAdminAsync(userId))
            {
                throw new ValidationFailedException(new[] { new ValidationFailure("IsLocked", "The final superadmin user cannot be locked") });
            }

            return await UpdateLockUserDetailsInDb(userId, tenantId, locked);
        }

        #region User Group Methods

        public async Task<UserGroup> CreateUserGroupAsync(UserGroup model,  Guid currentUserId)
        {
            var validationErrors = new List<ValidationFailure>();

            var validationResult = _validatorLocator.Validate<UserGroupValidator>(model);
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

            if (model.GroupId == GroupIds.SuperAdminGroupId && !await _superAdminService.IsSuperAdminAsync(currentUserId))
            {
                _logger.Warning($"SuperAdmin group failed to create because the current user {currentUserId} is not a superadmin");
                throw new ValidationFailedException(new[] { new ValidationFailure("GroupId", $"User group {model.GroupId} does not exist") });
            }

            if (existingUser.Groups.Contains(model.GroupId))
            {
                validationErrors.Add(new ValidationFailure(nameof(existingUser), $"User group {model.GroupId} already exists for User id {model.UserId}"));
                throw new ValidationFailedException(validationErrors);
            }

            return await CreateUserGroupInDb(model, existingUser);
        }

        public async Task<List<Guid>> GetUserIdsByGroupIdAsync(Guid groupId, Guid tenantId, Guid currentUserId)
        {
            var validationResult = _validatorLocator.Validate<GroupIdValidator>(groupId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a User group resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            if (groupId == GroupIds.SuperAdminGroupId && !await _superAdminService.IsSuperAdminAsync(currentUserId))
            {
                _logger.Error($"The list of superadmins was requested by a non superadmin {currentUserId}");
                throw new NotFoundException($"A User group resource could not be found for id {groupId}");
            }

            var tenantIds = await _tenantApi.GetTenantIdsForUserIdAsync(currentUserId);
            if (!tenantIds.IsSuccess())
            {
                _logger.Error("Unable to fetch tenant ids");
                throw new NotFoundException("Unable to fetch tenant ids from tenant service");
            }
            var result = await _userRepository.GetItemsAsync(u => u.Groups.Contains(groupId) && tenantIds.Payload.Contains(tenantId));

            if (result == null)
            {
                _logger.Error($"A User group resource could not be found for id {groupId}");
                throw new NotFoundException($"A User group resource could not be found for id {groupId}");
            }

            return result.Select(user => user.Id.GetValueOrDefault()).ToList();
        }

        public async Task<List<Guid>> GetGroupIdsByUserIdAsync(Guid userId)
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

            if (!userIdValidationResult.IsValid || !groupIdValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to remove the User from group.");
                throw new ValidationFailedException(userIdValidationResult.Errors);
            }

            var user = await _userRepository.GetItemAsync(userId);
            if (user == null)
            {
                throw new DocumentNotFoundException($"User {userId} doesn't exist");
            }

            if (groupId == GroupIds.SuperAdminGroupId)
            {
                if (!await _superAdminService.IsSuperAdminAsync(currentUserId))
                {
                    _logger.Warning($"Non superadmin {currentUserId} attempted to remove user {userId} from the superadmin group");
                    return true;
                }

                if (await _superAdminService.IsLastRemainingSuperAdminAsync(userId))
                {
                    throw new ValidationFailedException(new[] { new ValidationFailure("UserId", "The final unlocked user cannot be removed from the superadmin group") });
                }
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

        private async Task<UserGroup> CreateUserGroupInDb(UserGroup userGroup, User existingUser)
        {
            try
            {
                existingUser.Groups.Add(userGroup.GroupId);
                await _userRepository.UpdateItemAsync(userGroup.UserId, existingUser);
                return userGroup;
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

            return await GetUserLicenseType(userId, tenantId);
        }

        #endregion

        private async Task<LicenseType?> GetUserLicenseType(Guid userId, Guid tenantId)
        {
            var userLicenses = (await _licenseApi.GetUserLicenseDetailsAsync(tenantId, userId)).LicenseAssignments;

            if (userLicenses == null || userLicenses.Count == 0)
            {
                return null;
            }

            return (LicenseType)Enum.Parse(typeof(LicenseType), userLicenses[0].LicenseType);
        }

        private async Task<bool> UpdateLockUserDetailsInDb(Guid id, Guid tenantId, bool isLocked)
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
                    AccountId = tenantId.ToString()
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
            username = username?.ToLower();
            var users = await _userRepository
                            .GetItemsAsync(u => userId == null || userId.Value == Guid.Empty
                                                    ? u.Username == username
                                                    : u.Id != userId && u.Username == username);
            return !users.Any();
        }

        private async Task<bool> IsUniqueEmail(Guid? userId, string email)
        {
            email = email?.ToLower();
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

        public async Task<PagingMetadata<User>> GetTenantUsersFromDb(Guid tenantId, Guid currentUserId, UserFilteringOptions userFilteringOptions)
        {
            if (userFilteringOptions == null)
            {
                userFilteringOptions = new UserFilteringOptions
                {
                    SearchValue = "",
                    OnlyCurrentUser = false,
                    IncludeInactive = false,
                    SortColumn = "FirstName",
                    SortDescending = false,
                    IdpFilter = IdpFilter.All
                };
            }

            if (!userFilteringOptions.GroupingType.Equals(UserGroupingType.None) && userFilteringOptions.UserGroupingId.Equals(Guid.Empty))
            {
                throw new ValidationFailedException(new[]
                { new ValidationFailure(nameof(UserFilteringOptions), "If a GroupingType is specified then a valid GroupingId must also be provided") });
            }

            var tenantUsersResponse = await _tenantApi.GetUserIdsByTenantIdAsync(tenantId);
            if (!tenantUsersResponse.IsSuccess())
            {
                throw new Exception("Unable to find users for the tenant id");
            }

            var tenantUsers = tenantUsersResponse.Payload.ToList();
            var totalRecords = tenantUsers.Count;

            var query = await _searchBuilder.BuildSearchQueryAsync(currentUserId, tenantUsers, userFilteringOptions);
            var batch = await _queryRunner.RunQuery(query);
            var userList = batch.ToList();

            var filteredRecords = userList.Count;

            if (userFilteringOptions.PageSize < 1)
            {
                return new PagingMetadata<User>
                {
                    FilteredRecords = filteredRecords,
                    TotalRecords = totalRecords,
                    List = userList,
                    SearchValue = userFilteringOptions.SearchValue
                };
            }

            if (userFilteringOptions.PageNumber >= 1)
            {
                userList = userList.Skip((userFilteringOptions.PageNumber - 1) * userFilteringOptions.PageSize).ToList();
            }

            userList = userList.Take(userFilteringOptions.PageSize).ToList();

            return new PagingMetadata<User>
            {
                FilteredRecords = filteredRecords,
                TotalRecords = totalRecords,
                List = userList,
                SearchValue = userFilteringOptions.SearchValue
            };
        }

        private void TrimNameOfUser(User user)
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

        public async Task<VerifyUserEmailResponse> VerifyEmailAsync(VerifyUserEmailRequest verifyRequest)
        {
            var user = await _userRepository.GetItemAsync(x => x.Email == verifyRequest.Email);
            if (user == null)
            {
                _logger.Error($"A User resource could not be found with email {verifyRequest.Email}");
                throw new NotFoundException($"A User resource could not be found with email {verifyRequest.Email}");
            }

            if (user.Id == null)
            {
                _logger.Error($"User resource with email {verifyRequest.Email} has a null Id");
                throw new Exception($"User resource with email {verifyRequest.Email} has a null Id");
            }

            if (user.IsEmailVerified == null)
            {
                return new VerifyUserEmailResponse { Result = true };
            }

            if (user.EmailVerificationId == verifyRequest.VerificationId)
            {
                user.IsEmailVerified = true;
                user.EmailVerifiedAt = DateTime.UtcNow;

                await _userRepository.UpdateItemAsync((Guid)user.Id, user);

                return new VerifyUserEmailResponse{Result = true};
            }

            return new VerifyUserEmailResponse{Result = false};
        }

        private async Task<bool> CanManageUserLicensesAsync(ClaimsPrincipal principal)
        {
            return await _policyEvaluator.HasExplicitPermissionAsync(principal, SynthesisPermission.CanManageLicenses);
        }
    }
}