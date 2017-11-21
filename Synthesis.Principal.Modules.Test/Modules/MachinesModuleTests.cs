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
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Xunit;
using ClaimTypes = Synthesis.Nancy.MicroService.Constants.ClaimTypes;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public class MachinesModuleTests
    {
        private Browser AuthenticatedBrowser => GetBrowser();
        private Browser UnauthenticatedBrowser => GetBrowser(false);
        // TODO: Uncomment the browsers below when ForbiddenBrowser tests are added
        //private Browser ForbiddenBrowser => GetBrowser(true, false);

        private readonly Mock<IMachineController> _controllerMock = new Mock<IMachineController>();
        private readonly Mock<IRepository<Machine>> _machineRepositoryMock = new Mock<IRepository<Machine>>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorForbiddenMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<ITokenValidator> _tokenValidatorMock = new Mock<ITokenValidator>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();

        public MachinesModuleTests()
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
                                new Claim(ClaimTypes.Email, "test@user.com"),
                                new Claim("TenantId", "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3"),
                                new Claim("UserId", "16367A84-65E7-423C-B2A5-5C42F8F1D5F2")
                            },
                            AuthenticationTypes.Basic);
                        context.CurrentUser = new ClaimsPrincipal(identity);
                    });
                }

                with.Dependency(_controllerMock.Object);
                with.Dependency(_machineRepositoryMock.Object);
                with.Dependency(_tokenValidatorMock.Object);
                with.Dependency(_loggerFactoryMock.Object);
                with.Dependency(_metadataRegistryMock.Object);
                with.Dependency(hasAccess ? _policyEvaluatorMock.Object : _policyEvaluatorForbiddenMock.Object);
                with.Module<MachinesModule>();
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
        public async Task ChangeMachineAccountReturnsBadRequest()
        {
            var response = await AuthenticatedBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount", ctx => BuildRequest(ctx, "bad request"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.ChangeMachineAccountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>())).Throws(new Exception());

            var response = await AuthenticatedBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount", ctx => BuildRequest(ctx,
                new Machine
                {
                    MachineKey = "12345678901234567890",
                    Location = "TestLocation",
                    ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                    SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                    DateModified = DateTime.UtcNow
                }));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsNotFound()
        {
            var response = await AuthenticatedBrowser.Put("/v1/machines/notavalidmachine/changeaccount", ctx => BuildRequest(ctx,
                new Machine
                {
                    MachineKey = "12345678901234567890",
                    Location = "TestLocation",
                    ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                    SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                    DateModified = DateTime.UtcNow
                }));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsOk()
        {
            var response = await AuthenticatedBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount", ctx => BuildRequest(ctx,
                new Machine
                {
                    MachineKey = "12345678901234567890",
                    Location = "TestLocation",
                    ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                    SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                    DateModified = DateTime.UtcNow
                }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount", ctx => BuildRequest(ctx,
                new Machine
                {
                    MachineKey = "12345678901234567890",
                    Location = "TestLocation",
                    ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                    SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                    DateModified = DateTime.UtcNow
                }));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateMachineRequestWithInvalidBodyReturnsBadRequest()
        {
            var response = await AuthenticatedBrowser.Post("/v1/machines", ctx => BuildRequest(ctx, "invalid machine"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateMachineReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateMachineAsync(It.IsAny<CreateMachineRequest>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Post("/v1/machines", ctx => BuildRequest(ctx, new Machine()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateMachineReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(x => x.CreateMachineAsync(It.IsAny<CreateMachineRequest>(), It.IsAny<Guid>())).Throws(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/machines", ctx => BuildRequest(ctx, new Machine()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateMachineReturnStatusOk()
        {
            var response = await AuthenticatedBrowser.Post("/v1/machines", ctx => BuildRequest(ctx, new Machine()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task DeleteMachineReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.DeleteMachineAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var machineId = new Guid();

            var response = await AuthenticatedBrowser.Delete($"/v1/machines/{machineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task DeleteMachineReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.DeleteMachineAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());
            var machineId = new Guid();

            var response = await AuthenticatedBrowser.Delete($"/v1/machines/{machineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task DeleteMachineReturnsNoContent()
        {
            var machineId = new Guid();

            var response = await AuthenticatedBrowser.Delete($"/v1/machines/{machineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validMachineId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var validMachineId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var validMachineId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsOk()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new MachineResponse()));

            var validMachineId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new MachineResponse()));

            var validMachineId = Guid.NewGuid();

            var response = await UnauthenticatedBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsValidationFailedException()
        {
            var errors = Enumerable.Empty<ValidationFailure>();
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(errors));

            var validMachineId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsFound()
        {
            var validTenantId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new List<MachineResponse>()));

            _machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
                .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var response = await AuthenticatedBrowser.Get("/v1/tenantmachines", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsInternalServerError()
        {
            var validTenantId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>()))
                .Throws(new Exception());

            _machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
                .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var response = await AuthenticatedBrowser.Get("/v1/tenantmachines", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsNotFoundException()
        {
            var validTenantId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            _machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
                .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var response = await AuthenticatedBrowser.Get("/v1/tenantmachines", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsValidationException()
        {
            var validTenantId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(Enumerable.Empty<ValidationFailure>()));

            _machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
                .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var response = await AuthenticatedBrowser.Get("/v1/tenantmachines", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsBadRequest()
        {
            var response = await AuthenticatedBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539", ctx => BuildRequest(ctx, "bad request"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.UpdateMachineAsync(It.IsAny<UpdateMachineRequest>(), It.IsAny<Guid>())).Throws(new Exception());

            var response = await AuthenticatedBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539", ctx => BuildRequest(ctx,
                new Machine
                {
                    MachineKey = "12345678901234567890",
                    Location = "TestLocation",
                    ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                    SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                    DateModified = DateTime.UtcNow
                }));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsNotFound()
        {
            var response = await AuthenticatedBrowser.Put("/v1/machines/notavalidmachine", ctx => BuildRequest(ctx,
                new Machine
                {
                    MachineKey = "12345678901234567890",
                    Location = "TestLocation",
                    ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                    SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                    DateModified = DateTime.UtcNow
                }));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsOk()
        {
            var response = await AuthenticatedBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539", ctx => BuildRequest(ctx,
                new Machine
                {
                    MachineKey = "12345678901234567890",
                    Location = "TestLocation",
                    ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                    SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                    DateModified = DateTime.UtcNow
                }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539", ctx => BuildRequest(ctx,
                new Machine
                {
                    MachineKey = "12345678901234567890",
                    Location = "TestLocation",
                    ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                    SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                    DateModified = DateTime.UtcNow
                }));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}