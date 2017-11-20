using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Testing;
using Synthesis.Authentication;
using Synthesis.DocumentStorage;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Synthesis.PolicyEvaluator.Models;
using Synthesis.PrincipalService.Controllers.Interfaces;
using Synthesis.PrincipalService.Models;
using Xunit;
using ClaimTypes = Synthesis.Nancy.MicroService.Constants.ClaimTypes;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    /// <summary>
    ///     Groups Module Unit Test Cases class.
    /// </summary>
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public class GroupsModuleTest
    {
        private Browser AuthenticatedBrowser => GetBrowser();
        private Browser UnauthenticatedBrowser => GetBrowser(false);
        // TODO: Uncomment the browsers below when Unauthenticated and ForbiddenBrowser tests are added
        //private Browser ForbiddenBrowser => GetBrowser(true, false);

        private readonly Mock<IGroupsController> _controllerMock = new Mock<IGroupsController>();
        private readonly Mock<IRepository<Group>> _groupRepositoryMock = new Mock<IRepository<Group>>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorForbiddenMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<ITokenValidator> _tokenValidatorMock = new Mock<ITokenValidator>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();

        public GroupsModuleTest()
        {
            _loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _policyEvaluatorForbiddenMock
                .Setup(x => x.EvaluateAsync(It.IsAny<PolicyEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PermissionScope.Deny);

            _metadataRegistryMock
                .Setup(x => x.GetRouteMetadata(It.IsAny<string>()))
                .Returns<string>(name => new SynthesisRouteMetadata(null, null, name));
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
                                new Claim(ClaimTypes.Email, "test@user.com")
                            },
                            AuthenticationTypes.Basic);
                        context.CurrentUser = new ClaimsPrincipal(identity);
                    });
                }

                with.Dependency(_controllerMock.Object);
                with.Dependency(_groupRepositoryMock.Object);
                with.Dependency(_tokenValidatorMock.Object);
                with.Dependency(_loggerFactoryMock.Object);
                with.Dependency(_metadataRegistryMock.Object);
                with.Dependency(hasAccess ? _policyEvaluatorMock.Object : _policyEvaluatorForbiddenMock.Object);
                with.Module<GroupsModule>();
                with.EnableAutoRegistration();
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
        public async Task CreateGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Post("/v1/groups", ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.CreateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/groups", ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGroupReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var response = await AuthenticatedBrowser.Post("/v1/groups", ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateGroupReturnsOk()
        {
            var response = await AuthenticatedBrowser.Post("/v1/groups", ctx => BuildRequest(ctx, new Group()));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task DeleteGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.DeleteGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var groupId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Delete($"/v1/groups/{groupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task DeleteGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.DeleteGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var groupId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Delete($"/v1/groups/{groupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task DeleteGroupReturnsNoContent()
        {
            var groupId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Delete($"/v1/groups/{groupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validGroupId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/groups/{validGroupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var validGroupId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/groups/{validGroupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var validGroupId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/groups/{validGroupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsOk()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new Group()));

            var validGroupId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/groups/{validGroupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new Group()));

            var validGroupId = Guid.NewGuid();

            var response = await UnauthenticatedBrowser.Get($"/v1/groups/{validGroupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsValidationFailedException()
        {
            var errors = Enumerable.Empty<ValidationFailure>();
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(errors));

            var validGroupId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/groups/{validGroupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Trait("GetGroupsForTenant", "Get Groups For Tenant Test Cases")]
        [Fact]
        public async Task GetGroupsForTenantReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetGroupsForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await AuthenticatedBrowser.Get("/v1/groups/tenant", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Trait("GetGroupsForTenant", "Get Groups For Tenant Test Cases")]
        [Fact]
        public async Task GetGroupsForTenantReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetGroupsForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await AuthenticatedBrowser.Get("/v1/groups/tenant", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Trait("GetGroupsForTenant", "Get Groups For Tenant Test Cases")]
        [Fact]
        public async Task GetGroupsForTenantReturnsOk()
        {
            _controllerMock.Setup(m => m.GetGroupsForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(Enumerable.Empty<Group>()));

            var response = await AuthenticatedBrowser.Get("/v1/groups/tenant", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.UpdateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Put("/v1/groups", ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.UpdateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await AuthenticatedBrowser.Put("/v1/groups", ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var response = await AuthenticatedBrowser.Put("/v1/groups", ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupReturnsOk()
        {
            var response = await AuthenticatedBrowser.Put("/v1/groups", ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}