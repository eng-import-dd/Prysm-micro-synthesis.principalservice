using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Moq;
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
using Synthesis.Policy.Models;
using Synthesis.PolicyEvaluator;
using Synthesis.PolicyEvaluator.Permissions;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Email;
using Synthesis.PrincipalService.Exceptions;
using Synthesis.PrincipalService.InternalApi.Enums;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Services;
using Synthesis.PrincipalService.Validators;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.TenantService.InternalApi.Api;
using Synthesis.TenantService.InternalApi.Models;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Controllers
{
    public class UsersControllerTests
    {
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();
        private readonly Mock<IRepository<Group>> _groupRepositoryMock = new Mock<IRepository<Group>>();
        private readonly Mock<IRepository<UserInvite>> _userInviteRepositoryMock = new Mock<IRepository<UserInvite>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<ILicenseApi> _licenseApiMock = new Mock<ILicenseApi>();
        private readonly Mock<IEmailApi> _emailApiMock = new Mock<IEmailApi>();
        private readonly Mock<ITenantApi> _tenantApiMock = new Mock<ITenantApi>();
        private readonly Mock<IProjectApi> _projectApiMock = new Mock<IProjectApi>();
        private readonly Mock<IUserSearchBuilder> _searchBuilderMock = new Mock<IUserSearchBuilder>();
        private readonly Mock<IQueryRunner<User>> _queryRunnerMock = new Mock<IQueryRunner<User>>();
        private readonly Mock<IIdentityUserApi> _identityUserApiMock = new Mock<IIdentityUserApi>();
        private readonly Mock<IEmailSendingService> _emailSendingMock = new Mock<IEmailSendingService>();
        private readonly Mock<ISuperAdminService> _superadminServiceMock = new Mock<ISuperAdminService>();
        private readonly Mock<IPolicyEvaluator> _policyManagerMock = new Mock<IPolicyEvaluator>();

        private readonly UsersController _controller;
        private readonly IMapper _mapper;
        private readonly Mock<IUsersController> _userApiMock = new Mock<IUsersController>();
        private readonly Mock<IUsersController> _mockUserController = new Mock<IUsersController>();
        private readonly Mock<IValidator> _validatorFailsMock = new Mock<IValidator>();

        private readonly Guid _defaultGroupId = Guid.NewGuid();
        private readonly Guid _defaultTenantId = Guid.NewGuid();
        private readonly User _defaultUser = new User
        {
            FirstName = "FirstName",
            LastName = "LastName",
            Email = "cmalyala@prysm.com",
            IsLocked = false,
            IsIdpUser = false,
            LicenseType = LicenseType.UserLicense
        };

        private readonly ClaimsPrincipal _defaultClaimsPrincipal = new ClaimsPrincipal();

        private List<User> _usersInSameProject;
        private List<User> _usersInSameGroup;
        private List<User> _wildcardUsers;
        private List<User> _allUsers;
        private Project _testProject;

        public UsersControllerTests()
        {
            _mapper = new MapperConfiguration(cfg => { cfg.AddProfile<UserProfile>(); }).CreateMapper();

            SetupTestData();

            SetupMocks();

            const string deploymentType = "";

            _controller = new UsersController(_repositoryFactoryMock.Object,
                _validatorLocatorMock.Object,
                _eventServiceMock.Object,
                _loggerFactoryMock.Object,
                _licenseApiMock.Object,
                _emailApiMock.Object,
                _emailSendingMock.Object,
                _mapper,
                deploymentType,
                _searchBuilderMock.Object,
                _queryRunnerMock.Object,
                _tenantApiMock.Object,
                _policyManagerMock.Object,
                _superadminServiceMock.Object,
                _identityUserApiMock.Object);
        }

        private void SetupTestData()
        {
            _usersInSameProject = new List<User>
            {
                new User{Id = Guid.NewGuid()},
                new User{Id = Guid.NewGuid()},
                new User{Id = Guid.NewGuid()}
            };

            _usersInSameGroup = new List<User>
            {
                new User { Id = Guid.NewGuid() },
                new User { Id = Guid.NewGuid() },
                new User { Id = Guid.NewGuid() }
            };

            _wildcardUsers = new List<User>
            {
                new User { Id = Guid.NewGuid() },
                new User { Id = Guid.NewGuid() },
                new User { Id = Guid.NewGuid() },
                new User { Id = Guid.NewGuid() },
                new User { Id = Guid.NewGuid() }
            };

            _allUsers = _wildcardUsers.Concat(_usersInSameProject).Concat(_usersInSameGroup).ToList();

            _testProject = new Project
            {
                UserIds = _usersInSameProject.Select(u => u.Id ?? Guid.Empty).ToList()
            };
        }

        private void SetupMocks()
        {
            // repository mock
            _repositoryFactoryMock.Setup(m => m.CreateRepository<User>())
                .Returns(_userRepositoryMock.Object);

            _repositoryFactoryMock.Setup(m => m.CreateRepository<Group>())
                .Returns(_groupRepositoryMock.Object);

            _repositoryFactoryMock.Setup(m => m.CreateRepository<UserInvite>())
                .Returns(_userInviteRepositoryMock.Object);

            // event service mock
            _eventServiceMock.Setup(m => m.PublishAsync(It.IsAny<ServiceBusEvent<User>>()))
                .Returns(Task.FromResult(0));

            _validatorMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult());

            _validatorFailsMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult { Errors = { new ValidationFailure(string.Empty, string.Empty) } });

            _emailSendingMock.Setup(m => m.SendGuestVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK));

            // validator mock
            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            // logger factory mock
            _loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(_loggerMock.Object);

            // project internal api mock
            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _testProject));

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });

            _groupRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<Group, bool>>>()))
                .ReturnsAsync(new List<Group> { new Group { Id = _defaultGroupId, TenantId = _defaultTenantId, Type = GroupType.Default } }.AsEnumerable());

            _tenantApiMock.Setup(x => x.AddUserToTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK));

            _identityUserApiMock.Setup(x => x.SetPasswordAsync(It.IsAny<IdentityUser>())).ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK));

            _policyManagerMock
                .Setup(x => x.GetPoliciesAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PolicyDocument>
                {
                    new PolicyDocument
                    {
                        Permissions = new List<Permission>
                        {
                            new Permission
                            {
                                Expression = SynthesisPermission.CanManageLicenses.ToString(),
                                Scope = PermissionScope.Allow,
                                Type = PermissionType.Explicit
                            }
                        }
                    }
                }.AsEnumerable());


            _userRepositoryMock
                .Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com", Groups = new List<Guid>() });

            _licenseApiMock
                .Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });

            _tenantApiMock
                .Setup(m => m.AddUserToTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, true));

            _tenantApiMock
                .Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            _userRepositoryMock
                .Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync(new User());

            _userRepositoryMock
                .Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()))
                .ReturnsAsync(new User());
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsFailsAndThrowsCreateUserException()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .Throws<Exception>();

            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var idpUserRequest = new IdpUserRequest
            {
                FirstName = "TestUser",
                LastName = "TestUser"
            };
            await Assert.ThrowsAsync<Exception>(() => _controller.AutoProvisionRefreshGroupsAsync(idpUserRequest, tenantId, createdBy, _defaultClaimsPrincipal));
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsFailsAndThrowsException()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });
            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });

            _validatorMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult
                {
                    Errors =
                    {
                        new ValidationFailure("", "")
                    }
                });

            var tenantId = Guid.Empty;
            var createdBy = Guid.NewGuid();
            var idpUserRequest = new IdpUserRequest
            {
                FirstName = "TestUser",
                LastName = "TestUser"
            };

            _validatorMock.Setup(m => m.Validate(tenantId))
                .Returns(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("", "") }));

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.AutoProvisionRefreshGroupsAsync(idpUserRequest, tenantId, createdBy, _defaultClaimsPrincipal));
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsFailsAndThrowsPromotionFailedException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "user@nodomain.com" });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            _userApiMock.Setup(u => u.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>()))
                .Throws(new PromotionFailedException(""));

            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var idpUserRequest = new IdpUserRequest
            {
                UserId = Guid.NewGuid(),
                FirstName = "TestUser",
                LastName = "TestUser",
                IsGuestUser = true
            };
            await Assert.ThrowsAsync<PromotionFailedException>(() => _controller.AutoProvisionRefreshGroupsAsync(idpUserRequest, tenantId, createdBy, _defaultClaimsPrincipal));
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsReturnsIfSuccessful()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });
            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });

            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var idpUserRequest = new IdpUserRequest
            {
                FirstName = "TestUser",
                LastName = "TestUser"
            };
            var userResponse = await _controller.AutoProvisionRefreshGroupsAsync(idpUserRequest, tenantId, createdBy, _defaultClaimsPrincipal);
            Assert.NotNull(userResponse);
        }

        [Fact]
        public async Task CanPromoteUserThrowsValidationExceptionIfEmailIsEmpty()
        {
            _validatorLocatorMock.Setup(m => m.GetValidator(typeof(EmailValidator)))
                .Returns(_validatorFailsMock.Object);

            var email = "";
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CanPromoteUserAsync(email, Guid.NewGuid()));
        }

        [Fact]
        public async Task CanPromoteUserIfUserExistsInATenant()
        {
            var email = "ch@asd.com";
            var tenantId = Guid.NewGuid();
            _userRepositoryMock
                .Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(() => new List<User> { new User { Email = email, Id = Guid.NewGuid() } }.AsEnumerable());

            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "asd.com" } }));

            _tenantApiMock
                .Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid(), tenantId }.AsEnumerable()));

            var result = await _controller.CanPromoteUserAsync(email, tenantId);
            var response = CanPromoteUserResultCode.UserAccountAlreadyExists;
            Assert.Equal(response, result.ResultCode);
        }

        [Fact]
        public async Task CanPromoteUserNotFoundException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));
            var email = "ch@gmm.com";
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.CanPromoteUserAsync(email, Guid.NewGuid()));
        }

        [Fact]
        public async Task CanPromoteUserSuccess()
        {
            var email = "ch@prysm.com";
            var tenantId = Guid.NewGuid();
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(() =>
                {
                    var userList = new List<User> { new User { Email = email, Id = Guid.NewGuid()} };

                    var items = userList;
                    return Task.FromResult(items.AsEnumerable());
                });

            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "prysm.com" } }));

            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            var result = await _controller.CanPromoteUserAsync(email, tenantId);
            var response = CanPromoteUserResultCode.UserCanBePromoted;
            Assert.Equal(response, result.ResultCode);
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsDuplicateUserGroupValidationException()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()))
                .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User
                {
                    //TenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3"),
                    Groups = new List<Guid> { Guid.Parse("12bf0424-bd5e-4af0-affb-d48485ae7115") }
                }));

            var newUserGroupRequest = new UserGroup
            {
                UserId = Guid.Parse("79d68d52-838a-40e2-a83d-c509ba550a30"),
                GroupId = Guid.Parse("12bf0424-bd5e-4af0-affb-d48485ae7115")
            };

            var userId = Guid.Parse("79d68d52-838a-40e2-a83d-c509ba550a30");
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { tenantId }.AsEnumerable()));
            _mockUserController.Setup(m => m.CreateUserGroupAsync(newUserGroupRequest, tenantId, It.IsAny<Guid>()))
                .Returns(Task.FromResult(new UserGroup()));

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId));
            Assert.Single(ex.Errors.ToList());
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsNoUserFoundValidationException()
        {
            _mockUserController.Setup(m => m.CreateUserGroupAsync(new UserGroup(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new UserGroup()));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult<User>(null));

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserGroupAsync(new UserGroup(), It.IsAny<Guid>(), It.IsAny<Guid>()));
            Assert.Single(ex.Errors.ToList());
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsUserGroupIfSuccessful()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User
                {
                    Groups = new List<Guid> { Guid.NewGuid() }
                });

            var newUserGroupRequest = new UserGroup
            {
                UserId = Guid.Parse("79d68d52-838a-40e2-a83d-c509ba550a30"),
                GroupId = Guid.Parse("12bf0424-bd5e-4af0-affb-d48485ae7115")
            };

            var userId = Guid.Parse("79d68d52-838a-40e2-a83d-c509ba550a30");
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { tenantId }.AsEnumerable()));
            _mockUserController.Setup(m => m.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId))
                .Returns(Task.FromResult(new UserGroup()));

            var result = await _controller.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId);
            Assert.IsType<UserGroup>(result);
        }

        [Fact]
        public async Task CreateUserGroupThrowsValidationErrorIfNonSuperAdminCreatesSuperAdminGroup()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User
                {
                    Groups = new List<Guid> { Guid.NewGuid() }
                });

            _superadminServiceMock
                .Setup(x => x.IsSuperAdminAsync(It.IsAny<Guid>()))
                .ReturnsAsync(false);

            var newUserGroupRequest = new UserGroup
            {
                UserId = Guid.NewGuid(),
                GroupId = GroupIds.SuperAdminGroupId
            };

            _tenantApiMock
                .Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { _defaultTenantId }.AsEnumerable()));

            _mockUserController
                .Setup(m => m.CreateUserGroupAsync(newUserGroupRequest, _defaultTenantId, Guid.NewGuid()))
                .Returns(Task.FromResult(new UserGroup()));

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserGroupAsync(newUserGroupRequest, _defaultTenantId, Guid.NewGuid()));
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsValidationException()
        {
            _mockUserController
                .Setup(m => m.CreateUserGroupAsync(new UserGroup(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new UserGroup()));

            _userRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserGroupAsync(new UserGroup(), It.IsAny<Guid>(), It.IsAny<Guid>()));
            Assert.Single(ex.Errors.ToList());
        }

        [Fact]
        public async Task CreatUserAsyncSuccessAsync()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });

            var createUserRequest = CreateUserRequest.Example();
            var createdBy = Guid.NewGuid();
            var user = await _controller.CreateUserAsync(createUserRequest, createdBy, _defaultClaimsPrincipal);

            _userRepositoryMock.Verify(m => m.CreateItemAsync(It.IsAny<User>()));
            _emailApiMock.Verify(m => m.SendWelcomeEmail(It.IsAny<UserEmailRequest>()));
            _eventServiceMock.Verify(m => m.PublishAsync(It.Is<ServiceBusEvent<User>>(e => e.Name == "UserCreated")));

            Assert.NotNull(user);
            Assert.Equal(user.CreatedBy, createdBy);
            Assert.False(user.IsLocked);
        }

        [Fact]
        public async Task NewUserIsAssignedDefaultLicenseTypeIfCurrentUserDoesNotHaveCanManageLicensePermissionAsync()
        {
            _policyManagerMock
                .Setup(x => x.GetPoliciesAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PolicyDocument>
                {
                    new PolicyDocument
                    {
                        Permissions = new List<Permission>()
                    }
                }.AsEnumerable());

            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });

            var createUserRequest = CreateUserRequest.Example();
            createUserRequest.LicenseType = LicenseType.UserLicense;
            await _controller.CreateUserAsync(createUserRequest, Guid.NewGuid(), _defaultClaimsPrincipal);

            _userRepositoryMock.Verify(m => m.CreateItemAsync(It.Is<User>(u => u.LicenseType == LicenseType.Default)));
        }

        [Fact]
        public async Task PromotedUserIsAssignedDefaultLicenseTypeIfCurrentUserDoesNotHaveCanManageLicensePermissionAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com" });

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });

            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "test.com" } }));

            _tenantApiMock.Setup(m => m.AddUserToTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, true));
            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            _policyManagerMock
                .Setup(x => x.GetPoliciesAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PolicyDocument>
                {
                    new PolicyDocument
                    {
                        Permissions = new List<Permission>()
                    }
                }.AsEnumerable());

            await _controller.PromoteGuestUserAsync(Guid.NewGuid(), _defaultTenantId, LicenseType.UserLicense, _defaultClaimsPrincipal);

            _licenseApiMock
                .Verify(x => x.AssignUserLicenseAsync(It.Is<UserLicenseDto>(l => l.LicenseType == LicenseType.Default.ToString())));
        }

        [Fact]
        public async Task CreatUserAsyncThrowsValidationExceptionIfUserNameOrEmailIsDuplicateAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { new User() });

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last" };
            createUserRequest.TenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserAsync(createUserRequest, createdBy, _defaultClaimsPrincipal));

            Assert.Equal(2, ex.Errors.ToList().Count); //Duplidate Email & Duplicate username errors
        }

        [Fact]
        public async Task CreatUserAsyncThrowsValidationExceptionIfUserNameOrEmailOrLdapIsDuplicateAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { new User() });

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", LdapId = "ldap" };
            createUserRequest.TenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserAsync(createUserRequest, createdBy, _defaultClaimsPrincipal));

            Assert.Equal(3, ex.Errors.ToList().Count); //Duplidate Email, Duplicate Ldap & Duplicate username errors
        }

        [Fact]
        public async Task CreatUserAsyncForEnterpriseUserAddsBuiltInGroups()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "user@nodomain.com" });

            _groupRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<Group, bool>>>()))
                .ReturnsAsync(new List<Group> { new Group() });

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap", TenantId = Guid.NewGuid(), UserType = UserType.Enterprise};
            await _controller.CreateUserAsync(createUserRequest, Guid.NewGuid(), _defaultClaimsPrincipal);

            _userRepositoryMock.Verify(x => x.CreateItemAsync(It.Is<User>(u => u.Groups.Count == 2)));
        }

        [Fact]
        public async Task CreatUserAsyncThrowsExceptionIfDuplicateDefaultGroupExists()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });

            _groupRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<Group, bool>>>()))
                .ReturnsAsync(new List<Group> { new Group(), new Group() });

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap", TenantId = Guid.NewGuid() };
            await Assert.ThrowsAsync<Exception>(() => _controller.CreateUserAsync(createUserRequest, Guid.NewGuid(), _defaultClaimsPrincipal));
        }

        [Fact]
        public async Task CreatUserAsyncThrowsExceptionIDefaultGroupIsMissing()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });

            _groupRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<Group, bool>>>()))
                .ReturnsAsync(new List<Group>());

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap", TenantId = Guid.NewGuid() };
            await Assert.ThrowsAsync<Exception>(() => _controller.CreateUserAsync(createUserRequest, Guid.NewGuid(), _defaultClaimsPrincipal));
        }

        [Fact]
        public async Task CreatUserAsyncUserIsLockedIfLicenseApiThrowsExceptionAsync()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });

            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> {Guid.NewGuid()}.AsEnumerable()));

            _groupRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Group, bool>>>()))
                .ReturnsAsync(new List<Group> { new Group { Id = Guid.NewGuid() } }.AsEnumerable());

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>())).Throws<Exception>();

            var createUserRequest = CreateUserRequest.Example();
            var createdBy = Guid.NewGuid();
            var user = await _controller.CreateUserAsync(createUserRequest, createdBy, _defaultClaimsPrincipal);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));

            Assert.True(user.IsLocked);
        }

        [Fact]
        public async Task CreatUserAsyncUserIsLockedIfNoLicenseAvailableAsync()
        {
            var createdBy = Guid.NewGuid();
            var adminGroupId = Guid.NewGuid();

            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) =>
                {
                    u.Id = Guid.NewGuid();
                    return u;
                });

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());

            _groupRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Group, bool>>>()))
                .ReturnsAsync(new List<Group> { new Group { Id = adminGroupId } }.AsEnumerable());

            _userRepositoryMock.SetupSequence(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>())
                .ReturnsAsync(new List<User>())
                .ReturnsAsync(new List<User>())
                .ReturnsAsync(new List<User> { new User { FirstName = "admin", Email = "admin@test.com" } }.AsEnumerable());

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Failed });

            var createUserRequest = CreateUserRequest.Example();
            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));
            var user = await _controller.CreateUserAsync(createUserRequest, createdBy, _defaultClaimsPrincipal);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));
            _emailApiMock.Verify(m => m.SendUserLockedMail(It.IsAny<LockUserRequest>()));

            Assert.NotNull(user);
            Assert.Equal(user.CreatedBy, createdBy);
            Assert.True(user.IsLocked);
        }

        [Fact]
        public async Task GetGuestUserForTenantReturnsEmptyResult()
        {
            _userRepositoryMock.Setup(m => m.GetOrderedPaginatedItemsAsync(It.IsAny<OrderedQueryParameters<User, string>>()))
                .ReturnsAsync(new PaginatedResponse<User> { ContinuationToken = "", Items = new List<User>() });
            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));


            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new[] { new TenantDomain { Domain = "test.com" } }));

            var tenantId = Guid.NewGuid();
            var userFilteringOptions = new UserFilteringOptions();

            var result = await _controller.GetGuestUsersForTenantAsync(tenantId, userFilteringOptions);
            Assert.Empty(result.List);
            Assert.Equal(0, result.FilteredRecords);
            Assert.True(result.IsLastChunk);
            Assert.Null(result.SearchValue);
            Assert.Null(result.SortColumn);
        }

        [Fact]
        public async Task GetGuestUsersForTenantSuccess()
        {
            _userRepositoryMock.Setup(m => m.GetOrderedPaginatedItemsAsync(It.IsAny<OrderedQueryParameters<User, string>>()))
                .ReturnsAsync(new PaginatedResponse<User> { ContinuationToken = "test", Items = new List<User> { new User(), new User(), new User() } });
            var tenantId = Guid.NewGuid();
            var userFilteringOptions = new UserFilteringOptions();
            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new[] { new TenantDomain { Domain = "test.com" } }));

            var result = await _controller.GetGuestUsersForTenantAsync(tenantId, userFilteringOptions);

            Assert.Equal(3, result.List.Count);
            Assert.Equal(3, result.FilteredRecords);
            Assert.Equal("test", result.ContinuationToken);
            Assert.False(result.IsLastChunk);
        }

        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsLicenseTypeIfExists()
        {
            var userId = Guid.Parse("4d1b116e-debe-47e2-b0bd-6d7856b0c616");
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");

            _licenseApiMock.Setup(m => m.GetUserLicenseDetailsAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(new UserLicenseResponse
                {
                    LicenseAssignments = new List<UserLicenseDto>
                    {
                        new UserLicenseDto
                        {
                            LicenseType = LicenseType.UserLicense.ToString()
                        }
                    }
                });
            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { tenantId }.AsEnumerable()));
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User
                {
                    Id = userId,
                });
            var result = await _controller.GetLicenseTypeForUserAsync(userId, tenantId);
            Assert.IsType<LicenseType>(result);
        }

        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsUserNotFoundException()
        {
            var userId = Guid.Parse("4d1b116e-debe-47e2-b0bd-6d7856b0c616");
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetLicenseTypeForUserAsync(userId, tenantId));
        }

        [Fact]
        public async Task GetUserByNamesReturnsExpectedName()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>() { new User() { FirstName = "Joe", LastName = "Blow" } });

            var usernames = (await _controller.GetNamesForUsers(new List<Guid>())).ToList();

            Assert.NotEmpty(usernames);
            Assert.Equal("Joe", usernames.First().FirstName);
            Assert.Equal("Blow", usernames.First().LastName);
        }

        [Fact]
        public async Task GetUserByNamesThrowsValidationFailedExceptionIfValidationFails()
        {
            _validatorMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult
                {
                    Errors = { new ValidationFailure(string.Empty, string.Empty, string.Empty) }
                });

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.GetNamesForUsers(new List<Guid>()));
        }

        /// <summary>
        ///     Gets the user by identifier asynchronous returns user if exists.
        /// </summary>
        /// <returns>Task object.</returns>
        [Fact]
        public async Task GetUserByIdAsyncReturnsUserIfExistsAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());

            var userId = Guid.NewGuid();
            var result = await _controller.GetUserAsync(userId);

            Assert.IsType<User>(result);
        }

        /// <summary>
        ///     Gets the user by identifier asynchronous throws not found exception if user does not exist.
        /// </summary>
        /// <returns>Task object.</returns>
        [Fact]
        public async Task GetUserByIdAsyncThrowsNotFoundExceptionIfUserDoesNotExistAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));

            var userId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetUserAsync(userId));
        }

        [Fact]
        public async Task GetUserByIdBasicReturnsUserIfExists()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());

            var userId = Guid.NewGuid();
            var result = await _controller.GetUserAsync(userId);

            Assert.IsType<User>(result);
        }

        [Fact]
        public async Task GetUserByIdBasicThrowsNotFoundExceptionIfUserDoesNotExist()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));

            var userId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetUserAsync(userId));
        }

        [Fact]
        public async Task GetUserByIdsThrowsValidationFailedExceptionIfValidationFails()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            _validatorMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult
                {
                    Errors = { new ValidationFailure(string.Empty, string.Empty, string.Empty) }
                });

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.GetUsersByIdsAsync(new List<Guid>()));
        }

        private Mock<IBatchResult<User>> WrapUsersInBatchMock(IEnumerable<User> users)
        {
            var listMock = new Mock<IEnumerable<User>>();
            listMock
                .Setup(x => x.GetEnumerator())
                .Returns(users.GetEnumerator);

            return listMock.As<IBatchResult<User>>();
        }

        [Fact]
        public async Task GetUsersBasicAsyncReturnsUsersIfExists()
        {
            const int count = 3;
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(() =>
                {
                    var userList = new List<User>();
                    for (var i = 0; i < count; i++)
                    {
                        userList.Add(new User());
                    }

                    var items = userList;
                    return Task.FromResult(items.AsEnumerable());
                });
            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            var batchMock = WrapUsersInBatchMock(new List<User> { new User(), new User(), new User() } );

            _queryRunnerMock.Setup(x => x.RunQuery(It.IsAny<IQueryable<User>>()))
                .ReturnsAsync(batchMock.Object);

            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userFilteringOptions = new UserFilteringOptions();

            var result = await _controller.GetUsersBasicAsync(tenantId, userId, userFilteringOptions);
            Assert.Equal(count, result.List.Count);
        }

        [Fact]
        public async Task GetUsersByIdsReturnsCollectionOfUsersIfSuccessful()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(Task.FromResult((IEnumerable<User>)new List<User>
                {
                    new User()
                }));

            var result = await _controller.GetUsersByIdsAsync(new List<Guid>());
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetUsersForTenantIfExists()
        {
            const int count = 3;
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(() =>
                {
                    var userList = new List<User>();
                    for (var i = 0; i < count; i++)
                    {
                        userList.Add(new User());
                    }

                    var items = userList;
                    return Task.FromResult(items.AsEnumerable());
                });

            var batchMock = WrapUsersInBatchMock(new List<User> { new User(), new User(), new User() });

            _queryRunnerMock.Setup(x => x.RunQuery(It.IsAny<IQueryable<User>>()))
                .ReturnsAsync(batchMock.Object);

            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var userFilteringOptions = new UserFilteringOptions();
            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { userId }.AsEnumerable()));
            var result = await _controller.GetUsersForTenantAsync(userFilteringOptions, tenantId, userId);
            Assert.Equal(count, result.List.Count);
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task GetUsersForGroupReturnsUsersIfExists()
        {
            var validGroupId = Guid.NewGuid();

            _mockUserController.Setup(m => m.GetUserIdsByGroupIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new List<Guid>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                .Returns(Task.FromResult(Enumerable.Empty<User>()));
            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }.AsEnumerable()));
            var result = await _controller.GetUserIdsByGroupIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>());

            Assert.IsType<List<Guid>>(result);
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task GetUsersForGroupThrowsNotFoundExceptionIfNonSuperAdminTriesToGetSuperAdminUsers()
        {
            _superadminServiceMock
                .Setup(x => x.IsSuperAdminAsync(It.IsAny<Guid>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetUserIdsByGroupIdAsync(GroupIds.SuperAdminGroupId, It.IsAny<Guid>(), It.IsAny<Guid>()));
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task GetUsersForGroupThrowsNotFoundExceptionIfGroupDoesNotExist()
        {
            var validGroupId = Guid.NewGuid();

            _mockUserController.Setup(m => m.GetUserIdsByGroupIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                .Throws(new NotFoundException(string.Empty));
            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }.AsEnumerable()));
            var result = await _controller.GetUserIdsByGroupIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>());

            Assert.Empty(result);
        }

        [Fact]
        public async Task InOnPremDeploymentUserCreationOnLocalAccountShouldFailAsync()
        {
            var deploymentType = "OnPrem";
            var controller = new UsersController(_repositoryFactoryMock.Object,
                _validatorLocatorMock.Object,
                _eventServiceMock.Object,
                _loggerFactoryMock.Object,
                _licenseApiMock.Object,
                _emailApiMock.Object,
                _emailSendingMock.Object,
                _mapper,
                deploymentType,
                _searchBuilderMock.Object,
                _queryRunnerMock.Object,
                _tenantApiMock.Object,
                _policyManagerMock.Object,
                _superadminServiceMock.Object,
                _identityUserApiMock.Object);

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap", TenantId = Guid.Parse("2D907264-8797-4666-A8BB-72FE98733385")  };
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => controller.CreateUserAsync(createUserRequest, createdBy, _defaultClaimsPrincipal));

            Assert.Single(ex.Errors.ToList());
        }

        [Fact]
        public async Task InOnPremDeploymentUserCreationOnPrysmAccountShouldFailAsync()
        {
            var deploymentType = "OnPrem";
            var controller = new UsersController(_repositoryFactoryMock.Object,
                _validatorLocatorMock.Object,
                _eventServiceMock.Object,
                _loggerFactoryMock.Object,
                _licenseApiMock.Object,
                _emailApiMock.Object,
                _emailSendingMock.Object,
                _mapper,
                deploymentType,
                _searchBuilderMock.Object,
                _queryRunnerMock.Object,
                _tenantApiMock.Object,
                _policyManagerMock.Object,
                _superadminServiceMock.Object,
                _identityUserApiMock.Object);

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap", TenantId = Guid.Parse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3") };
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => controller.CreateUserAsync(createUserRequest, createdBy, _defaultClaimsPrincipal));

            Assert.Single(ex.Errors.ToList());
        }

        [Fact]
        public async Task LockUserAsyncIfAssigningLicenseFails()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());
            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>())).ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Failed });
            var userId = Guid.NewGuid();
            
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.LockOrUnlockUserAsync(userId, _defaultTenantId, false));
        }

        [Fact]
        public async Task LockUserAsyncIfReleaseLicenseFails()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());
            _licenseApiMock.Setup(m => m.ReleaseUserLicenseAsync(It.IsAny<UserLicenseDto>())).ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Failed });
            var userId = Guid.NewGuid();
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.LockOrUnlockUserAsync(userId, _defaultTenantId, true));
        }

        [Fact]
        public async Task UserIsLockedIfUserExist()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());
            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });
            var userId = Guid.NewGuid();
            var result = await _controller.LockOrUnlockUserAsync(userId, _defaultTenantId, false);
            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));
            Assert.True(result);
        }

        [Fact]
        public async Task InvalidOperationIsThrownIfTheUserIsTheLastSuperAdmin()
        {
            _superadminServiceMock
                .Setup(x => x.IsLastRemainingSuperAdminAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.LockOrUnlockUserAsync(Guid.NewGuid(), _defaultTenantId, true));
        }

        [Fact]
        public async Task LockUserThrowsNotFoundIfUserNotFound()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });
            var userId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetUserAsync(userId));
            var result = await _controller.LockOrUnlockUserAsync(userId, _defaultTenantId, false);
            Assert.False(result);
        }

        [Fact]
        public async Task PromoteGuestAutoShouldFaileIfNoLicenceAvailableAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com" });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            await Assert.ThrowsAsync<PromotionFailedException>(() => _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense, _defaultClaimsPrincipal, true));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFaileIfNoLicenceAvailableAsync()
        {
            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Failed });

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com" });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());


            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "test.com" } }));

            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));
            _tenantApiMock.Setup(m => m.AddUserToTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK));

            var userid = Guid.NewGuid();
            await Assert.ThrowsAsync<LicenseAssignmentFailedException>(() => _controller.PromoteGuestUserAsync(userid, _defaultTenantId, LicenseType.UserLicense, _defaultClaimsPrincipal));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Exactly(1));
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFailIfIfEmailIsNotWhitelistedAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@testtest.com"});

            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "test.com" } }));

            var userid = Guid.NewGuid();
            await Assert.ThrowsAsync < PromotionFailedException >(()=> _controller.PromoteGuestUserAsync(userid, _defaultTenantId, LicenseType.UserLicense, _defaultClaimsPrincipal));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFailIfIfUserEmailIsEmptyAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid() });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            var userid = Guid.NewGuid();
            await Assert.ThrowsAsync<PromotionFailedException>(() => _controller.PromoteGuestUserAsync(userid, _defaultTenantId, LicenseType.UserLicense, _defaultClaimsPrincipal));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFailIfIfUserIsAlreadyInTenantAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com" });

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { _defaultTenantId }.AsEnumerable()));

            var userid = Guid.NewGuid();
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, _defaultTenantId, LicenseType.UserLicense, _defaultClaimsPrincipal);

            Assert.Equal(CanPromoteUserResultCode.UserAccountAlreadyExists, promoteResponse);
        }

        [Fact]
        public async Task GetGroupsForUserSuccess()
        {
            Guid? userId = Guid.NewGuid();
            _mockUserController.Setup(m => m.GetGroupIdsByUserIdAsync(It.IsAny<Guid>()))
                               .Returns(Task.FromResult(new List<Guid>()));
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = userId, Groups = new List<Guid>() });
            var result = await _controller.GetGroupIdsByUserIdAsync((Guid)userId);
            Assert.IsType<List<Guid>>(result);
        }

        [Fact]
        public async Task GetGroupsForUserThrowsNotFoundException()
        {
            Guid? userId = Guid.NewGuid();
            const string exception = "Resource Not Found";
            _mockUserController.Setup(m => m.GetGroupIdsByUserIdAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new NotFoundException(exception));
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new NotFoundException(exception));
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetGroupIdsByUserIdAsync((Guid)userId));
        }

        [Fact]
        public async Task GetGroupsForUserThrowsException()
        {
            Guid? userId = Guid.NewGuid();
            _mockUserController.Setup(m => m.GetGroupIdsByUserIdAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new Exception());
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<Exception>(() => _controller.GetGroupIdsByUserIdAsync((Guid)userId));
        }

        [Fact]
        public async Task GetGroupsForUserThrowsValidationException()
        {
            Guid? userId = Guid.NewGuid();
            _mockUserController.Setup(m => m.GetGroupIdsByUserIdAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.GetGroupIdsByUserIdAsync((Guid)userId));
        }

        [Fact]
        public async Task PromoteGuestSuccssTestAsync()
        {
            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "test.com" } }));
            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense, _defaultClaimsPrincipal);

            _emailApiMock.Verify(m => m.SendWelcomeEmail(It.IsAny<UserEmailRequest>()));

            Assert.Equal(CanPromoteUserResultCode.UserCanBePromoted, promoteResponse);
        }

        [Fact]
        public async Task RemoveUserFromPermissionGroupSuccess()
        {
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            _userRepositoryMock
                .Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Groups = new List<Guid> { groupId } });

            _userRepositoryMock
                .Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()))
                .ReturnsAsync(new User());

            var result = await _controller.RemoveUserFromPermissionGroupAsync(userId, groupId, userId);
            Assert.True(result);
        }

        [Fact]
        public async Task UserIsNotRemovedFromSuperAdminGroupIfCurrentUserIsNotASuperAdmin()
        {
                var userId = Guid.NewGuid();

                await _controller.RemoveUserFromPermissionGroupAsync(userId, GroupIds.SuperAdminGroupId, userId);

                _userRepositoryMock
                    .Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
        }


        [Fact]
        public async Task InvalidOperationIsThrownIfFinalSuperAdminGroupIsRemoved()
        {
            var userId = Guid.NewGuid();

            _superadminServiceMock
                .Setup(x => x.IsSuperAdminAsync(It.IsAny<Guid>()))
                .ReturnsAsync(true);

            _superadminServiceMock.Setup(x => x.IsLastRemainingSuperAdminAsync(It.IsAny<Guid>())).ReturnsAsync(true);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.RemoveUserFromPermissionGroupAsync(userId, GroupIds.SuperAdminGroupId, userId));
        }

        [Fact]
        public async Task RemoveUserFromPermissionGroupThrowsDocumentNotFound()
        {
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(() =>
                {
                    var userList = new List<User>();
                    for (var i = 0; i < 3; i++)
                    {
                        userList.Add(new User());
                    }

                    var items = userList;
                    return Task.FromResult(items.AsEnumerable());
                });

            _userRepositoryMock
                .Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new DocumentNotFoundException());

            await Assert.ThrowsAsync<DocumentNotFoundException>(() => _controller.RemoveUserFromPermissionGroupAsync(userId, groupId, userId));
        }

        [Fact]
        public async Task RemoveUserFromPermissionGroupThrowsException()
        {
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(() =>
                {
                    var userList = new List<User>();
                    for (var i = 0; i < 3; i++)
                    {
                        userList.Add(new User());
                    }

                    var items = userList;
                    return Task.FromResult(items.AsEnumerable());
                });
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User { Groups = new List<Guid> { groupId } }));
            _userRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()))
                .ThrowsAsync(new Exception());

            await Assert.ThrowsAsync<Exception>(() => _controller.RemoveUserFromPermissionGroupAsync(userId, groupId, userId));
        }

        [Fact]
        public async Task UpdateUserNotFoundException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));
            var userId = Guid.NewGuid();
            var user = new User();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.UpdateUserAsync(userId, user, _defaultClaimsPrincipal));
        }

        [Fact]
        public async Task UpdateUserSuccess()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());

            var userId = Guid.NewGuid();
            var user = new User
            {
                FirstName = "FirstName",
                LastName = "LastName",
                Email = "cmalyala@prysm.com",
                IsLocked = false,
                IsIdpUser = false
            };

            var result = await _controller.UpdateUserAsync(userId, user, _defaultClaimsPrincipal);
            Assert.IsType<User>(result);
        }

        [Fact]
        public async Task UpdateToUserUpdatesLicenseIfCurrentUserHasCanManageLicensePermission()
        {
            var newUser = new User { LicenseType = LicenseType.UserLicense };
            var oldUser = new User { LicenseType = LicenseType.LegacyLicense };

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(oldUser);

            await _controller.UpdateUserAsync(_defaultUser.Id.GetValueOrDefault(), newUser, _defaultClaimsPrincipal);

            _userRepositoryMock
                .Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.Is<User>(u => u.LicenseType == LicenseType.UserLicense)));
        }

        [Fact]
        public async Task UpdateToUserUpdatesLicenseIsNotUpdatedIfCurrentUserDoesNotHaveCanManageLicensePermission()
        {
            var newUser = new User { LicenseType = LicenseType.UserLicense };
            var oldUser = new User { LicenseType = LicenseType.LegacyLicense };

            _policyManagerMock
                .Setup(x => x.GetPoliciesAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PolicyDocument>
                {
                    new PolicyDocument
                    {
                        Permissions = new List<Permission>
                        {
                            new Permission()
                        }
                    }
                });

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(oldUser);

            await _controller.UpdateUserAsync(_defaultUser.Id.GetValueOrDefault(), newUser, _defaultClaimsPrincipal);

            _userRepositoryMock
                .Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.Is<User>(u => u.LicenseType == LicenseType.LegacyLicense)));
        }

        [Fact]
        public async Task UpdateUserValidationException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());
            var userId = Guid.NewGuid();
            var user = new User();
            _validatorMock.Setup(m => m.Validate(userId))
                .Returns(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("", "") }));
            _validatorMock.Setup(m => m.Validate(user))
                .Returns(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("", ""), new ValidationFailure("", "") }));
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.UpdateUserAsync(userId, user, _defaultClaimsPrincipal));
            Assert.Equal(3, ex.Errors.ToList().Count);
        }

        [Fact]
        public async Task GetUserByEmailOrUserNameIfExistsAsync()
        {
            var validEmail = "smm@pry.com";
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                               .ReturnsAsync(new List<User>() { new User() { Email = validEmail } });

            var result = await _controller.GetUserByUserNameOrEmailAsync(validEmail);

            Assert.IsType<User>(result);
        }

        [Fact]
        public async Task GetUserByEmailOrUserNameIfDoesntExistsAsync()
        {
            var validEmail = "smm@pry.com";
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                               .ThrowsAsync(new NotFoundException("Not found"));
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetUserByUserNameOrEmailAsync(validEmail));
        }

        [Fact]
        public async Task CreateGuestBadRequestThrowsValidationfailed()
        {
            _validatorLocatorMock.Setup(m => m.GetValidator(typeof(CreateGuestUserRequestValidator)))
                .Returns(_validatorFailsMock.Object);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateGuestUserAsync(CreateUserRequest.GuestUserExample()));
        }

        [Fact]
        public async Task CreateGuestForExistingUserThrowsUserExists()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { User.Example() });

            await Assert.ThrowsAsync<UserExistsException>(() => _controller.CreateGuestUserAsync(CreateUserRequest.GuestUserExample()));
        }

        [Fact]
        public async Task CreateGuestForUninvitedUserSucceeds()
        {
            _userInviteRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<UserInvite, bool>>>()))
                .ReturnsAsync(new List<UserInvite>());

            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>() ))
                .ReturnsAsync(User.GuestUserExample());

            var result = await _controller.CreateGuestUserAsync(CreateUserRequest.GuestUserExample());

            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateGuestCreatesUser()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .Returns(Task.FromResult(User.GuestUserExample()));

            await _controller.CreateGuestUserAsync(CreateUserRequest.GuestUserExample());

            _userRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<User>()));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreateGuestReturnsExpectedIsEmailVerificationRequired(bool emailVerificationRequired)
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .Returns(Task.FromResult(User.GuestUserExample()));

            var requestModel = CreateUserRequest.GuestUserExample();
            requestModel.EmailVerificationRequired = emailVerificationRequired;

            var response = await _controller.CreateGuestUserAsync(requestModel);

            Assert.Equal(response.IsEmailVerificationRequired, emailVerificationRequired);
        }

        [Fact]
        public async Task CreateGuestCallsSetPassword()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync(User.GuestUserExample());

            await _controller.CreateGuestUserAsync(CreateUserRequest.GuestUserExample());

            _identityUserApiMock.Verify(x => x.SetPasswordAsync(It.IsAny<IdentityUser>()));
        }

        [Fact]
        public async Task CreateGuestSendsSetPasswordErrorDeletesUser()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync(User.GuestUserExample());
            _identityUserApiMock.Setup(m => m.SetPasswordAsync(It.IsAny<IdentityUser>()))
                .Throws<Exception>();

            await Assert.ThrowsAsync<Exception>(() => _controller.CreateGuestUserAsync(CreateUserRequest.GuestUserExample()));

            _userRepositoryMock.Verify(x => x.DeleteItemAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task CreateGuestSendsVerificationEmail()
        {
            var example = CreateUserRequest.GuestUserExample();
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync(User.GuestUserExample());

            await _controller.CreateGuestUserAsync(CreateUserRequest.GuestUserExample());

            _emailSendingMock.Verify(x => x.SendGuestVerificationEmailAsync(example.FirstName, example.Email, example.Redirect, It.IsAny<Guid?>()));
        }

        [Fact]
        public async Task CreateGuestSendsEvent()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync(User.GuestUserExample());

            await _controller.CreateGuestUserAsync(CreateUserRequest.GuestUserExample());

            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<User>>()));
        }

        [Fact]
        public async Task CreateGuestReturnsUser()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync(User.GuestUserExample());

            var result = await _controller.CreateGuestUserAsync(CreateUserRequest.GuestUserExample());
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateGuestTrimsWhiteSpaceInName()
        {
            const string firstName = "  FirstName  ";
            const string lastName = "  LastName  ";

            var request = CreateUserRequest.GuestUserExample();
            request.FirstName = firstName;
            request.LastName = lastName;

            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .Returns<User>(Task.FromResult);

            var result = await _controller.CreateGuestUserAsync(request);

            Assert.Equal(firstName.Trim(), result.User.FirstName);
            Assert.Equal(lastName.Trim(), result.User.LastName);
        }

        [Fact]
        public async Task GetTenantUsersFromDbReturnsCorrectChunkOfUsersWhenPageNumberIsSpecified()
        {
            var filteringOptions = new UserFilteringOptions
            {
                PageNumber = 2,
                PageSize = 3,
            };

            _tenantApiMock.Setup(x => x.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _allUsers.Select(y => y.Id ?? Guid.Empty)));

            var batchMock = WrapUsersInBatchMock(_allUsers);

            _queryRunnerMock.Setup(x => x.RunQuery(It.IsAny<IQueryable<User>>()))
                .ReturnsAsync(batchMock.Object);

            var result = await _controller.GetTenantUsersFromDb(Guid.NewGuid(), Guid.NewGuid(), filteringOptions);

            Assert.Equal(result.List[0], _allUsers[3]);
        }

        [Fact]
        public async Task GetTenantUsersFromDbReturnsCorrectNumberOfUsersWhenPageSizeIsSpecified()
        {
            var filteringOptions = new UserFilteringOptions
            {
                PageSize = 3
            };

            _tenantApiMock.Setup(x => x.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _allUsers.Select(y => y.Id ?? Guid.Empty)));

            var batchMock = WrapUsersInBatchMock(_allUsers);

            _queryRunnerMock.Setup(x => x.RunQuery(It.IsAny<IQueryable<User>>()))
                .ReturnsAsync(batchMock.Object);

            var result = await _controller.GetTenantUsersFromDb(Guid.NewGuid(), Guid.NewGuid(), filteringOptions);

            Assert.Equal(3, result.List.Count);
        }

        [Fact]
        public async Task GetTenantUsersFromDbReturnsAllFilteredUsersIfPageSizeIsNotSpecified()
        {
            var filteringOptions = new UserFilteringOptions();

            _tenantApiMock.Setup(x => x.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _allUsers.Select(y => y.Id ?? Guid.Empty)));

            var batchMock = WrapUsersInBatchMock(_allUsers);

            _queryRunnerMock.Setup(x => x.RunQuery(It.IsAny<IQueryable<User>>()))
                .ReturnsAsync(batchMock.Object);

            var result = await _controller.GetTenantUsersFromDb(Guid.NewGuid(), Guid.NewGuid(), filteringOptions);

            Assert.Equal(_allUsers.Count, result.List.Count);
        }

        #region SendVerificationEmail

        [Fact]
        public async Task SendGuestVerificationEmailAsyncWithInvalidRequestThrowsValidationFailedException()
        {
            _validatorLocatorMock.Setup(m => m.GetValidator(typeof(GuestVerificationEmailRequestValidator)))
                .Returns(_validatorFailsMock.Object);

            var request = GuestVerificationEmailRequest.Example();

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.SendGuestVerificationEmailAsync(request));
        }

        [Fact]
        public async Task SendGuestVerificationEmailAsyncSendsEmail()
        {
            var request = GuestVerificationEmailRequest.Example();

            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>
                {
                    new User
                    {
                        Id = Guid.NewGuid(),
                        FirstName = request.FirstName,
                        Email = request.Email,
                        EmailVerificationId = Guid.NewGuid()
                    }
                });

            await _controller.SendGuestVerificationEmailAsync(request);

            _emailSendingMock.Verify(x => x.SendGuestVerificationEmailAsync(request.FirstName, request.Email, request.Redirect, It.IsAny<Guid?>()));
        }

        [Fact]
        public async Task SendGuestVerificationEmailThrowsEmailAlreadyVerifiedExceptionIfEmailAlreadyVerified()
        {
            var request = GuestVerificationEmailRequest.Example();

            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>
                {
                    new User
                    {
                        Id = Guid.NewGuid(),
                        FirstName = request.FirstName,
                        Email = request.Email,
                        EmailVerificationId = Guid.NewGuid(),
                        IsEmailVerified = true
                    }
                });

            await Assert.ThrowsAsync<EmailAlreadyVerifiedException>(() => _controller.SendGuestVerificationEmailAsync(request));
        }

        [Fact]
        public async Task SendGuestVerificationEmailThrowsEmailRecentlySentExceptionIfEmailRecentlySent()
        {
            var request = GuestVerificationEmailRequest.Example();

            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>
                {
                    new User
                    {
                        Id = Guid.NewGuid(),
                        FirstName = request.FirstName,
                        Email = request.Email,
                        EmailVerificationId = Guid.NewGuid(),
                        VerificationEmailSentAt = DateTime.UtcNow
                    }
                });

            await Assert.ThrowsAsync<EmailRecentlySentException>(() => _controller.SendGuestVerificationEmailAsync(request));
        }

        [Fact]
        public async Task SendGuestVerificationEmailThrowsNotFoundExceptionIfUserNotFound()
        {
            var request = GuestVerificationEmailRequest.Example();

            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.SendGuestVerificationEmailAsync(request));
        }

        [Fact]
        public async Task SendGuestVerificationEmailUpdatesUserIfEmailIsSent()
        {
            var request = GuestVerificationEmailRequest.Example();

            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName,
                Email = request.Email,
                EmailVerificationId = Guid.NewGuid()
            };

            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>
                {
                    user
                });

            var previousTime = DateTime.UtcNow;

            await _controller.SendGuestVerificationEmailAsync(request);

            _userRepositoryMock.Verify(x => x.UpdateItemAsync(It.Is<Guid>(y => y == user.Id), It.Is<User>(z => z.VerificationEmailSentAt > previousTime)));
        }

        #endregion

        #region VerifyEmail

        [Fact]
        public async Task VerifyEmailThrowsNotFoundExceptionIfUserIsNotFound()
        {
            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.VerifyEmailAsync(VerifyUserEmailRequest.Example()));
        }

        [Fact]
        public async Task VerifyEmailReturnsTrueVerifyUserEmailResponseIfIsEmailVerifiedIsNull()
        {
            var request = VerifyUserEmailRequest.Example();

            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>
                {
                    new User
                    {
                        Email = request.Email,
                        EmailVerificationId = request.VerificationId,
                        Id = new Guid(),
                        IsEmailVerified = null
                    }
                });

            var response = await _controller.VerifyEmailAsync(request);
            Assert.True(response.Result);
        }

        [Fact]
        public async Task VerifyEmailReturnsTrueVerifyUserEmailResponseIfRequestEmailVerificationIdMatches()
        {
            var request = VerifyUserEmailRequest.Example();
            request.VerificationId = new Guid();

            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>
                {
                    new User
                    {
                        Email = request.Email,
                        EmailVerificationId = request.VerificationId,
                        Id = new Guid(),
                        IsEmailVerified = false
                    }
                });

            var response = await _controller.VerifyEmailAsync(request);
            Assert.True(response.Result);
        }

        [Fact]
        public async Task VerifyEmailUpdatesIsEmailVerifiedAndEmailVerifiedAtProperties()
        {
            var request = VerifyUserEmailRequest.Example();
            request.VerificationId = new Guid();

            var user = new User
            {
                Email = request.Email,
                EmailVerificationId = request.VerificationId,
                Id = new Guid(),
                IsEmailVerified = false
            };

            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>
                {
                    user
                });

            await _controller.VerifyEmailAsync(request);

            _userRepositoryMock.Verify(x => x.UpdateItemAsync(
                It.Is<Guid>(y => y == user.Id),
                It.Is<User>(z => z.IsEmailVerified == true && z.EmailVerifiedAt != null)));
        }

        [Fact]
        public async Task VerifyEmailReturnsFalseVerifyUserEmailResponseIfRequestEmailVerificationIdDoesNotMatch()
        {
            var request = VerifyUserEmailRequest.Example();
            request.VerificationId = new Guid();

            _userRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>
                {
                    new User
                    {
                        Email = request.Email,
                        EmailVerificationId = Guid.NewGuid(),
                        Id = new Guid(),
                        IsEmailVerified = false
                    }
                });

            var response = await _controller.VerifyEmailAsync(request);
            Assert.False(response.Result);
        }


        #endregion
    }
}
