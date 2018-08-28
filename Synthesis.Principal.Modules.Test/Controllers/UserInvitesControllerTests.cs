using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
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
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Validators;
using Synthesis.TenantService.InternalApi.Api;
using Synthesis.TenantService.InternalApi.Models;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Controllers
{
    public class UserInvitesControllerTests
    {
        public UserInvitesControllerTests()
        {
            var mapper = new MapperConfiguration(cfg => { cfg.AddProfile<UserInviteProfile>(); }).CreateMapper();

            // repository mock
            _repositoryFactoryMock.Setup(m => m.CreateRepository<UserInvite>())
                .Returns(_repositoryMock.Object);
            _repositoryFactoryMock.Setup(m => m.CreateRepository<User>())
                .Returns(_userRepositoryMock.Object);

            _validatorMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult());

            _validatorMock.Setup(m => m.ValidateAsync(It.IsAny<object>(), CancellationToken.None))
                .ReturnsAsync(new ValidationResult());

            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            _validatorFailsMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult { Errors = { new ValidationFailure(string.Empty, string.Empty) } });

            // event service mock
            _eventServiceMock.Setup(m => m.PublishAsync(It.IsAny<ServiceBusEvent<UserInvite>>()));

            // logger factory mock
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(_loggerMock.Object);

            _controller = new UserInvitesController(_repositoryFactoryMock.Object,
                loggerFactoryMock.Object,
                mapper,
                _validatorLocatorMock.Object,
                _tenantApiMock.Object,
                _emailApiMock.Object);
        }

        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IRepository<UserInvite>> _repositoryMock = new Mock<IRepository<UserInvite>>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();
        private readonly IUserInvitesController _controller;
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<ITenantApi> _tenantApiMock = new Mock<ITenantApi>();
        private readonly Mock<IEmailApi> _emailApiMock = new Mock<IEmailApi>();
        private readonly Mock<IValidator> _validatorFailsMock = new Mock<IValidator>();

        [Fact]
        public async Task CreateUserInviteListDuplicateInvite()
        {
            _repositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<UserInvite, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UserInvite> { new UserInvite { Id = Guid.NewGuid(), FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" } });

            _repositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(UserInvite));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(User));

            var createUserInviteRequest = new List<UserInvite> { new UserInvite { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" } };
            var tenantId = Guid.NewGuid();

            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "yopmail.com" } }));

            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            Assert.NotNull(userInvite);
            Assert.Equal(InviteUserStatus.DuplicateUserEmail, userInvite.ElementAt(0).Status);
        }

        [Fact]
        public async Task CreateUserInviteListDuplicateUserEntry()
        {
            _repositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<UserInvite>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserInvite { Id = Guid.NewGuid(), FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" });

            _repositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(UserInvite));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(User));

            var createUserInviteRequest = new List<UserInvite>
            {
                new UserInvite { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" },
                new UserInvite { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" }
            };
            var tenantId = Guid.NewGuid();
            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "yopmail.com" } }));

            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            Assert.NotNull(userInvite);
            Assert.Equal(InviteUserStatus.DuplicateUserEntry, userInvite.ElementAt(1).Status);
        }

        [Fact]
        public async Task CreateUserInviteListEmailDomainNotAllowed()
        {
            var createUserInviteRequest = new List<UserInvite> { new UserInvite { FirstName = "abc", LastName = "xyz", Email = "abc@test.com" } };
            var tenantId = Guid.NewGuid();
            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "yopmail.com" } }));

            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            Assert.NotNull(userInvite);
            Assert.Equal(InviteUserStatus.UserEmailNotDomainAllowed, userInvite.ElementAt(0).Status);
        }

        [Fact]
        public async Task CreateUserInviteListFreeEmailDomain()
        {
            var createUserInviteRequest = new List<UserInvite> { new UserInvite { FirstName = "abc", LastName = "xyz", Email = "abc@gmail.com" } };
            var tenantId = Guid.NewGuid();
            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "gmail.com" } }));

            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            Assert.NotNull(userInvite);
            Assert.Equal(InviteUserStatus.Success, userInvite.ElementAt(0).Status);
        }

        [Fact]
        public async Task CreateUserInviteListInvalidEmailFormate()
        {
            _validatorLocatorMock.Setup(m => m.GetValidator(typeof(EmailValidator)))
                .Returns(_validatorFailsMock.Object);

            var createUserInviteRequest = new List<UserInvite> { new UserInvite { FirstName = "abc", LastName = "xyz", Email = "abc.com" } };
            var tenantId = Guid.NewGuid();
            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "gmail.com" } }));

            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            Assert.NotNull(userInvite);
            Assert.Equal(InviteUserStatus.UserEmailFormatInvalid, userInvite.ElementAt(0).Status);
        }

        [Fact]
        public async Task CreateUserInviteListSuccess()
        {
            _repositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<UserInvite>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UserInvite { Id = Guid.NewGuid(), FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" });

            _repositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(UserInvite));

            _userRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(User));

            var createUserInviteRequest = new List<UserInvite> { new UserInvite { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" } };
            var tenantId = Guid.NewGuid();
            _tenantApiMock.Setup(m => m.GetTenantDomainsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<IEnumerable<TenantDomain>>(HttpStatusCode.OK, new List<TenantDomain> { new TenantDomain { Domain = "yopmail.com" } }));

            var userInvite = await _controller.CreateUserInviteListAsync(createUserInviteRequest, tenantId);

            _repositoryMock.Verify(m => m.CreateItemAsync(It.IsAny<UserInvite>(), It.IsAny<CancellationToken>()));
            _emailApiMock.Verify(m => m.SendUserInvite(It.IsAny<List<UserEmailRequest>>()));
            Assert.NotNull(userInvite);
            Assert.Equal(InviteUserStatus.Success, userInvite.ElementAt(0).Status);
        }

        [Fact]
        public async Task GetInvitedUsersForTenantIfUsersExists()
        {
            const int count = 5;
            _repositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<UserInvite, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    var itemsList = new List<UserInvite>();
                    for (var i = 0; i < count; i++)
                    {
                        itemsList.Add(new UserInvite());
                    }

                    IEnumerable<UserInvite> items = itemsList;
                    return Task.FromResult(items);
                });

            var tenantId = Guid.NewGuid();
            var result = await _controller.GetUsersInvitedForTenantAsync(tenantId, true);

            Assert.Equal(count, result.List.Count);
        }

        [Fact]
        public async Task ResendUserInviteListReturnsUserNotExists()
        {
            var resendUserInviteRequest = new List<UserInvite> { new UserInvite { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" } };
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.ResendEmailInviteAsync(resendUserInviteRequest, tenantId);

            Assert.NotNull(userInvite);
            Assert.Equal(InviteUserStatus.UserNotExist, userInvite.ElementAt(0).Status);
        }

        [Fact]
        public async Task ResendUserInviteListSuccess()
        {
            _repositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<UserInvite, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UserInvite> { new UserInvite { Id = Guid.NewGuid(), FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" } });
            var userEmailRequests = new List<UserEmailResponse>
            {
                new UserEmailResponse { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" }
            };
            _emailApiMock.Setup(m => m.SendUserInvite(It.IsAny<List<UserEmailRequest>>()))
                .ReturnsAsync(userEmailRequests);

            var resendUserInviteRequest = new List<UserInvite> { new UserInvite { FirstName = "abc", LastName = "xyz", Email = "abc@yopmail.com" } };
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.ResendEmailInviteAsync(resendUserInviteRequest, tenantId);

            _repositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<UserInvite>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()));
            _emailApiMock.Verify(m => m.SendUserInvite(It.IsAny<List<UserEmailRequest>>()));

            Assert.NotNull(userInvite);
            Assert.Equal(InviteUserStatus.Success, userInvite.ElementAt(0).Status);
        }

        [Fact]
        public async Task ResendUserInviteWithEmptyList()
        {
            var resendUserInviteRequest = new List<UserInvite>();
            var tenantId = Guid.NewGuid();
            var userInvite = await _controller.ResendEmailInviteAsync(resendUserInviteRequest, tenantId);

            Assert.Empty(userInvite);
        }
    }
}