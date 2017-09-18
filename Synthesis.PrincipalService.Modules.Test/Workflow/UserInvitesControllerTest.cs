using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Workflow.Controllers;
using System;
using System.Threading.Tasks;
using Xunit;
using AutoMapper;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Requests;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentValidation;
using FluentValidation.Results;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Enums;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Utilities;

namespace Synthesis.PrincipalService.Modules.Test.Workflow
{
    public class UserInvitesControllerTest
    {
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IRepository<UserInvite>> _repositoryMock = new Mock<IRepository<UserInvite>>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();
        private readonly Mock<IEmailUtility> _emailUtilityMock = new Mock<IEmailUtility>();
        private readonly IUserInvitesController _controller;
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();

        public UserInvitesControllerTest()
        {
            var mapper = new MapperConfiguration(cfg => {
                                                     cfg.AddProfile<UserInviteProfile>();
                                                 }).CreateMapper();
            // repository mock
            _repositoryFactoryMock.Setup(m => m.CreateRepository<UserInvite>())
                                  .Returns(_repositoryMock.Object);
            _repositoryFactoryMock.Setup(m => m.CreateRepository<User>())
                                  .Returns(_userRepositoryMock.Object);

            _validatorMock.Setup(m => m.ValidateAsync(It.IsAny<object>(), CancellationToken.None))
                          .ReturnsAsync(new ValidationResult());

            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                                 .Returns(_validatorMock.Object);

            // event service mock
            _eventServiceMock.Setup(m => m.PublishAsync(It.IsAny<ServiceBusEvent<UserInvite>>()));

            _controller = new UserInvitesController(_repositoryFactoryMock.Object,
                                              _loggerMock.Object,
                                              _emailUtilityMock.Object,
                                              mapper,
                                              _validatorLocatorMock.Object);
        }

        [Fact]
        public async Task CreateUserInviteListSuccess()
        {
            _repositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<UserInvite>()))
                .ReturnsAsync(new UserInvite { Id = Guid.NewGuid(), FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" });

            _repositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(default(UserInvite));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(User));

            var createUserInviteRequest = new List<UserInviteRequest>();
            createUserInviteRequest.Add(new UserInviteRequest { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" });
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            _repositoryMock.Verify(m => m.CreateItemAsync(It.IsAny<UserInvite>()));
            _emailUtilityMock.Verify(m => m.SendUserInvite(It.IsAny<List<UserInviteResponse>>()));

            Assert.NotNull(userInvite);
            Assert.Equal(userInvite.ElementAt(0).Status, InviteUserStatus.Success);
        }

        [Fact]
        public async Task CreateUserInviteListEmailDomainNotAllowed()
        {
            var createUserInviteRequest = new List<UserInviteRequest>();
            createUserInviteRequest.Add(new UserInviteRequest { FirstName = "abc", LastName = "xyz", Email = "abc@test.com" });
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            Assert.NotNull(userInvite);
            Assert.Equal(userInvite.ElementAt(0).Status, InviteUserStatus.UserEmailNotDomainAllowed);
        }

        [Fact]
        public async Task CreateUserInviteListFreeEmailDomain()
        {
            var createUserInviteRequest = new List<UserInviteRequest>();
            createUserInviteRequest.Add(new UserInviteRequest { FirstName = "abc", LastName = "xyz", Email = "abc@gmail.com" });
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            Assert.NotNull(userInvite);
            Assert.Equal(userInvite.ElementAt(0).Status, InviteUserStatus.UserEmailDomainFree);
        }

        [Fact]
        public async Task CreateUserInviteListInvalidEmailFormate()
        {
            var createUserInviteRequest = new List<UserInviteRequest>();
            createUserInviteRequest.Add(new UserInviteRequest { FirstName = "abc", LastName = "xyz", Email = "abc.com" });
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            Assert.NotNull(userInvite);
            Assert.Equal(userInvite.ElementAt(0).Status, InviteUserStatus.UserEmailFormatInvalid);
        }

        [Fact]
        public async Task CreateUserInviteListDuplicateInvite()
        {
            _repositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<UserInvite, bool>>>()))
                           .ReturnsAsync(new List<UserInvite> {new UserInvite { Id = Guid.NewGuid(), FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" }});

            _repositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(default(UserInvite));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(default(User));

            var createUserInviteRequest = new List<UserInviteRequest>();
            createUserInviteRequest.Add(new UserInviteRequest { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" });
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);


            Assert.NotNull(userInvite);
            Assert.Equal(userInvite.ElementAt(0).Status, InviteUserStatus.DuplicateUserEmail);
        }

        [Fact]
        public async Task CreateUserInviteListDuplicateUserEntry()
        {
            _repositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<UserInvite>()))
                           .ReturnsAsync(new UserInvite { Id = Guid.NewGuid(), FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" });

            _repositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(default(UserInvite));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(default(User));

            var createUserInviteRequest = new List<UserInviteRequest>();
            createUserInviteRequest.Add(new UserInviteRequest { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" });
            createUserInviteRequest.Add(new UserInviteRequest { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" });
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);


            Assert.NotNull(userInvite);
            Assert.Equal(userInvite.ElementAt(1).Status, InviteUserStatus.DuplicateUserEntry);
        }

        [Fact]
        public async Task GetInvitedUsersForTenantIfUsersExists()
        {
            const int count = 5;
            _repositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<UserInvite, bool>>>()))
                                      .Returns(() =>
                                               {
                                                   var itemsList = new List<UserInvite>();
                                                   for (var i = 0; i < count; i++)
                                                   {
                                                       itemsList.Add(new UserInvite());
                                                   }

                                                   IEnumerable<UserInvite> items = itemsList;
                                                   return (Task.FromResult(items));
                                               });

            var tenantId = Guid.NewGuid();
            var result = await _controller.GetUsersInvitedForTenantAsync(tenantId, true);

            Assert.Equal(count, result.List.Count);


        }
    }
}
