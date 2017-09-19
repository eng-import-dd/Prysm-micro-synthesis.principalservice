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
using Xunit;

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
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var getUsersParams = new GetUsersParams();

            var result = await _controller.GetUsersBasicAsync(tenantId, userId, getUsersParams);
            Assert.Equal(count, result.TotalCount);
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
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var getUsersParams = new GetUsersParams();

            var result = await _controller.GetUsersForAccountAsync(getUsersParams, tenantId, userId);
            Assert.Equal(count, result.TotalCount);
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
    }
}

