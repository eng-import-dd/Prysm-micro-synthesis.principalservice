using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Http.Microservice;
using Synthesis.License.Manager.Interfaces;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Exceptions;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Utilities;
using Synthesis.PrincipalService.Validators;
using Synthesis.TenantService.InternalApi.Api;
using Synthesis.TenantService.InternalApi.Models;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Controllers
{
    public class UsersControllerTests
    {
        public UsersControllerTests()
        {
            _mapper = new MapperConfiguration(cfg => { cfg.AddProfile<UserProfile>(); }).CreateMapper();

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

            // validator mock
            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            // logger factory mock
            _loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(_loggerMock.Object);

            const string deploymentType = "";
            _controller = new UsersController(_repositoryFactoryMock.Object,
                _validatorLocatorMock.Object,
                _eventServiceMock.Object,
                _loggerFactoryMock.Object,
                _licenseApiMock.Object,
                _emailApiMock.Object,
                _mapper,
                deploymentType, 
                _tenantDomainApiMock.Object, 
                _tenantApiMock.Object);
        }

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
        private readonly Mock<ITenantDomainApi> _tenantDomainApiMock = new Mock<ITenantDomainApi>();
        
        private readonly UsersController _controller;
        private readonly IMapper _mapper;
        private readonly Mock<IUsersController> _userApiMock = new Mock<IUsersController>();
        private readonly Mock<IUsersController> _mockUserController = new Mock<IUsersController>();
        private readonly Mock<IValidator> _validatorFailsMock = new Mock<IValidator>();

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
            await Assert.ThrowsAsync<Exception>(() => _controller.AutoProvisionRefreshGroupsAsync(idpUserRequest, tenantId, createdBy));
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

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.AutoProvisionRefreshGroupsAsync(idpUserRequest, tenantId, createdBy));
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsFailsAndThrowsPromotionFailedException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "user@nodomain.com" });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            _userApiMock.Setup(u => u.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<bool>()))
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
            await Assert.ThrowsAsync<PromotionFailedException>(() => _controller.AutoProvisionRefreshGroupsAsync(idpUserRequest, tenantId, createdBy));
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
            var userResponse = await _controller.AutoProvisionRefreshGroupsAsync(idpUserRequest, tenantId, createdBy);
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
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(() =>
                {
                    var userList = new List<User> { new User { Email = email, Id = Guid.NewGuid()} };

                    var items = userList;
                    return Task.FromResult(items.AsEnumerable());
                });

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }.AsEnumerable()));

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "asd.com" }));

            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
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

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }.AsEnumerable()));

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "prysm.com" }));

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
                .Returns(Task.FromResult(new User()));

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId));
            Assert.Single(ex.Errors.ToList());
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsNoUserFoundValidationException()
        {
            _mockUserController.Setup(m => m.CreateUserGroupAsync(new UserGroup(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult<User>(null));

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserGroupAsync(new UserGroup(), It.IsAny<Guid>(), It.IsAny<Guid>()));
            Assert.Single(ex.Errors.ToList());
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsUserIfSuccessful()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()))
                .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User
                {
                    //TenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3"),
                    Groups = new List<Guid> { Guid.NewGuid() }
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
            _mockUserController.Setup(m => m.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId))
                .Returns(Task.FromResult(new User()));

            var result = await _controller.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId);
            Assert.IsType<User>(result);
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsValidationException()
        {
            _mockUserController.Setup(m => m.CreateUserGroupAsync(new UserGroup(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User()));

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

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });

            var createUserRequest = new User { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var user = await _controller.CreateUserAsync(createUserRequest, tenantId, createdBy);

            _userRepositoryMock.Verify(m => m.CreateItemAsync(It.IsAny<User>()));
            _emailApiMock.Verify(m => m.SendWelcomeEmail(It.IsAny<UserEmailRequest>()));
            _eventServiceMock.Verify(m => m.PublishAsync(It.Is<ServiceBusEvent<User>>(e => e.Name == "UserCreated")));

            Assert.NotNull(user);
            Assert.Equal(user.CreatedBy, createdBy);
            Assert.False(user.IsLocked);
        }

        [Fact]
        public async Task CreatUserAsyncThrowsValidationExceptionIfUserNameOrEmailIsDuplicateAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { new User() });

            var createUserRequest = new User { FirstName = "first", LastName = "last" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(2, ex.Errors.ToList().Count); //Duplidate Email & Duplicate username errors
        }

        [Fact]
        public async Task CreatUserAsyncThrowsValidationExceptionIfUserNameOrEmailOrLdapIsDuplicateAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { new User() });

            var createUserRequest = new User { FirstName = "first", LastName = "last", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(3, ex.Errors.ToList().Count); //Duplidate Email, Duplicate Ldap & Duplicate username errors
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

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>())).Throws<Exception>();

            var createUserRequest = new User { FirstName = "first", LastName = "last", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var user = await _controller.CreateUserAsync(createUserRequest, tenantId, createdBy);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));

            Assert.True(user.IsLocked);
        }

        [Fact]
        public async Task CreatUserAsyncUserIsLockedIfNoLicenseAvailableAsync()
        {
            var tenantId = Guid.NewGuid();
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

            var createUserRequest = new User { FirstName = "first", LastName = "last", LdapId = "ldap" };
            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));
            var user = await _controller.CreateUserAsync(createUserRequest, tenantId, createdBy);

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
            _tenantDomainApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }.AsEnumerable()));
            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "test.com" }));
            var tenantId = Guid.NewGuid();
            var getGuestUserParams = new GetUsersParams();

            var result = await _controller.GetGuestUsersForTenantAsync(tenantId, getGuestUserParams);
            Assert.Empty(result.List);
            Assert.Equal(0, result.CurrentCount);
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
            var getGuestUserParams = new GetUsersParams();
            _tenantDomainApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }.AsEnumerable()));
            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "test.com" }));
            var result = await _controller.GetGuestUsersForTenantAsync(tenantId, getGuestUserParams);

            Assert.Equal(3, result.List.Count);
            Assert.Equal(3, result.CurrentCount);
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

            var usernames = await _controller.GetNamesForUsers(new List<Guid>());

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
            _userRepositoryMock.Setup(m => m.GetOrderedPaginatedItemsAsync(It.IsAny<OrderedQueryParameters<User, string>>()))
                .ReturnsAsync(new PaginatedResponse<User> { ContinuationToken = "test", Items = new List<User> { new User(), new User(), new User() } });
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var getUsersParams = new GetUsersParams();

            var result = await _controller.GetUsersBasicAsync(tenantId, userId, getUsersParams);
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
            _userRepositoryMock.Setup(m => m.GetOrderedPaginatedItemsAsync(It.IsAny<OrderedQueryParameters<User, string>>()))
                .ReturnsAsync(new PaginatedResponse<User> { ContinuationToken = "test", Items = new List<User> { new User(), new User(), new User() } });

            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var getUsersParams = new GetUsersParams();
            _tenantApiMock.Setup(m => m.GetUserIdsByTenantIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { userId }.AsEnumerable()));
            var result = await _controller.GetUsersForTenantAsync(getUsersParams, tenantId, userId);
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
                _mapper,
                deploymentType,
                _tenantDomainApiMock.Object,
                _tenantApiMock.Object);

            var createUserRequest = new User { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap" };
            var tenantId = Guid.Parse("2D907264-8797-4666-A8BB-72FE98733385");
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

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
                _mapper,
                deploymentType,
                _tenantDomainApiMock.Object,
                _tenantApiMock.Object);

            var createUserRequest = new User { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap" };
            var tenantId = Guid.Parse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3");
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Single(ex.Errors.ToList());
        }

        [Fact]
        public async Task LockUserAsyncIfAssigningLicenseFails()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());
            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>())).ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Failed });
            var userId = Guid.NewGuid();
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.LockOrUnlockUserAsync(userId, false));
        }

        [Fact]
        public async Task LockUserAsyncIfReleaseLicenseFails()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());
            _licenseApiMock.Setup(m => m.ReleaseUserLicenseAsync(It.IsAny<UserLicenseDto>())).ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Failed });
            var userId = Guid.NewGuid();
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.LockOrUnlockUserAsync(userId, true));
        }

        [Fact]
        public async Task LockUserAsyncIfUserExist()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());
            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });
            var userId = Guid.NewGuid();
            var result = await _controller.LockOrUnlockUserAsync(userId, false);
            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));
            Assert.True(result);
        }

        [Fact]
        public async Task LockUserAsyncIfUserNotFound()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });
            var userId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetUserAsync(userId));
            var result = await _controller.LockOrUnlockUserAsync(userId, false);
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
            await Assert.ThrowsAsync<PromotionFailedException>(() => _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense, true));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFaileIfNoLicenceAvailableAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com" });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }.AsEnumerable()));

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "test.com" }));

            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));
            _tenantApiMock.Setup(m => m.AddUserToTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK));


            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            await Assert.ThrowsAsync<LicenseAssignmentFailedException>(() => _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Exactly(1));
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFailIfIfEmailIsNotWhitelistedAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@testtest.com"});

            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "test.com" }));

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }.AsEnumerable()));

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            await Assert.ThrowsAsync < PromotionFailedException >(()=> _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFailIfIfUserEmailIsEmptyAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid() });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            await Assert.ThrowsAsync<PromotionFailedException>(() => _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFailIfIfUserIsAlreadyInTenantAsync()
        {
            var tenantId = Guid.NewGuid();
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com" });

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { tenantId }.AsEnumerable()));

            var userid = Guid.NewGuid();
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense);

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
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com" });

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }.AsEnumerable()));

            _tenantDomainApiMock.Setup(m => m.GetTenantDomainByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain{Domain = "test.com"}));
            _tenantApiMock.Setup(m => m.AddUserToTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, true));
            _tenantApiMock.Setup(m => m.GetTenantIdsForUserIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>().AsEnumerable()));

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense);

            _emailApiMock.Verify(m => m.SendWelcomeEmail(It.IsAny<UserEmailRequest>()));

            Assert.Equal(CanPromoteUserResultCode.UserCanBePromoted, promoteResponse);
        }

        [Fact]
        public async Task RemoveUserFromPermissionGroupSuccess()
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
                .Returns(Task.FromResult(new User()));

            var result = await _controller.RemoveUserFromPermissionGroupAsync(userId, groupId, userId);
            Assert.True(result);
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
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new DocumentNotFoundException());
            _userRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()))
                .Returns(Task.FromResult(new User()));

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
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.UpdateUserAsync(userId, user));
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

            var result = await _controller.UpdateUserAsync(userId, user);
            Assert.IsType<User>(result);
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
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.UpdateUserAsync(userId, user));
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
            _validatorLocatorMock.Setup(m => m.GetValidator(typeof(GuestCreationRequestValidator)))
                .Returns(_validatorFailsMock.Object);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateGuestAsync(User.Example(), Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task CreateGuestBadTenantIdThrowsValidationfailed()
        {
            _validatorLocatorMock.Setup(m => m.GetValidator(typeof(TenantIdValidator)))
                .Returns(_validatorFailsMock.Object);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateGuestAsync(User.Example(), Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task CreateGuestBadCreatedByIdThrowsValidationfailed()
        {
            _validatorLocatorMock.Setup(m => m.GetValidator(typeof(UserIdValidator)))
                .Returns(_validatorFailsMock.Object);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateGuestAsync(User.Example(), Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task CreateGuestForExistingUserThrowsUserExists()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { User.Example() });

            await Assert.ThrowsAsync<UserExistsException>(() => _controller.CreateGuestAsync(User.Example(), Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task CreateGuestForUninvitedUserThrowsUserNotInvited()
        {
            _userInviteRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<UserInvite, bool>>>()))
                .ReturnsAsync(new List<UserInvite> { UserInvite.Example() });

            await Assert.ThrowsAsync<UserNotInvitedException>(() => _controller.CreateGuestAsync(User.Example(), Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task CreateGuestCreatesUser()
        {
            await _controller.CreateGuestAsync(User.Example(), Guid.NewGuid(), Guid.NewGuid());

            _userRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<User>()));
        }

        [Fact]
        public async Task CreateGuestSendsEvent()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync(User.Example());

            await _controller.CreateGuestAsync(User.Example(), Guid.NewGuid(), Guid.NewGuid());

            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<User>>()));
        }

        [Fact]
        public async Task CreateGuestReturnsUser()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .ReturnsAsync(User.Example());

            var result = await _controller.CreateGuestAsync(User.Example(), Guid.NewGuid(), Guid.NewGuid());
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateGuestTrimsWhiteSpaceInName()
        {
            const string firstName = "  FirstName  ";
            const string lastName = "  LastName  ";

            var request = User.Example();
            request.FirstName = firstName;
            request.LastName = lastName;

            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                .Returns<User>(u =>
                {
                    return Task.FromResult(u);
                });

            var result = await _controller.CreateGuestAsync(request, Guid.NewGuid(), Guid.NewGuid());

            Assert.Equal(firstName.Trim(), result.FirstName);
            Assert.Equal(lastName.Trim(), result.LastName);
        }
    }
}
