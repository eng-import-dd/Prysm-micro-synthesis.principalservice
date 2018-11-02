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
using Synthesis.PrincipalService.Controllers.Exceptions;
using Synthesis.PrincipalService.Email;
using Synthesis.PrincipalService.Exceptions;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Enums;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Services;
using Synthesis.PrincipalService.Validators;
using Synthesis.TenantService.InternalApi.Api;
using Synthesis.TenantService.InternalApi.Models;
using Synthesis.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    /// <summary>
    /// Represents a controller for User resources.
    /// </summary>
    /// <seealso cref="IUsersController" />
    public class UsersController : IUsersController
    {
        private readonly AsyncLazy<IRepository<User>> _userRepositoryAsyncLazy;
        private readonly AsyncLazy<IRepository<Group>> _groupRepositoryAsyncLazy;
        private readonly IValidatorLocator _validatorLocator;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;
        private readonly ILicenseApi _licenseApi;
        private readonly IEmailApi _emailApi;
        private readonly IMapper _mapper;
        private readonly string _deploymentType;
        private readonly ITenantApi _tenantApi;
        private readonly IIdentityUserApi _identityUserApi;
        private readonly ITenantUserSearchBuilder _searchBuilder;
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
            ITenantUserSearchBuilder searchBuilder,
            IQueryRunner<User> queryRunner,
            ITenantApi tenantApi,
            IPolicyEvaluator policyEvaluator,
            ISuperAdminService superAdminService,
            IIdentityUserApi identityUserApi)
        {
            _userRepositoryAsyncLazy = new AsyncLazy<IRepository<User>>(() => repositoryFactory.CreateRepositoryAsync<User>());
            _groupRepositoryAsyncLazy = new AsyncLazy<IRepository<Group>>(() => repositoryFactory.CreateRepositoryAsync<Group>());
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

        internal static PartitionKey DefaultPartitionKey => new PartitionKey(Undefined.Value);

        internal static BatchOptions DefaultBatchOptions => new BatchOptions { PartitionKey = DefaultPartitionKey };

        internal static QueryOptions DefaultQueryOptions => new QueryOptions { PartitionKey = DefaultPartitionKey };

        public async Task<User> CreateUserAsync(CreateUserRequest model, ClaimsPrincipal principal)
        {
            var validationResult = _validatorLocator.Validate<CreateUserRequestValidator>(model);
            if (!validationResult.IsValid)
            {
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
                CreatedBy = principal.GetPrincipialId(),
                CreatedDate = DateTime.UtcNow,
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = model.Email?.ToLower(),
                EmailVerificationId = Guid.NewGuid(),
                Username = model.Username?.ToLower(),
                Groups = model.Groups ?? new List<Guid>(),
                IsIdpUser = model.IsIdpUser,
                IsLocked = false,
                LdapId = model.LdapId,
                LicenseType = model.LicenseType,
                IsEmailVerified = !model.EmailVerificationRequired,
                EmailVerifiedAt = !model.EmailVerificationRequired ? DateTime.UtcNow : new DateTime?()
            };

            newUser.Groups.AddRange(new List<Guid>
            {
                (await GetBuiltInGroupIdAsync(null, GroupType.Default)).GetValueOrDefault(),
                (await GetBuiltInGroupIdAsync(tenantId, GroupType.Basic)).GetValueOrDefault()
            });

            var result = await CreateUserInDbAsync(newUser, tenantId);

            if (result.Id == null)
            {
                throw new ResourceException("User was incorrectly created with a null Id");
            }

            if (model.UserType == UserType.Enterprise)
            {
                await AssignUserLicenseAsync(result, newUser.LicenseType, tenantId, true);
            }

            var response = await _tenantApi.AddUserToTenantAsync(tenantId, (Guid)result.Id);
            if (!response.IsSuccess())
            {
                var userRepository = await _userRepositoryAsyncLazy;
                await userRepository.DeleteItemAsync((Guid)result.Id);
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
                var userRepository = await _userRepositoryAsyncLazy;
                await userRepository.DeleteItemAsync((Guid)result.Id, DefaultQueryOptions);

                var removeUserResponse = await _tenantApi.RemoveUserFromTenantAsync(tenantId, (Guid)result.Id);
                if (removeUserResponse.ResponseCode != HttpStatusCode.NoContent)
                {
                    throw new IdentityPasswordException($"Setting the user's password failed. The user entry was removed from the database, but the attempt to remove the user with id {(Guid)result.Id} from their tenant with id {model.TenantId} failed.");
                }

                throw new IdentityPasswordException("Setting the user's password failed. The user was removed from the database and from the tenant they were mapped to.");
            }

            _eventService.Publish(EventNames.UserCreated, result);

            return result;
        }

        public async Task<User> GetUserAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(id);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userRepository = await _userRepositoryAsyncLazy;
            var result = await userRepository.GetItemAsync(id, DefaultQueryOptions);

            if (result == null)
            {
                throw new NotFoundException($"A User resource could not be found for id {id}");
            }

            return result;
        }

        public async Task<IEnumerable<UserNames>> GetNamesForUsersAsync(IEnumerable<Guid> userIds)
        {
            var validationResult = _validatorLocator.Validate<GeUserNamesByIdsValidator>(userIds);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var nullableUserIds = userIds.Select(x => new Guid?(x));
            var userRepository = await _userRepositoryAsyncLazy;
            return await userRepository.CreateItemQuery(DefaultBatchOptions)
                .Where(user => nullableUserIds.Contains(user.Id))
                .Select(user => new UserNames
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Id = user.Id ?? default(Guid)
                })
                .ToListAsync();
        }

        /// <inheritdoc />
        public async Task<PagingMetadata<BasicUser>> GetUsersBasicAsync(Guid tenantId, Guid userId, UserFilteringOptions userFilteringOptions)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userListResult = await GetTenantUsersFromDbAsync(tenantId, userId, userFilteringOptions);
            if (userListResult == null)
            {
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
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userListResult = await GetTenantUsersFromDbAsync(tenantId, userId, userFilteringOptions);
            if (userListResult == null)
            {
                return 0;
            }

            return userListResult.FilteredRecords;
        }

        public async Task<PagingMetadata<User>> GetUsersForTenantAsync(UserFilteringOptions userFilteringOptions, Guid tenantId, Guid currentUserId)
        {
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(UserIdValidator), currentUserId },
                { typeof(TenantIdValidator), tenantId }
            });
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
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

            var userRepository = await _userRepositoryAsyncLazy;
            var usersInTenant = await userRepository.GetItemsAsync(u => tenantUsers.Payload.Contains(u.Id ?? Guid.Empty), DefaultBatchOptions);
            if (!usersInTenant.Any())
            {
                throw new NotFoundException($"Users for tenant '{tenantId}' could not be found");
            }

            return await GetTenantUsersFromDbAsync(tenantId, currentUserId, userFilteringOptions);
        }

        public async Task<User> UpdateUserAsync(Guid userId, User userModel, Guid tenantId, ClaimsPrincipal claimsPrincipal)
        {
            TrimNameOfUser(userModel);

            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(UserIdValidator), userId },
                { typeof(UpdateUserRequestValidator), userModel }
            });

            if (!await IsUniqueUsernameAsync(userId, userModel.Username))
            {
                validationResult.Errors.Add(new ValidationFailure(nameof(userModel.Username), "A user with that Username already exists."));
            }

            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            return await UpdateUserInDbAsync(userModel, userId, tenantId, claimsPrincipal);
        }

        public async Task<CanPromoteUser> CanPromoteUserAsync(string email, Guid tenantId)
        {
            var validationResult = _validatorLocator.Validate<EmailValidator>(email);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var user = await GetUserByEmail(email);
            if (user == null)
            {
                return new CanPromoteUser
                {
                    ResultCode = CanPromoteUserResultCode.UserDoesNotExist,
                    UserId = null
                };
            }

            var canPromoteCode = await IsValidPromotionForTenant(user, tenantId);
            return new CanPromoteUser
            {
                ResultCode = canPromoteCode,
                UserId = user.Id
            };
        }

        private CanPromoteUser MakePromoteResponse(CanPromoteUserResultCode code, Guid? userId)
        {
            return new CanPromoteUser
            {
                ResultCode = code,
                UserId = userId
            };
        }

        private async Task<User> GetUserByEmail(string emailAddress)
        {
            emailAddress = emailAddress?.ToLower();
            var userRepository = await _userRepositoryAsyncLazy;
            var userList = await userRepository.GetItemsAsync(u => u.Email.Equals(emailAddress), DefaultBatchOptions);
            var existingUser = userList.FirstOrDefault();

            return existingUser;
        }

        public async Task DeleteUserAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(id);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            try
            {
                var userRepository = await _userRepositoryAsyncLazy;
                await userRepository.DeleteItemAsync(id, DefaultQueryOptions);

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
            var userRepository = await _userRepositoryAsyncLazy;
            var existingUser = await userRepository.CreateItemQuery(DefaultBatchOptions)
                .FirstOrDefaultAsync(x => x.Email == model.Email || x.Username == model.Email);

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
                IsEmailVerified = false,
                IsIdpUser = model.IsIdpUser,
                IsLocked = false,
                Groups = new List<Guid>()
            };

            user.Groups.AddRange(new List<Guid>
            {
                (await GetBuiltInGroupIdAsync(null, GroupType.Default)).GetValueOrDefault()
            });

            // Create the user in the DB
            var guestUser = await userRepository.CreateItemAsync(user);
            var guestUserId = guestUser.Id.GetValueOrDefault();

            try
            {
                // Set the password
                var setPasswordResponse = await _identityUserApi.SetPasswordAsync(new IdentityUser { Password = model.Password, UserId = guestUserId });
                if (!setPasswordResponse.IsSuccess())
                {
                    throw new IdentityPasswordException($"Setting the new guest user's password failed. The user ({guestUserId}) was removed from the database and from the tenant to which they were mapped.");
                }
            }
            catch (Exception)
            {
                try
                {
                    await userRepository.DeleteItemAsync(guestUserId, DefaultQueryOptions);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to delete newly created guest user {guestUserId} after failing to set the password.", ex);
                }

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
                await userRepository.UpdateItemAsync(guestUserId, guestUser);
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

            var userRepository = await _userRepositoryAsyncLazy;
            var user = await userRepository.CreateItemQuery(DefaultBatchOptions)
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            if (user == null)
            {
                throw new NotFoundException($"No user found with the email: {request.Email}");
            }

            if (user.IsEmailVerified)
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
                await userRepository.UpdateItemAsync((Guid)user.Id, user);
            }
        }

        public async Task<PromoteGuestResultCode> PromoteGuestUserAsync(Guid userId, Guid tenantId, LicenseType licenseType, ClaimsPrincipal claimsPrincipal, bool autoPromote = false)
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
                    throw new LicenseNotAvailableException($"No license of type={licenseType} is available to assign to user={userId} and tenantId={tenantId}");
                }
            }

            var userRepository = await _userRepositoryAsyncLazy;
            var user = await userRepository.GetItemAsync(userId, DefaultQueryOptions);

            var isValidResult = await IsValidPromotionForTenant(user, tenantId);
            if (isValidResult == CanPromoteUserResultCode.UserAccountAlreadyExists)
            {
                throw new UserAlreadyPromotedException($"UserId={userId} already promoted.");
            }
            if (isValidResult == CanPromoteUserResultCode.EmailNotInTenantDomain)
            {
                throw new PromotionNotPossibleException($"User={userId} email domain is not in the tenant domain.");
            }

            await AssignGuestUserToTenant(user, tenantId);

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
                // If assigning a license fails, then we must disable the user
                await LockUserAsync(userId, true);

                throw new LicenseAssignmentFailedException($"Assigned user {userId} to tenant {tenantId}, but failed to assign license", userId);
            }

            await SendWelcomeEmailAsync(user);

            return PromoteGuestResultCode.Success;
        }

        public async Task<User> AutoProvisionRefreshGroupsAsync(IdpUserRequest model, Guid tenantId, ClaimsPrincipal claimsPrincipal)
        {
            var validationResult = _validatorLocator.Validate<TenantIdValidator>(tenantId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the tenant id.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userId = model.UserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return await AutoProvisionUserAsync(model, tenantId, claimsPrincipal);
            }

            if (model.IsGuestUser)
            {
                try
                {
                    await PromoteGuestUserAsync(userId, model.TenantId, LicenseType.UserLicense, claimsPrincipal, true);
                }
                catch (Exception ex)
                {
                    throw new PromotionFailedException($"Failed to promote user {userId}", ex);
                }

                await SendWelcomeEmailAsync(new User { Id = userId, Email = model.EmailId, FirstName = model.FirstName });
            }

            if (model.Groups != null)
            {
                return await UpdateIdpUserGroupsAsync(userId, model);
            }

            var userRepository = await _userRepositoryAsyncLazy;
            return await userRepository.GetItemAsync(userId, DefaultQueryOptions);
        }

        private async Task<User> AutoProvisionUserAsync(IdpUserRequest model, Guid tenantId, ClaimsPrincipal claimsPrincipal)
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

            var result = await CreateUserAsync(createUserRequest, claimsPrincipal);
            if (result == null || model.Groups == null)
            {
                return result;
            }

            var groupResult = await UpdateIdpUserGroupsAsync(result.Id.GetValueOrDefault(), model);
            if (groupResult == null)
            {
                throw new IdpUserProvisioningException($"Failed to update Idp user groups for user {result.Id.GetValueOrDefault()}");
            }

            return result;
        }

        private async Task<User> UpdateIdpUserGroupsAsync(Guid userId, IdpUserRequest model)
        {
            var userRepository = await _userRepositoryAsyncLazy;
            var user = await userRepository.GetItemAsync(userId, DefaultQueryOptions);

            if (model.Groups == null || !model.Groups.Any())
            {
                return user;
            }

            var groupRepository = await _groupRepositoryAsyncLazy;
            var tenantGroupsResult = await groupRepository.GetItemsAsync(g => g.TenantId == model.TenantId);

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
                    if (user.Groups.Contains(tenantGroup.Id.GetValueOrDefault()))
                    {
                        continue; //Nothing to do if the user is already a member of the group
                    }

                    user.Groups.Add(tenantGroup.Id.GetValueOrDefault());
                }
                else
                {
                    //remove the user from the group
                    user.Groups.Remove(tenantGroup.Id.GetValueOrDefault());
                }
            }

            return await userRepository.UpdateItemAsync(userId, user);
        }

        public async Task<User> GetUserByUserNameOrEmailAsync(string username)
        {
            var unameValidationResult = username.Contains("@") ? _validatorLocator.Validate<EmailValidator>(username) : _validatorLocator.Validate<UserNameValidator>(username);

            if (!unameValidationResult.IsValid)
            {
                throw new ValidationException("Email/Username is either empty or invalid");
            }

            username = username.ToLower();
            var userRepository = await _userRepositoryAsyncLazy;
            var existingUser = await userRepository.CreateItemQuery(DefaultBatchOptions)
                .FirstOrDefaultAsync(u => u.Email == username || u.Username == username);

            if (existingUser == null)
            {
                throw new NotFoundException("User not found with that Email/Username.");
            }

            return existingUser;
        }

        public async Task<PagingMetadata<User>> GetGuestUsersForTenantAsync(Guid tenantId, UserFilteringOptions userFilteringOptions)
        {
            var validationResult = _validatorLocator.Validate<TenantIdValidator>(tenantId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userRepository = await _userRepositoryAsyncLazy;
            var userQuery = userRepository.CreateItemQuery(new BatchOptions
            {
                BatchSize = userFilteringOptions.PageSize,
                ContinuationToken = userFilteringOptions.ContinuationToken,
                PartitionKey = DefaultPartitionKey
            });

            var userIdsInTenant = await _tenantApi.GetUserIdsByTenantIdAsync(tenantId);
            if (userIdsInTenant.IsSuccess())
            {
                var userids = userIdsInTenant.Payload.ToList();
                userQuery = userQuery.Where(u => !userids.Contains(u.Id.Value));
            }

            var tenantDomainsResponse = await _tenantApi.GetTenantDomainsAsync(tenantId);
            if (tenantDomainsResponse.IsSuccess() && tenantDomainsResponse.Payload.Any())
            {
                var tenantDomains = tenantDomainsResponse.Payload.Select(d => d.Domain).ToList();
                userQuery = userQuery.Where(u => tenantDomains.Contains(u.EmailDomain));
            }

            if (!string.IsNullOrEmpty(userFilteringOptions.SearchValue))
            {
                userQuery = userQuery.Where(x =>
                    x != null &&
                    (x.FirstName.ToLower() + " " + x.LastName.ToLower()).Contains(userFilteringOptions.SearchValue.ToLower()) ||
                    x != null && x.Email.ToLower().Contains(userFilteringOptions.SearchValue.ToLower()) ||
                    x != null && x.Username.ToLower().Contains(userFilteringOptions.SearchValue.ToLower()));
            }

            Expression<Func<User, string>> orderBy;
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

            userQuery = userFilteringOptions.SortDescending
                ? userQuery.OrderByDescending(orderBy)
                : userQuery.OrderBy(orderBy);

            var userRepositoryQuery = userQuery.AsRepositoryQuery();
            if (!await userRepositoryQuery.MoveNextAsync())
            {
                return new PagingMetadata<User>
                {
                    FilteredRecords = 0,
                    List = new List<User>(),
                    SearchValue = userFilteringOptions.SearchValue,
                    ContinuationToken = null,
                    IsLastChunk = true
                };
            }

            var users = userRepositoryQuery.Current.ToList();
            return new PagingMetadata<User>
            {
                FilteredRecords = users.Count,
                List = users,
                SearchValue = userFilteringOptions.SearchValue,
                ContinuationToken = userRepositoryQuery.Current.ContinuationToken,
                IsLastChunk = userRepositoryQuery.Current.ContinuationToken == null
            };
        }

        public async Task<bool> LockOrUnlockUserAsync(Guid userId, Guid tenantId, bool @lock)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            if (@lock && await _superAdminService.IsLastRemainingSuperAdminAsync(userId))
            {
                throw new ValidationFailedException(new[] { new ValidationFailure("IsLocked", "The final superadmin user cannot be locked") });
            }

            return await UpdateLockUserDetailsInDbAsync(userId, tenantId, @lock);
        }

        public async Task<UserGroup> CreateUserGroupAsync(UserGroup model, Guid currentUserId)
        {
            var validationResult = _validatorLocator.Validate<UserGroupValidator>(model);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            // We need to verify that the group they want to add a user to is a group within their account
            // and we also need to verify that the user they want to add to a group is also within their account.
            var userRepository = await _userRepositoryAsyncLazy;
            var existingUser = await userRepository.GetItemAsync(model.UserId, DefaultQueryOptions);

            if (existingUser == null)
            {
                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(existingUser), $"Unable to find the user with the id {model.UserId}") });
            }

            if (model.GroupId == GroupIds.SuperAdminGroupId && !await _superAdminService.IsSuperAdminAsync(currentUserId))
            {
                throw new ValidationFailedException(new[] { new ValidationFailure("GroupId", $"User group {model.GroupId} does not exist") });
            }

            if (existingUser.Groups.Contains(model.GroupId))
            {
                throw new ValidationFailedException(new[] { new ValidationFailure(nameof(existingUser), $"User group {model.GroupId} already exists for User id {model.UserId}") });
            }

            return await CreateUserGroupInDbAsync(model, existingUser);
        }

        public async Task<List<Guid>> GetUserIdsByGroupIdAsync(Guid groupId, Guid currentUserId)
        {
            var validationResult = _validatorLocator.Validate<GroupIdValidator>(groupId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            if (groupId == GroupIds.SuperAdminGroupId && !await _superAdminService.IsSuperAdminAsync(currentUserId))
            {
                throw new NotFoundException($"A group resource could not be found for id {groupId}");
            }

            var userRepository = await _userRepositoryAsyncLazy;
            return await userRepository.CreateItemQuery(DefaultBatchOptions)
                .Where(u => u.Groups.Contains(groupId) && u.Id != null)
                .Select(u => u.Id.Value)
                .ToListAsync();
        }

        public async Task<List<Guid>> GetGroupIdsByUserIdAsync(Guid userId)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var userRepository = await _userRepositoryAsyncLazy;
            var userWithGivenUserId = await userRepository.GetItemAsync(userId, DefaultQueryOptions);
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
                throw new ValidationFailedException(userIdValidationResult.Errors);
            }

            var userRepository = await _userRepositoryAsyncLazy;
            var user = await userRepository.GetItemAsync(userId, DefaultQueryOptions);
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
            await userRepository.UpdateItemAsync(userId, user);
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

            var userRepository = await _userRepositoryAsyncLazy;
            return await userRepository.GetItemsAsync(u => userIdList.Contains(u.Id ?? Guid.Empty), DefaultBatchOptions);
        }

        private async Task<UserGroup> CreateUserGroupInDbAsync(UserGroup userGroup, User existingUser)
        {
            try
            {
                existingUser.Groups.Add(userGroup.GroupId);
                var userRepository = await _userRepositoryAsyncLazy;
                await userRepository.UpdateItemAsync(userGroup.UserId, existingUser);
                return userGroup;
            }
            catch (Exception ex)
            {
                _logger.Error("", ex);
                throw;
            }
        }

        public async Task<LicenseType?> GetLicenseTypeForUserAsync(Guid userId, Guid tenantId)
        {
            var validationResult = _validatorLocator.Validate<UserIdValidator>(userId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a User license type resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _tenantApi.GetTenantIdsForUserIdAsync(userId);
            if (!result.IsSuccess())
            {
                _logger.Error($"Error fetching tenant Ids for the user Id: {userId} .");
                throw new NotFoundException($"Error fetching tenant Ids for the user Id: {userId} .");
            }

            var userTenantIds = result.Payload;
            // ReSharper disable once PossibleMultipleEnumeration
            if (!userTenantIds.Any())
            {
                throw new NotFoundException($"A User resource could not be found for id {userId}");
            }

            // ReSharper disable once PossibleMultipleEnumeration
            if (!userTenantIds.Contains(tenantId))
            {
                throw new InvalidOperationException();
            }

            return await GetUserLicenseTypeAsync(userId, tenantId);
        }

        public async Task<PagingMetadata<User>> GetTenantUsersFromDbAsync(Guid tenantId, Guid currentUserId, UserFilteringOptions userFilteringOptions)
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

        public async Task<VerifyUserEmailResponse> VerifyEmailAsync(VerifyUserEmailRequest verifyRequest)
        {
            var userRepository = await _userRepositoryAsyncLazy;
            var user = await userRepository.CreateItemQuery(DefaultBatchOptions)
                .FirstOrDefaultAsync(x => x.Email == verifyRequest.Email);
            if (user == null)
            {
                throw new NotFoundException($"A User resource could not be found with email {verifyRequest.Email}");
            }

            if (user.Id == null)
            {
                throw new Exception($"User resource with email {verifyRequest.Email} has a null Id");
            }

            if (user.EmailVerificationId != verifyRequest.VerificationId)
            {
                return new VerifyUserEmailResponse { Result = false };
            }

            user.IsEmailVerified = true;
            user.EmailVerifiedAt = DateTime.UtcNow;

            await userRepository.UpdateItemAsync((Guid)user.Id, user);

            return new VerifyUserEmailResponse { Result = true };
        }

        private static void TrimNameOfUser(User user)
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

        private async Task<bool> IsLicenseAvailable(Guid tenantId, LicenseType licenseType)
        {
            var summary = await _licenseApi.GetTenantLicenseSummaryAsync(tenantId);
            var item = summary.FirstOrDefault(x => string.Equals(x.LicenseName, licenseType.ToString(), StringComparison.CurrentCultureIgnoreCase));

            return item != null && item.TotalAvailable > 0;
        }

        private async Task<CanPromoteUserResultCode> IsValidPromotionForTenant(User user, Guid tenantId)
        {
            var tenants = await GetTentantsForUser(user.Id.GetValueOrDefault());
            if (tenants.Contains(tenantId))
            {
                return CanPromoteUserResultCode.UserAccountAlreadyExists;
            }

            var userDomain = user.Email.Substring(user.Email.IndexOf('@') + 1);
            var tenantDomains = await GetTentantDomains(tenantId);
            if (!tenantDomains.Any(d => d.Domain.Equals(userDomain, StringComparison.OrdinalIgnoreCase)))
            {
                return CanPromoteUserResultCode.EmailNotInTenantDomain;
            }

            return CanPromoteUserResultCode.UserCanBePromoted;
        }

        private async Task<IEnumerable<Guid>> GetTentantsForUser(Guid userId)
        {
            var result = await _tenantApi.GetTenantIdsForUserIdAsync(userId);
            if (!result.IsSuccess() || result.Payload == null)
            {
                throw new Exception($"Could not get tenants for user={userId}. Reason={result}");
            }
            return result.Payload;
        }

        private async Task<IEnumerable<TenantDomain>> GetTentantDomains(Guid tenantId)
        {
            var result = await _tenantApi.GetTenantDomainsAsync(tenantId);
            if (!result.IsSuccess() || result.Payload == null)
            {
                throw new Exception($"Could not get tenant domains for tenant={tenantId}. Reason={result}");
            }
            return result.Payload;
        }

        private async Task AssignGuestUserToTenant(User user, Guid tenantId)
        {
            if (user.Id == null)
            {
                throw new ArgumentException("The id of the user cannot be null");
            }

            var result = await _tenantApi.AddUserToTenantAsync(tenantId, user.Id.Value);
            if (!result.IsSuccess())
            {
                throw new Exception($"Error while adding userId={user.Id} to tenantId={tenantId}.  Reason={result.ReasonPhrase}");
            }

            _eventService.Publish(new ServiceBusEvent<Guid>
            {
                Name = EventNames.UserPromoted,
                Payload = user.Id.GetValueOrDefault()
            });
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

        private async Task<User> CreateUserInDbAsync(User user, Guid tenantId)
        {
            var validationErrors = new List<ValidationFailure>();

            if (!await IsUniqueUsernameAsync(user.Id, user.Username))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.Username), "A user with that Username already exists."));
            }

            if (!await IsUniqueEmailAsync(user.Id, user.Email))
            {
                validationErrors.Add(new ValidationFailure(nameof(user.Email), "A user with that email address already exists."));
            }

            if (!string.IsNullOrEmpty(user.LdapId) && !await IsUniqueLdapIdAsync(user.Id, user.LdapId))
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

            var userRepository = await _userRepositoryAsyncLazy;
            return await userRepository.CreateItemAsync(user);
        }

        private async Task<User> UpdateUserInDbAsync(User user, Guid id, Guid tenantId, ClaimsPrincipal claimsPrincipal)
        {
            var userRepository = await _userRepositoryAsyncLazy;
            var existingUser = await userRepository.GetItemAsync(id, DefaultQueryOptions);
            if (existingUser == null)
            {
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

            if (!await CanManageUserLicensesAsync(claimsPrincipal))
            {
                return await userRepository.UpdateItemAsync(id, existingUser);
            }

            var userLicenseType = await GetLicenseTypeForUserAsync(id, tenantId);
            if (user.LicenseType != userLicenseType)
            {
                await AssignUserLicenseAsync(user, user.LicenseType, tenantId);
            }

            return await userRepository.UpdateItemAsync(id, existingUser);
        }

        private async Task AssignUserLicenseAsync(User user, LicenseType? licenseType, Guid tenantId, bool sendWelcomeEmail = false)
        {
            if (user.Id == null || user.Id == Guid.Empty)
            {
                throw new ArgumentException("User Id is required for assiging license");
            }

            var licenseRequestDto = new UserLicenseDto
            {
                AccountId = tenantId.ToString(),
                LicenseType = (licenseType ?? LicenseType.Default).ToString(),
                UserId = user.Id?.ToString()
            };

            try
            {
                // If the user is successfully created assign the license.
                var assignedLicenseServiceResult = await _licenseApi.AssignUserLicenseAsync(licenseRequestDto);

                if (assignedLicenseServiceResult.ResultCode == LicenseResponseResultCode.Success)
                {
                    if (sendWelcomeEmail)
                    {
                        await SendWelcomeEmailAsync(user);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error assigning license to user", ex);
            }

            // If a license could not be obtained lock the user that was just created.
            await LockUserAsync(user.Id.Value, true);
            user.IsLocked = true;

            // Send email to the the Org Admin about the locked user.
            var orgAdmins = await GetTenantAdminsByIdAsync(tenantId);

            if (orgAdmins.Count > 0)
            {
                var orgAdminEmailRequests = _mapper.Map<List<User>, List<UserEmailRequest>>(orgAdmins);
                await _emailApi.SendUserLockedMail(new LockUserRequest
                {
                    OrgAdmins = orgAdminEmailRequests,
                    UserEmail = user.Email,
                    UserFullName = $"{user.FirstName} {user.LastName}"
                });
            }
        }

        private async Task SendWelcomeEmailAsync(User user)
        {
            try
            {
                await _emailApi.SendWelcomeEmail(new UserEmailRequest
                {
                    Email = user.Email,
                    FirstName = user.FirstName
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"Welcome email not sent to user {user.Id}", ex);
            }
        }

        private async Task<List<User>> GetTenantAdminsByIdAsync(Guid userTenantId)
        {
            var adminGroupId = await GetBuiltInGroupIdAsync(userTenantId, GroupType.TenantAdmin);
            if (adminGroupId == null)
            {
                return new List<User>();
            }

            var userIds = await _tenantApi.GetUserIdsByTenantIdAsync(userTenantId);
            if (!userIds.IsSuccess())
            {
                _logger.Error($"Error fetching user ids for the tenant id: {userTenantId}");
                throw new NotFoundException($"Error fetching user ids for the tenant id: {userTenantId}");
            }

            var userRepository = await _userRepositoryAsyncLazy;
            var userIdList = userIds.Payload.ToList();
            return await userRepository.CreateItemQuery(DefaultBatchOptions)
                .Where(u => userIdList.Contains(u.Id.Value) && u.Groups.Contains(adminGroupId.Value))
                .ToListAsync();
        }

        private async Task<Guid?> GetBuiltInGroupIdAsync(Guid? tenantId, GroupType groupType)
        {
            try
            {
                var groupRepository = await _groupRepositoryAsyncLazy;
                var group = await groupRepository.CreateItemQuery()
                    .SingleOrDefaultAsync(g => g.Type == groupType && g.TenantId == tenantId);

                if (group == null)
                {
                    throw new Exception($"{groupType} group does not exist for tenant {tenantId}");
                }

                return group.Id.GetValueOrDefault();
            }
            catch (InvalidOperationException ex)
            {
                throw new Exception($"Multiple {groupType} groups exist for tenant {tenantId}", ex);
            }
        }

        private async Task<User> LockUserAsync(Guid userId, bool @lock)
        {
            var userRepository = await _userRepositoryAsyncLazy;
            var user = await userRepository.GetItemAsync(userId, DefaultQueryOptions);
            user.IsLocked = @lock;

            await userRepository.UpdateItemAsync(userId, user);
            return user;
        }

        private async Task<LicenseType?> GetUserLicenseTypeAsync(Guid userId, Guid tenantId)
        {
            var userLicenses = (await _licenseApi.GetUserLicenseDetailsAsync(tenantId, userId)).LicenseAssignments;

            if (userLicenses == null || userLicenses.Count == 0)
            {
                return null;
            }

            return (LicenseType)Enum.Parse(typeof(LicenseType), userLicenses[0].LicenseType);
        }

        private async Task<bool> UpdateLockUserDetailsInDbAsync(Guid id, Guid tenantId, bool @lock)
        {
            var validationErrors = new List<ValidationFailure>();
            var userRepository = await _userRepositoryAsyncLazy;
            var existingUser = await userRepository.GetItemAsync(id, DefaultQueryOptions);
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

                if (@lock)
                {
                    // If the user is being locked, remove the associated license.
                    var removeUserLicenseResult = await _licenseApi.ReleaseUserLicenseAsync(licenseRequestDto);

                    if (removeUserLicenseResult.ResultCode != LicenseResponseResultCode.Success)
                    {
                        validationErrors.Add(new ValidationFailure(nameof(existingUser), "Unable to remove license for the user"));
                    }
                }
                else // unlock
                {
                    // If not locked, request a license.
                    var assignUserLicenseResult = await _licenseApi.AssignUserLicenseAsync(licenseRequestDto);

                    if (assignUserLicenseResult.ResultCode != LicenseResponseResultCode.Success)
                    {
                        validationErrors.Add(new ValidationFailure(nameof(existingUser), "Unable to assign license to the user"));
                    }
                }

                if (validationErrors.Any())
                {
                    throw new ValidationFailedException(validationErrors);
                }

                await LockUserAsync(id, @lock);

                return true;
            }

            return false;
        }

        private async Task<bool> IsUniqueUsernameAsync(Guid? userId, string username)
        {
            username = username?.ToLower();
            var userRepository = await _userRepositoryAsyncLazy;

            // Better to perform logic on local variables locally due to query costs.
            if ((userId ?? Guid.Empty) == Guid.Empty)
            {
                return !await userRepository.CreateItemQuery(DefaultBatchOptions)
                    .AnyAsync(u => u.Username == username);
            }

            return !await userRepository.CreateItemQuery(DefaultBatchOptions)
                .AnyAsync(u => u.Id != userId && u.Username == username);
        }

        private async Task<bool> IsUniqueEmailAsync(Guid? userId, string email)
        {
            email = email?.ToLower();
            var userRepository = await _userRepositoryAsyncLazy;

            // Better to perform logic on local variables locally due to query costs.
            if ((userId ?? Guid.Empty) == Guid.Empty)
            {
                return !await userRepository.CreateItemQuery(DefaultBatchOptions)
                    .AnyAsync(u => u.Email == email);
            }

            return !await userRepository.CreateItemQuery(DefaultBatchOptions)
                .AnyAsync(u => u.Id != userId && u.Email == email);
        }

        private async Task<bool> IsUniqueLdapIdAsync(Guid? userId, string ldapId)
        {
            var userRepository = await _userRepositoryAsyncLazy;

            // Better to perform logic on local variables locally due to query costs.
            if ((userId ?? Guid.Empty) == Guid.Empty)
            {
                return !await userRepository.CreateItemQuery(DefaultBatchOptions)
                    .AnyAsync(u => u.LdapId == ldapId);
            }

            return !await userRepository.CreateItemQuery(DefaultBatchOptions)
                .AnyAsync(u => u.Id != userId && u.LdapId == ldapId);
        }

        private async Task<bool> CanManageUserLicensesAsync(ClaimsPrincipal principal)
        {
            return await _policyEvaluator.HasExplicitPermissionAsync(principal, SynthesisPermission.CanManageUserLicenses);
        }
    }
}