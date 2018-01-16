﻿using System;
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
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Utilities;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Controllers
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

            // event service mock
            _eventServiceMock.Setup(m => m.PublishAsync(It.IsAny<ServiceBusEvent<User>>()));

            _validatorMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult());

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
                _emailUtilityMock.Object,
                _passwordUtilityMock.Object,
                _mapper,
                deploymentType,
                _tenantApiMock.Object);
        }

        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();
        private readonly Mock<IRepository<Group>> _groupRepositoryMock = new Mock<IRepository<Group>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<ILicenseApi> _licenseApiMock = new Mock<ILicenseApi>();
        private readonly Mock<IEmailUtility> _emailUtilityMock = new Mock<IEmailUtility>();
        private readonly Mock<ITenantApi> _tenantApiMock = new Mock<ITenantApi>();
        private readonly IUsersController _controller;
        private readonly IMapper _mapper;
        private readonly Mock<IUsersController> _userApiMock = new Mock<IUsersController>();
        private readonly Mock<IUsersController> _mockUserController = new Mock<IUsersController>();
        private readonly Mock<IPasswordUtility> _passwordUtilityMock = new Mock<IPasswordUtility>();

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
        public async Task CanPromoteUserIfEmailIsEmpty()
        {
            var email = "";
            await Assert.ThrowsAsync<ValidationException>(() => _controller.CanPromoteUserAsync(email));
        }

        [Fact]
        public async Task CanPromoteUserIfUserExistsInAnAccount()
        {
            var email = "ch@asd.com";
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(() =>
                {
                    var userList = new List<User> { new User { Email = email, TenantId = Guid.NewGuid()} };

                    var items = userList;
                    return Task.FromResult(items.AsEnumerable());
                });

            _tenantApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }));

            _tenantApiMock.Setup(m => m.GetTenantDomainAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "asd.com" }));

            var result = await _controller.CanPromoteUserAsync(email);
            var response = CanPromoteUserResultCode.UserAccountAlreadyExists;
            Assert.Equal(response, result.ResultCode);
        }

        [Fact]
        public async Task CanPromoteUserNotFoundException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));
            var email = "ch@gmm.com";
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.CanPromoteUserAsync(email));
        }

        [Fact]
        public async Task CanPromoteUserSuccess()
        {
            var email = "ch@prysm.com";
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(() =>
                {
                    var userList = new List<User> { new User { Email = email } };

                    var items = userList;
                    return Task.FromResult(items.AsEnumerable());
                });

            _tenantApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }));

            _tenantApiMock.Setup(m => m.GetTenantDomainAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "prysm.com" }));

            var result = await _controller.CanPromoteUserAsync(email);
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
                    TenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3"),
                    Groups = new List<Guid> { Guid.Parse("12bf0424-bd5e-4af0-affb-d48485ae7115") }
                }));

            var newUserGroupRequest = new CreateUserGroupRequest
            {
                UserId = Guid.Parse("79d68d52-838a-40e2-a83d-c509ba550a30"),
                GroupId = Guid.Parse("12bf0424-bd5e-4af0-affb-d48485ae7115")
            };

            var userId = Guid.Parse("79d68d52-838a-40e2-a83d-c509ba550a30");
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");

            _mockUserController.Setup(m => m.CreateUserGroupAsync(newUserGroupRequest, tenantId, It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User()));

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId));
            Assert.Equal(ex.Errors.ToList().Count, 1);
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsNoUserFoundValidationException()
        {
            _mockUserController.Setup(m => m.CreateUserGroupAsync(new CreateUserGroupRequest(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult<User>(null));

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserGroupAsync(new CreateUserGroupRequest(), It.IsAny<Guid>(), It.IsAny<Guid>()));
            Assert.Equal(ex.Errors.ToList().Count, 1);
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
                    TenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3"),
                    Groups = new List<Guid> { Guid.NewGuid() }
                }));

            var newUserGroupRequest = new CreateUserGroupRequest
            {
                UserId = Guid.Parse("79d68d52-838a-40e2-a83d-c509ba550a30"),
                GroupId = Guid.Parse("12bf0424-bd5e-4af0-affb-d48485ae7115")
            };

            var userId = Guid.Parse("79d68d52-838a-40e2-a83d-c509ba550a30");
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");

            _mockUserController.Setup(m => m.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId))
                .Returns(Task.FromResult(new User()));

            var result = await _controller.CreateUserGroupAsync(newUserGroupRequest, tenantId, userId);
            Assert.IsType<User>(result);
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsValidationException()
        {
            _mockUserController.Setup(m => m.CreateUserGroupAsync(new CreateUserGroupRequest(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User()));

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserGroupAsync(new CreateUserGroupRequest(), It.IsAny<Guid>(), It.IsAny<Guid>()));
            Assert.Equal(ex.Errors.ToList().Count, 1);
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

            var createUserRequest = new UserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var user = await _controller.CreateUserAsync(createUserRequest, tenantId, createdBy);

            _userRepositoryMock.Verify(m => m.CreateItemAsync(It.IsAny<User>()));
            _emailUtilityMock.Verify(m => m.SendWelcomeEmailAsync("a@b.com", "first"));
            _eventServiceMock.Verify(m => m.PublishAsync("UserCreated", It.IsAny<User>()));

            Assert.NotNull(user);
            Assert.Equal(user.TenantId, tenantId);
            Assert.Equal(user.CreatedBy, createdBy);
            Assert.Equal(user.IsLocked, false);
        }

        [Fact]
        public async Task CreatUserAsyncThrowsValidationExceptionIfUserNameOrEmailIsDuplicateAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { new User() });

            var createUserRequest = new UserRequest { FirstName = "first", LastName = "last" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(ex.Errors.ToList().Count, 2); //Duplidate Email & Duplicate username errors
        }

        [Fact]
        public async Task CreatUserAsyncThrowsValidationExceptionIfUserNameOrEmailOrLdapIsDuplicateAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { new User() });

            var createUserRequest = new UserRequest { FirstName = "first", LastName = "last", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(ex.Errors.ToList().Count, 3); //Duplidate Email, Duplicate Ldap & Duplicate username errors
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

            var createUserRequest = new UserRequest { FirstName = "first", LastName = "last", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var user = await _controller.CreateUserAsync(createUserRequest, tenantId, createdBy);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));

            Assert.Equal(user.IsLocked, true);
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

            var createUserRequest = new UserRequest { FirstName = "first", LastName = "last", LdapId = "ldap" };

            var user = await _controller.CreateUserAsync(createUserRequest, tenantId, createdBy);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));
            _emailUtilityMock.Verify(m => m.SendUserLockedMailAsync(It.IsAny<List<User>>(), It.IsAny<string>(), It.IsAny<string>()));

            Assert.NotNull(user);
            Assert.Equal(user.TenantId, tenantId);
            Assert.Equal(user.CreatedBy, createdBy);
            Assert.Equal(user.IsLocked, true);
        }

        [Fact]
        public async Task GetGuestUserForTenantReturnsEmptyResult()
        {
            _userRepositoryMock.Setup(m => m.GetOrderedPaginatedItemsAsync(It.IsAny<OrderedQueryParameters<User, string>>()))
                .ReturnsAsync(new PaginatedResponse<User> { ContinuationToken = "", Items = new List<User>() });
            _tenantApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }));

            _tenantApiMock.Setup(m => m.GetTenantDomainAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "test.com" }));
            var tenantId = Guid.NewGuid();
            var getGuestUserParams = new GetUsersParams();

            var result = await _controller.GetGuestUsersForTenantAsync(tenantId, getGuestUserParams);
            Assert.Empty(result.List);
            Assert.Equal(0, result.CurrentCount);
            Assert.Equal(true, result.IsLastChunk);
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
            _tenantApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }));

            _tenantApiMock.Setup(m => m.GetTenantDomainAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "test.com" }));
            var result = await _controller.GetGuestUsersForTenantAsync(tenantId, getGuestUserParams);

            Assert.Equal(3, result.List.Count);
            Assert.Equal(3, result.CurrentCount);
            Assert.Equal("test", result.ContinuationToken);
            Assert.Equal(false, result.IsLastChunk);
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

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User
                {
                    Id = userId,
                    TenantId = tenantId
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

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetLicenseTypeForUserAsync(userId, tenantId));
        }

        [Trait("Get Tenant Id by User Email", "Get Tenant Id by User Email")]
        [Fact]
        public async Task GetTenantIdByUserEmailAsyncSuccess()
        {
            var validEmail = "user@prysm.com";
            var userId = Guid.Parse("814CF57E-157B-4493-8007-691B5E316006");

            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>
                {
                    new User
                    {
                        Id = userId,
                        Email = validEmail
                    }
                });

            var result = await _controller.GetTenantIdByUserEmailAsync(validEmail);

            Assert.IsType<Guid>(result);
        }

        [Trait("Get Tenant Id by User Email", "Get Tenant Id by User Email")]
        [Fact]
        public async Task GetTenantIdByUserEmailAsyncThrowsNotFoundException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var validEmail = "user@prysm.com";

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetTenantIdByUserEmailAsync(validEmail));
        }

        [Trait("Get Tenant Id by User Email", "Get Tenant Id by User Email")]
        [Fact]
        public async Task GetTenantIdByUserEmailAsynctReturnsNoMatchingRecords()
        {
            const int count = 3;
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .Returns(() =>
                {
                    var itemsList = new List<User>();
                    for (var i = 0; i < count; i++)
                    {
                        itemsList.Add(new User());
                    }

                    IEnumerable<User> items = itemsList;
                    return Task.FromResult(items);
                });

            var validEmail = "user@prysm.com";
            var result = await _controller.GetTenantIdByUserEmailAsync(validEmail);

            Assert.Equal(Guid.Empty, result);
        }

        [Trait("Send Reset Password Email", "Send Reset Password Email")]
        [Fact]
        public async Task SendResetPasswordEmailSuccess()
        {
            var request = new PasswordResetEmailRequest
            {
                Email = "a@b.com",
                FirstName = "test",
                Link = "http://test.com"
            };

            _emailUtilityMock.Setup(m => m.SendResetPasswordEmailAsync(request.Email, request.FirstName, request.Link))
                .ReturnsAsync(true);

            var result = await _controller.SendResetPasswordEmail(request);

            _emailUtilityMock.Verify(m => m.SendResetPasswordEmailAsync(request.Email, request.FirstName, request.Link), Times.Once);
            Assert.Equal(result, true);
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

            Assert.IsType<UserResponse>(result);
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

            Assert.IsType<UserResponse>(result);
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
                .Returns(Task.FromResult((IEnumerable<User>)new List<User>()));

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
        public async Task GetUsersForAccountIfExists()
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

            var result = await _controller.GetUsersForAccountAsync(getUsersParams, tenantId, userId);
            Assert.Equal(count, result.List.Count);
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task GetUsersForGroupReturnsUsersIfExists()
        {
            var validGroupId = Guid.NewGuid();

            _mockUserController.Setup(m => m.GetGroupUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new List<Guid>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                .Returns(Task.FromResult(Enumerable.Empty<User>()));

            var result = await _controller.GetGroupUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>());

            Assert.IsType<List<Guid>>(result);
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task GetUsersForGroupThrowsNotFoundExceptionIfGroupDoesNotExist()
        {
            var validGroupId = Guid.NewGuid();

            _mockUserController.Setup(m => m.GetGroupUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                .Throws(new NotFoundException(string.Empty));

            var result = await _controller.GetGroupUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>());

            Assert.Equal(0, result.Count);
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
                _emailUtilityMock.Object,
                _passwordUtilityMock.Object,
                _mapper,
                deploymentType,
                _tenantApiMock.Object);

            var createUserRequest = new UserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap" };
            var tenantId = Guid.Parse("2D907264-8797-4666-A8BB-72FE98733385");
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(ex.Errors.ToList().Count, 1);
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
                _emailUtilityMock.Object,
                _passwordUtilityMock.Object,
                _mapper,
                deploymentType,
                _tenantApiMock.Object);

            var createUserRequest = new UserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap" };
            var tenantId = Guid.Parse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3");
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(ex.Errors.ToList().Count, 1);
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
            Assert.Equal(result, true);
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
            Assert.Equal(result, false);
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

            _tenantApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid> { Guid.NewGuid() }));

            _tenantApiMock.Setup(m => m.GetTenantDomainAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain { Domain = "test.com" }));

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            await Assert.ThrowsAsync<LicenseAssignmentFailedException>(() => _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Exactly(2));
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFailIfIfEmailIsNotWhitelistedAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@testtest.com", TenantId = Guid.NewGuid() });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
            Assert.Equal(promoteResponse.ResultCode, PromoteGuestResultCode.UserAlreadyPromoted);
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFailIfIfUserEmailIsEmptyAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), TenantId = Guid.NewGuid() });

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
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com", TenantId = Guid.NewGuid() });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<LicenseSummaryDto>());

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
            Assert.Equal(promoteResponse.ResultCode, PromoteGuestResultCode.UserAlreadyPromoted);
        }

        [Fact]
        public async Task GetGroupsForUserSuccess()
        {
            Guid? userId = Guid.NewGuid();
            _mockUserController.Setup(m => m.GetGroupsForUserAsync(It.IsAny<Guid>()))
                               .Returns(Task.FromResult(new List<Guid>()));
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = userId, Groups = new List<Guid>() });
            var result = await _controller.GetGroupsForUserAsync((Guid)userId);
            Assert.IsType<List<Guid>>(result);
        }

        [Fact]
        public async Task GetGroupsForUserThrowsNotFoundException()
        {
            Guid? userId = Guid.NewGuid();
            const string exception = "Resource Not Found";
            _mockUserController.Setup(m => m.GetGroupsForUserAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new NotFoundException(exception));
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new NotFoundException(exception));
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetGroupsForUserAsync((Guid)userId));
        }

        [Fact]
        public async Task GetGroupsForUserThrowsException()
        {
            Guid? userId = Guid.NewGuid();
            _mockUserController.Setup(m => m.GetGroupsForUserAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new Exception());
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new Exception());
            await Assert.ThrowsAsync<Exception>(() => _controller.GetGroupsForUserAsync((Guid)userId));
        }

        [Fact]
        public async Task GetGroupsForUserThrowsValidationException()
        {
            Guid? userId = Guid.NewGuid();
            _mockUserController.Setup(m => m.GetGroupsForUserAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.GetGroupsForUserAsync((Guid)userId));
        }

        [Fact]
        public async Task PromoteGuestSuccssTestAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com" });

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse { ResultCode = LicenseResponseResultCode.Success });

            _tenantApiMock.Setup(m => m.GetTenantDomainIdsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new List<Guid>{Guid.NewGuid()}));

            _tenantApiMock.Setup(m => m.GetTenantDomainAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new TenantDomain{Domain = "test.com"}));

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Once);
            _emailUtilityMock.Verify(m => m.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()));

            Assert.Equal(promoteResponse.ResultCode, PromoteGuestResultCode.Success);
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
            Assert.Equal(true, result);
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
        public async Task ResendWelcomeEmailFailed()
        {
            _emailUtilityMock.Setup(m => m.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception());
            await Assert.ThrowsAsync<Exception>(() => _controller.ResendUserWelcomeEmailAsync("ch@gg.com", "charan"));
        }

        [Fact]
        public async Task ResendWelcomeEmailIfEmailIsEmpty()
        {
            _emailUtilityMock.Setup(m => m.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>())).Throws(new ValidationException(new List<ValidationFailure>()));
            await Assert.ThrowsAsync<ValidationException>(() => _controller.ResendUserWelcomeEmailAsync("", "charan"));
        }

        [Fact]
        public async Task ResendWelcomeEmailSuccess()
        {
            _emailUtilityMock.Setup(m => m.SendWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            var result = await _controller.ResendUserWelcomeEmailAsync("ch@gmm.com", "charan");
            Assert.Equal(true, result);
        }

        [Fact]
        public async Task UpdateUserNotFoundException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));
            var userId = Guid.NewGuid();
            var user = new UpdateUserRequest();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.UpdateUserAsync(userId, user));
        }

        [Fact]
        public async Task UpdateUserSuccess()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());

            var userId = Guid.NewGuid();
            var user = new UpdateUserRequest
            {
                FirstName = "FirstName",
                LastName = "LastName",
                Email = "cmalyala@prysm.com",
                IsLocked = false,
                IsIdpUser = false
            };

            var result = await _controller.UpdateUserAsync(userId, user);
            Assert.IsType<UserResponse>(result);
        }

        [Fact]
        public async Task UpdateUserValidationException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new User());
            var userId = Guid.NewGuid();
            var user = new UpdateUserRequest();
            _validatorMock.Setup(m => m.Validate(userId))
                .Returns(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("", "") }));
            _validatorMock.Setup(m => m.Validate(user))
                .Returns(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("", ""), new ValidationFailure("", "") }));
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.UpdateUserAsync(userId, user));
            Assert.Equal(ex.Errors.ToList().Count, 3);
        }

        [Fact]
        public async Task GetUserByEmailOrUserNameIfExistsAsync()
        {
            var validEmail = "smm@pry.com";
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User,bool>>>()))
                               .ReturnsAsync(new List<User>(){new User(){Email = validEmail}});

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
    }
}