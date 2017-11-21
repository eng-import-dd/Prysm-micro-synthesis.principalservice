using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Nancy;
using Nancy.Testing;
using Synthesis.Authentication;
using Synthesis.DocumentStorage;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.PolicyEvaluator;
using Synthesis.PolicyEvaluator.Models;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Xunit;
using ClaimTypes = Synthesis.Nancy.MicroService.Constants.ClaimTypes;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    public class UserInviteModuleTests
    {
        private Browser AuthenticatedBrowser => GetBrowser();
        private Browser UnauthenticatedBrowser => GetBrowser(false);
        // TODO: Uncomment the browsers below when ForbiddenBrowser tests are added
        //private Browser ForbiddenBrowser => GetBrowser(true, false);

        private readonly Mock<IUserInvitesController> _controllerMock = new Mock<IUserInvitesController>();
        private readonly Mock<IRepository<UserInvite>> _userInviteRepositoryMock = new Mock<IRepository<UserInvite>>();
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorForbiddenMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<ITokenValidator> _tokenValidatorMock = new Mock<ITokenValidator>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();

        public UserInviteModuleTests()
        {
            _loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _policyEvaluatorForbiddenMock
                .Setup(x => x.EvaluateAsync(It.IsAny<PolicyEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PermissionScope.Deny);

            _metadataRegistryMock
                .Setup(x => x.GetRouteMetadata(It.IsAny<string>()))
                .Returns<string>(name => new SynthesisRouteMetadata(null, null, name));

            _repositoryFactoryMock
                .Setup(f => f.CreateRepository<UserInvite>())
                .Returns(_userInviteRepositoryMock.Object);
        }

        private Browser GetBrowser(bool isAuthenticated = true, bool hasAccess = true)
        {
            return new Browser(with =>
            {
                if (isAuthenticated)
                {
                    with.RequestStartup((container, pipelines, context) =>
                    {
                        var identity = new ClaimsIdentity(new[]
                            {
                                new Claim(ClaimTypes.Account, "Test User"),
                                new Claim(ClaimTypes.Email, "test@user.com"),
                                new Claim("TenantId" , "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3")
                            },
                            AuthenticationTypes.Basic);
                        context.CurrentUser = new ClaimsPrincipal(identity);
                    });
                }

                with.Dependency(_controllerMock.Object);
                with.Dependency(_userInviteRepositoryMock.Object);
                with.Dependency(_repositoryFactoryMock.Object);
                with.Dependency(_tokenValidatorMock.Object);
                with.Dependency(_loggerFactoryMock.Object);
                with.Dependency(_metadataRegistryMock.Object);
                with.Dependency(hasAccess ? _policyEvaluatorMock.Object : _policyEvaluatorForbiddenMock.Object);
                with.Module<UserInviteModule>();
            });
        }

        private static void BuildRequest(BrowserContext context)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
        }

        private static void BuildRequest<T>(BrowserContext context, T body)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
            context.JsonBody(body);
        }

        [Fact]
        public async void CreateUserInviteReturnCreated()
        {
            _controllerMock.Setup(m => m.CreateUserInviteListAsync(It.IsAny<List<UserInviteRequest>>(), It.IsAny<Guid>()))
                .ReturnsAsync(new List<UserInviteResponse>());

            var response = await AuthenticatedBrowser.Post("/v1/userinvites", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async void CreateUserInviteReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.CreateUserInviteListAsync(It.IsAny<List<UserInviteRequest>>(), It.IsAny<Guid>()))
                .ThrowsAsync(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/userinvites", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async void GetInvitedUsersForTenantReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetUsersInvitedForTenantAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception());

            var response = await AuthenticatedBrowser.Get("/v1/userinvites", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetInvitedUsersForTenantReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.GetUsersInvitedForTenantAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .Throws(new Exception());

            var response = await AuthenticatedBrowser.Get("/v1/userinvites", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async void GetInvitedUsersForTenantReturnsOk()
        {
            _controllerMock.Setup(m => m.GetUsersInvitedForTenantAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(new PagingMetadata<UserInviteResponse>()));

            var response = await AuthenticatedBrowser.Get("/v1/userinvites", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async void ResendUserInviteReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.ResendEmailInviteAsync(It.IsAny<List<UserInviteRequest>>(), It.IsAny<Guid>()))
                .ThrowsAsync(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/userinvites/resend", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async void ResendUserInviteReturnsRespondWithUnauthorizedNoBearer()
        {
            var response = await UnauthenticatedBrowser.Post("/v1/userinvites/resend", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async void ResendUserInviteReturnsSuccess()
        {
            var response = await AuthenticatedBrowser.Post("/v1/userinvites/resend", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async void RespondWithUnauthorizedNoBearer()
        {
            var response = await UnauthenticatedBrowser.Post("/v1/userinvites", ctx => BuildRequest(ctx, new UserInviteRequest()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}