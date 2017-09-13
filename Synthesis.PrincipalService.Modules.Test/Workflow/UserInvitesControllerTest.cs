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
using Synthesis.PrincipalService.Entity;
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

            // event service mock
            _eventServiceMock.Setup(m => m.PublishAsync(It.IsAny<ServiceBusEvent<UserInvite>>()));

            _controller = new UserInvitesController(_repositoryFactoryMock.Object,
                                              _eventServiceMock.Object,
                                              _loggerMock.Object,
                                              _emailUtilityMock.Object,
                                              mapper);
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
            createUserInviteRequest.Add(new UserInviteRequest { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" });;
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            _repositoryMock.Verify(m => m.CreateItemAsync(It.IsAny<UserInvite>()));
            _emailUtilityMock.Verify(m => m.SendUserInvite(It.IsAny<List<UserInviteEntity>>()));

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
        public async Task CreateUserInviteListDuplicateInvite()
        {
            _repositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<UserInvite, bool>>>()))
                           .ReturnsAsync(new List<UserInvite> {new UserInvite { Id = Guid.NewGuid(), FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" }});

            _repositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(default(UserInvite));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(default(User));

            var createUserInviteRequest = new List<UserInviteRequest>();
            createUserInviteRequest.Add(new UserInviteRequest { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" }); ;
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);


            Assert.NotNull(userInvite);
            Assert.Equal(userInvite.ElementAt(0).Status, InviteUserStatus.DuplicateUserEmail);
        }
    }
}
