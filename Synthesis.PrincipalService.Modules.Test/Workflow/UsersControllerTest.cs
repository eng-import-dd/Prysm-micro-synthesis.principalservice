using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.PrincipalService.Validators;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Workflow.Controllers;
using Synthesis.Nancy.MicroService;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using AutoMapper;
using Synthesis.License.Manager.Interfaces;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Requests;
using System.Linq.Expressions;
using Synthesis.License.Manager.Models;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Utility;

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

        public UsersControllerTest()
        {
            var mapper = new MapperConfiguration(cfg => {
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

            _controller = new UsersController(_repositoryFactoryMock.Object,
                                              _validatorLocatorMock.Object,
                                              _eventServiceMock.Object,
                                              _loggerMock.Object,
                                              _licenseApiMock.Object,
                                              _emailUtilityMock.Object,
                                              mapper);
        }

        /// <summary>
        /// Gets the user by identifier asynchronous returns user if exists.
        /// </summary>
        /// <returns>Task object.</returns>
        [Fact]
        public async Task GetUserByIdAsyncReturnsUserIfExists()
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
        public async Task GetUserByIdAsyncThrowsNotFoundExceptionIfUserDoesNotExist()
        {
            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(default(User));

            var userId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetUserAsync(userId));
        }


        [Fact]
        public async Task CreatUserAsyncThrowsValidationExceptionIfUserNameOrEmailIsDuplicate()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                           .ReturnsAsync(new List<User> { new User() });

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserAsync( createUserRequest, tenantId, createdBy));

            Assert.Equal(ex.Errors.ToList().Count, 2); //Duplidate Email & Duplicate username errors
        }

        [Fact]
        public async Task CreatUserAsyncThrowsValidationExceptionIfUserNameOrEmailOrLdapIsDuplicate()
        {
            _userRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<User, bool>>>()))
                           .ReturnsAsync(new List<User> { new User() });

            var createUserRequest = new CreateUserRequest { FirstName = "first", LastName = "last", LdapId = "ldap" };
            var tenantId = Guid.NewGuid();
            var createdBy = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateUserAsync(createUserRequest, tenantId, createdBy));

            Assert.Equal(ex.Errors.ToList().Count, 3);//Duplidate Email, Duplicate Ldap & Duplicate username errors
        }

        [Fact]
        public async Task CreatUserAsyncSuccess()
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

            Assert.NotNull(user);
            Assert.Equal(user.TenantId, tenantId);
            Assert.Equal(user.CreatedBy, createdBy);
            Assert.Equal(user.IsLocked , false);
        }

        [Fact]
        public async Task CreatUserAsyncUserIsLockedIfNoLicenseAvailable()
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
            _emailUtilityMock.Verify(m=>m.SendUserLockedMail(It.IsAny<List<User>>(), It.IsAny<string>(), It.IsAny<string>()));

            Assert.NotNull(user);
            Assert.Equal(user.TenantId, tenantId);
            Assert.Equal(user.CreatedBy, createdBy);
            Assert.Equal(user.IsLocked, true);
        }

        [Fact]
        public async Task CreatUserAsyncUserIsLockedIfLicenseApiThrowsException()
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
        public async Task GetUsersBasicAsyncReturnsUsersIfExists()
        {
            //Mock<IRepository<UserBasicResponse>> _repositoryMock1 = new Mock<IRepository<UserBasicResponse>>();
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
                                //IEnumerable<User> items = userList;
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
            //Mock<IRepository<UserBasicResponse>> _repositoryMock1 = new Mock<IRepository<UserBasicResponse>>();
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
                                        //IEnumerable<User> items = userList;
                                        return (Task.FromResult(items.AsEnumerable()));

                                    });
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var getUsersParams = new GetUsersParams();

            var result = await _controller.GetUsersForAccount(getUsersParams, tenantId, userId);
            Assert.Equal(count, result.TotalCount);
        }
    }
}

