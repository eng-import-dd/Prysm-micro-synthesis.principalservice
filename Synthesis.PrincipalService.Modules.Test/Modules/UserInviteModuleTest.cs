using System;
using System.Collections.Generic;
using System.Security.Claims;
using AutoMapper;
using Moq;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Testing;
using Nancy.TinyIoc;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Utility;
using Synthesis.PrincipalService.Workflow.Controllers;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    public class UserInviteModuleTest
    {
        private readonly Browser _browserAuth;
        private readonly Browser _browserNoAuth;

        private readonly Mock<IUserInvitesController> _controllerMock = new Mock<IUserInvitesController>();

        public UserInviteModuleTest()
        {
            _browserAuth = BrowserWithRequestStartup((container, pipelines, context) =>
            {
                context.CurrentUser = new ClaimsPrincipal(
                    new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "TestUser"),
                        new Claim(ClaimTypes.Email, "test@user.com"),
                        new Claim("TenantId" , "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3")
                    },
                    AuthenticationTypes.Basic));
            });
            _browserNoAuth = BrowserWithRequestStartup((container, pipelines, context) =>
            {
            });
        }

        private Browser BrowserWithRequestStartup(Action<TinyIoCContainer, IPipelines, NancyContext> requestStartup)
        {
            return new Browser(with =>
            {
                var mockLogger = new Mock<ILogger>();

                mockLogger.Setup(l => l.LogMessage(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Callback(() => Console.Write(""));
                var logger = mockLogger.Object;
                var loggerFactoryMock = new Mock<ILoggerFactory>();
                loggerFactoryMock.Setup(f => f.Get(It.IsAny<LogTopic>())).Returns(logger);

                var loggerFactory = loggerFactoryMock.Object;
                var resource = new UserInvite
                {
                    Id = Guid.Parse("2c1156fa-5902-4978-9c3d-ebcb77ae0d55"),
                    FirstName = "abc",
                    LastName = "xyz",
                    Email = "abc@yopmail.com"
                };
                var repositoryMock = new Mock<IRepository<UserInvite>>();
                repositoryMock
                    .Setup(r => r.GetItemAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(resource);

                var repositoryFactoryMock = new Mock<IRepositoryFactory>();
                repositoryFactoryMock
                    .Setup(f => f.CreateRepository<UserInvite>())
                    .Returns(repositoryMock.Object);

                var eventServiceMock = new Mock<IEventService>();
                eventServiceMock.Setup(s => s.PublishAsync(It.IsAny<string>()));

                var mapper = new MapperConfiguration(cfg => {
                    cfg.AddProfile<UserInviteProfile>();
                }).CreateMapper();

                var mockEmailUtility = new Mock<IEmailUtility>();

                with.EnableAutoRegistration();
                with.RequestStartup(requestStartup);
                with.Dependency(new Mock<IMetadataRegistry>().Object);
                with.Dependency(loggerFactory);
                with.Dependency(logger);
                with.Dependency(repositoryFactoryMock.Object);
                with.Dependency(eventServiceMock.Object);
                with.Dependency(_controllerMock.Object);
                with.Dependency(mockEmailUtility.Object);
                with.Dependency(mapper);
                with.Module<UserInviteModule>();
            });
        }

        [Fact]
        public async void RespondWithUnauthorizedNoBearer()
        {
            var actual = await _browserNoAuth.Post(
                                                 "/v1/userinvites",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new UserInviteRequest());
                                                 });
            Assert.Equal(HttpStatusCode.Unauthorized, actual.StatusCode);
        }

        [Fact]
        public async void CreateUserInviteReturnCreated()
        {
            var actual = await _browserAuth.Post(
                                                  "/v1/userinvites",
                                                  with =>
                                                  {
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                      with.HttpRequest();
                                                      with.JsonBody(new UserInviteRequest());
                                                  });
            Assert.Equal(HttpStatusCode.Created, actual.StatusCode);
        }
    }
}
