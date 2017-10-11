using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.License.Manager.Interfaces;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Utilities;
using Synthesis.PrincipalService.Workflow.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Xunit;
using Synthesis.PrincipalService.Workflow.Exceptions;

namespace Synthesis.PrincipalService.Modules.Test.Workflow
{
    public class UsersControllerTest
    {
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();
        private readonly Mock<IRepository<Group>> _groupRepositoryMock = new Mock<IRepository<Group>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<ILicenseApi> _licenseApiMock = new Mock<ILicenseApi>();
        private readonly Mock<IEmailUtility> _emailUtilityMock = new Mock<IEmailUtility>();
        private readonly IUsersController _controller;
        private readonly IMapper _mapper;
        private readonly Mock<IUsersController> _userApiMock = new Mock<IUsersController>();
        private readonly Mock<IUsersController> _mockUserController = new Mock<IUsersController>();

        public UsersControllerTest()
        {
            _mapper = new MapperConfiguration(cfg =>
                                              {
                                                         cfg.AddProfile<UserProfile>();
                                                     }).CreateMapper();

            // repository mock
            _repositoryFactoryMock.Setup(m => m.CreateRepository<User>())
                                  .Returns(_userRepositoryMock.Object);

            _repositoryFactoryMock.Setup(m => m.CreateRepository<Group>())
                                  .Returns(_groupRepositoryMock.Object);

            // event service mock
            _eventServiceMock.Setup(m => m.PublishAsync(It.IsAny<ServiceBusEvent<User>>()));


            _validatorMock.Setup(m => m.ValidateAsync(It.IsAny<object>(), CancellationToken.None))
                          .ReturnsAsync(new ValidationResult());

            // validator mock
            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                                 .Returns(_validatorMock.Object);

            string deploymentType = "";
            _controller = new UsersController(_repositoryFactoryMock.Object,
                                              _validatorLocatorMock.Object,
                                              _eventServiceMock.Object,
                                              _loggerMock.Object,
                                              _licenseApiMock.Object,
                                              _emailUtilityMock.Object,
                                              _mapper,
                                              deploymentType);
        }

        #region Get User By Id Test Cases
        
        /// <summary>
        /// Gets the user by identifier asynchronous returns user if exists.
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
        /// Gets the user by identifier asynchronous throws not found exception if user does not exist.
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

        #endregion

        #region Create User Test Cases
        
        [Fact]
        public async Task CreatUserAsyncThrowsValidationExceptionIfUserNameOrEmailIsDuplicateAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                           .ReturnsAsync(new List<User> { new User() });

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last" };
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

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(ex.Errors.ToList().Count, 3); //Duplidate Email, Duplicate Ldap & Duplicate username errors
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
                .ReturnsAsync(new LicenseResponse() { ResultCode = LicenseResponseResultCode.Success });

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var user = await _controller.CreateUserAsync(createUserRequest, tenantId, createdBy);

            _userRepositoryMock.Verify(m => m.CreateItemAsync(It.IsAny<User>()));
            _emailUtilityMock.Verify(m => m.SendWelcomeEmail("a@b.com", "first"));
            _eventServiceMock.Verify(m=>m.PublishAsync("UserCreated", It.IsAny<User>()));

            Assert.NotNull(user);
            Assert.Equal(user.TenantId, tenantId);
            Assert.Equal(user.CreatedBy, createdBy);
            Assert.Equal(user.IsLocked, false);
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
                               .ReturnsAsync(new List<User> { new User() { FirstName = "admin", Email = "admin@test.com" } }.AsEnumerable());

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse() { ResultCode = LicenseResponseResultCode.Failed });

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", LdapId = "ldap" };

            var user = await _controller.CreateUserAsync(createUserRequest, tenantId, createdBy);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));
            _emailUtilityMock.Verify(m => m.SendUserLockedMail(It.IsAny<List<User>>(), It.IsAny<string>(), It.IsAny<string>()));

            Assert.NotNull(user);
            Assert.Equal(user.TenantId, tenantId);
            Assert.Equal(user.CreatedBy, createdBy);
            Assert.Equal(user.IsLocked, true);
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

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var user = await _controller.CreateUserAsync(createUserRequest, tenantId, createdBy);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));

            Assert.Equal(user.IsLocked, true);
        }

       [Fact]
        public async Task InOnPremDeploymentUserCreationOnPrysmAccountShouldFailAsync()
        {
            string deploymentType = "OnPrem";
            var controller = new UsersController(_repositoryFactoryMock.Object,
                                                 _validatorLocatorMock.Object,
                                                 _eventServiceMock.Object,
                                                 _loggerMock.Object,
                                                 _licenseApiMock.Object,
                                                 _emailUtilityMock.Object,
                                                 _mapper,
                                                 deploymentType);

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap" };
            var tenantId = Guid.Parse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3");
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(ex.Errors.ToList().Count, 1);
        }

        [Fact]
        public async Task InOnPremDeploymentUserCreationOnLocalAccountShouldFailAsync()
        {
            string deploymentType = "OnPrem";
            var controller = new UsersController(_repositoryFactoryMock.Object,
                                                 _validatorLocatorMock.Object,
                                                 _eventServiceMock.Object,
                                                 _loggerMock.Object,
                                                 _licenseApiMock.Object,
                                                 _emailUtilityMock.Object,
                                                 _mapper,
                                                 deploymentType);

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", Email = "a@b.com", LdapId = "ldap" };
            var tenantId = Guid.Parse("2D907264-8797-4666-A8BB-72FE98733385");
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(ex.Errors.ToList().Count, 1);
        }

        #endregion

        #region Lock User Test Cases
        [Fact]
        public async Task LockUserAsyncIfUserExist()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new User());
            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                           .ReturnsAsync(new LicenseResponse() { ResultCode = LicenseResponseResultCode.Success });
            var userId = Guid.NewGuid();
            var isLocked = false;
            var result = await _controller.LockOrUnlockUserAsync(userId, isLocked);
            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()));
            Assert.Equal(result, true);

        }
        [Fact]
        public async Task LockUserAsyncIfAssigningLicenseFails()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new User());
            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>())).Throws<Exception>();
            var userId = Guid.NewGuid();
            var isLocked = false;
            var result = await _controller.LockOrUnlockUserAsync(userId, isLocked);
            Assert.Equal(result, false);

        }
        [Fact]
        public async Task LockUserAsyncIfReleaseLicenseFails()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new User());
            _licenseApiMock.Setup(m => m.ReleaseUserLicenseAsync(It.IsAny<UserLicenseDto>())).Throws<Exception>();
            var userId = Guid.NewGuid();
            var isLocked = true;
            var result = await _controller.LockOrUnlockUserAsync(userId, isLocked);
            Assert.Equal(result, false);

        }
        [Fact]
        public async Task LockUserAsyncIfUserNotFound()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(default(User));


            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                           .ReturnsAsync(new LicenseResponse() { ResultCode = LicenseResponseResultCode.Success });
            var userId = Guid.NewGuid();
            var isLocked = false;
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetUserAsync(userId));
            var result = await _controller.LockOrUnlockUserAsync(userId, isLocked);
            Assert.Equal(result, false);

        } 
        #endregion

        #region PromoteGuest Tests

        [Fact]
        public async Task PromoteGuestSuccssTestAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new User{ Id = Guid.NewGuid(), Email = "a@test.com"});

            _licenseApiMock.Setup(m => m.AssignUserLicenseAsync(It.IsAny<UserLicenseDto>()))
                .ReturnsAsync(new LicenseResponse() { ResultCode = LicenseResponseResultCode.Success });

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, tenantId,LicenseType.UserLicense, false);

            _userRepositoryMock.Verify(m=>m.UpdateItemAsync(It.IsAny<Guid>(),It.IsAny<User>()), Times.Once);
            _emailUtilityMock.Verify(m => m.SendWelcomeEmail(It.IsAny<string>(), It.IsAny<string>()));


            Assert.Equal(promoteResponse.ResultCode, PromoteGuestResultCode.Success);
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

            _userRepositoryMock.Verify(m=>m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
            
            
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFaileIfNoLicenceAvailableAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com" });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new List<LicenseSummaryDto>());

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            await Assert.ThrowsAsync<LicenseAssignmentFailedException>(() =>  _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense, false));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Exactly(2));
        }

        [Fact]
        public async Task PromoteGuestManuallyShouldFailIfIfUserIsAlreadyInTenantAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com", TenantId = Guid.NewGuid()});

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new List<LicenseSummaryDto>());

            var tenantId = Guid.NewGuid();
            var userid = Guid.NewGuid();
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense, false);

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
            await Assert.ThrowsAsync<PromotionFailedException>(() => _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense, false));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
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
            var promoteResponse = await _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense, false);

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
            Assert.Equal(promoteResponse.ResultCode, PromoteGuestResultCode.UserAlreadyPromoted);
        }

        [Fact]
        public async Task PromoteGuestShoulldThrowValidationErrorIfUserIdEmptyAsync()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = "a@test.com", TenantId = Guid.NewGuid() });

            _licenseApiMock.Setup(m => m.GetTenantLicenseSummaryAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new List<LicenseSummaryDto>());

            
            var tenantId = Guid.NewGuid();
            var userid = Guid.Empty;

            _validatorMock.Setup(m => m.ValidateAsync(userid, CancellationToken.None))
                          .ReturnsAsync(new ValidationResult( new List<ValidationFailure>(){new ValidationFailure("","" )} ));

            await Assert.ThrowsAsync<ValidationFailedException>( () => _controller.PromoteGuestUserAsync(userid, tenantId, LicenseType.UserLicense, false));

            _userRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
        }
        #endregion

        #region Get User Test Cases

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
                                List<User> items = userList;
                                return (Task.FromResult(items.AsEnumerable()));
                                
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

                                        List<User> items = userList;
                                        return (Task.FromResult(items.AsEnumerable()));

                                    });
            _userRepositoryMock.Setup(m => m.GetOrderedPaginatedItemsAsync(It.IsAny<OrderedQueryParameters<User, string>>()))
                               .ReturnsAsync(new PaginatedResponse<User> { ContinuationToken = "test", Items = new List<User> { new User(), new User(), new User() } });

            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var getUsersParams = new GetUsersParams();

            var result = await _controller.GetUsersForAccountAsync(getUsersParams, tenantId, userId);
            Assert.Equal(count, result.List.Count);
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

        #endregion

        #region Auto Provision Refresh Group Test Cases

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
                           .ReturnsAsync(new LicenseResponse() { ResultCode = LicenseResponseResultCode.Success });

            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var idpUserRequest = new IdpUserRequest
            {
                FirstName = "TestUser",
                LastName = "TestUser"
            };
            var userResponse = await _controller.AutoProvisionRefreshGroups(idpUserRequest, tenantId, createdBy);
            Assert.NotNull(userResponse);

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
                           .ReturnsAsync(new LicenseResponse() { ResultCode = LicenseResponseResultCode.Success });

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

            _validatorMock.Setup(m => m.ValidateAsync(tenantId, CancellationToken.None))
                          .ReturnsAsync(new ValidationResult(new List<ValidationFailure>() { new ValidationFailure("", "") }));

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.AutoProvisionRefreshGroups(idpUserRequest, tenantId, createdBy));
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
            await Assert.ThrowsAsync<PromotionFailedException>(() => _controller.AutoProvisionRefreshGroups(idpUserRequest, tenantId, createdBy));
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
            await Assert.ThrowsAsync<Exception>(() => _controller.AutoProvisionRefreshGroups(idpUserRequest, tenantId, createdBy));
        }

        #endregion

        #region Update User Test Cases
        
        [Fact]
        public async Task UpdateUserSuccess()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new User());

            var userId = Guid.NewGuid();
            var user = new UpdateUserRequest()
            {
               FirstName = "FirstName",
               LastName = "LastName",
               Email = "cmalyala@prysm.com",
               PasswordAttempts = 3,
               IsLocked = false,
               IsIdpUser = false
            };

            var result = await _controller.UpdateUserAsync(userId,user);
            Assert.IsType<UserResponse>(result);
        }

        [Fact]
        public async Task UpdateUserValidationException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new User());
            var userId = Guid.NewGuid();
            var user = new UpdateUserRequest();
            _validatorMock.Setup(m => m.ValidateAsync(userId, CancellationToken.None))
                          .ReturnsAsync(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("", "") }));
            _validatorMock.Setup(m => m.ValidateAsync(user, CancellationToken.None))
                          .ReturnsAsync(new ValidationResult(new List<ValidationFailure> { new ValidationFailure("", ""), new ValidationFailure("", "") }));
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.UpdateUserAsync(userId, user));
            Assert.Equal(ex.Errors.ToList().Count,3);
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
        #endregion

        #region Can Promote User Test Cases
        [Fact]
        public async Task CanPromoteUserSuccess()
        {

            var email = "ch@prysm.com";
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                               .Returns(() =>
                                        {
                                            var userList = new List<User>();

                                            userList.Add(new User() { Email = email });
                                            var items = userList;
                                            return (Task.FromResult(items.AsEnumerable()));

                                        });
            var result = await _controller.CanPromoteUserAsync(email);
            var response = CanPromoteUserResultCode.UserCanBePromoted;
            Assert.Equal(response, result.ResultCode);
        }

        [Fact]
        public async Task CanPromoteUserIfUserExistsInAnAccount()
        {

            var email = "ch@asd.com";
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                               .Returns(() =>
                                        {
                                            var userList = new List<User>();

                                            userList.Add(new User() { Email = email });
                                            var items = userList;
                                            return (Task.FromResult(items.AsEnumerable()));

                                        });
            var result = await _controller.CanPromoteUserAsync(email);
            var response = CanPromoteUserResultCode.UserAccountAlreadyExists;
            Assert.Equal(response, result.ResultCode);
        }

        [Fact]
        public async Task CanPromoteUserIfEmailIsEmpty()
        {
            var email = "";
            await Assert.ThrowsAsync<ValidationException>(() => _controller.CanPromoteUserAsync(email));
        }
        [Fact]
        public async Task CanPromoteUserNotFoundException()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(default(User));
            var email = "ch@gmm.com";
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.CanPromoteUserAsync(email));
        }
        #endregion

        #region User Groups Test Cases

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task CreateUserGroupAsyncReturnsUserIfSuccessful()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                               .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()))
                               .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .Returns(Task.FromResult(new User()
                               {
                                   TenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3"),
                                   Groups = new List<Guid>() { Guid.NewGuid() }
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
        public async Task CreateUserGroupAsyncReturnsDuplicateUserGroupValidationException()
        {
            _userRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<User>()))
                               .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()))
                               .Returns(Task.FromResult(new User()));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .Returns(Task.FromResult(new User()
                               {
                                   TenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3"),
                                   Groups = new List<Guid>() { Guid.Parse("12bf0424-bd5e-4af0-affb-d48485ae7115") }
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
        public async Task GetUsersForGroupReturnsUsersIfExists()
        {
            var validGroupId = Guid.NewGuid();

            _mockUserController.Setup(m => m.GetUsersForGroup(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<UserGroup>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));

            var result = await _controller.GetUsersForGroup(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>());

            Assert.IsType<List<UserGroup>>(result);
        }

        [Trait("User Group", "User Group Tests")]
        [Fact]
        public async Task GetUsersForGroupThrowsNotFoundExceptionIfGroupDoesNotExist()
        {
            var validGroupId = Guid.NewGuid();

            _mockUserController.Setup(m => m.GetUsersForGroup(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                               .Throws(new NotFoundException(string.Empty));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Throws(new NotFoundException(string.Empty));

            var result = await _controller.GetUsersForGroup(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>());

            Assert.Equal(0, result.Count);
        }

        #endregion

        #region GetGuestUsers Tests
        [Fact]
        public async Task GetGuestUsersForTenantSuccess()
        {
            _userRepositoryMock.Setup(m => m.GetOrderedPaginatedItemsAsync(It.IsAny<OrderedQueryParameters<User, string>>()))
                               .ReturnsAsync(new PaginatedResponse<User> { ContinuationToken = "test", Items = new List<User> { new User(), new User(), new User() } });
            var tenantId = Guid.NewGuid();
            var getGuestUserParams = new GetUsersParams();

            var result = await _controller.GetGuestUsersForTenantAsync(tenantId, getGuestUserParams);

            Assert.Equal(3, result.List.Count);
            Assert.Equal(3, result.CurrentCount);
            Assert.Equal("test", result.ContinuationToken);
            Assert.Equal(false, result.IsLastChunk);
        }

        
        [Fact]
        public async Task GetGuestUserForTenantReturnsEmptyResult()
        {
            _userRepositoryMock.Setup(m => m.GetOrderedPaginatedItemsAsync(It.IsAny<OrderedQueryParameters<User, string>>()))
                               .ReturnsAsync(new PaginatedResponse<User>{ContinuationToken = "", Items = new List<User>()});

            var tenantId = Guid.NewGuid();
            var getGuestUserParams = new GetUsersParams();

            var result = await _controller.GetGuestUsersForTenantAsync(tenantId, getGuestUserParams);
            Assert.Empty(result.List);
            Assert.Equal(0, result.CurrentCount);
            Assert.Equal(true, result.IsLastChunk);
            Assert.Null(result.SearchValue);
            Assert.Null(result.SortColumn);
        }
        #endregion
    }
}