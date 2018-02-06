using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Modules;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    public class MachinesModuleTests : BaseModuleTests<MachinesModule>
    {
        /// <inheritdoc />
        protected override List<object> BrowserDependencies { get; }
        private readonly Mock<IMachineController> _controllerMock = new Mock<IMachineController>();

        public MachinesModuleTests()
        {
            BrowserDependencies = new List<object> { _controllerMock.Object };
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount", ctx => BuildRequest(ctx, "bad request"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.ChangeMachineAccountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>())).Throws(new Exception());

            var response = await UserTokenBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount", ctx => BuildRequest(ctx,
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
            var response = await UserTokenBrowser.Put("/v1/machines/notavalidmachine/changeaccount", ctx => BuildRequest(ctx,
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
            var response = await UserTokenBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount", ctx => BuildRequest(ctx,
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
            var response = await UserTokenBrowser.Post("/v1/machines", ctx => BuildRequest(ctx, "invalid machine"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateMachineReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateMachineAsync(It.IsAny<CreateMachineRequest>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post("/v1/machines", ctx => BuildRequest(ctx, new Machine()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateMachineReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(x => x.CreateMachineAsync(It.IsAny<CreateMachineRequest>(), It.IsAny<Guid>())).Throws(new Exception());

            var response = await UserTokenBrowser.Post("/v1/machines", ctx => BuildRequest(ctx, new Machine()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateMachineReturnStatusOk()
        {
            var response = await UserTokenBrowser.Post("/v1/machines", ctx => BuildRequest(ctx, new Machine()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task DeleteMachineReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.DeleteMachineAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var machineId = new Guid();

            var response = await UserTokenBrowser.Delete($"/v1/machines/{machineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task DeleteMachineReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.DeleteMachineAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());
            var machineId = new Guid();

            var response = await UserTokenBrowser.Delete($"/v1/machines/{machineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task DeleteMachineReturnsNoContent()
        {
            var machineId = new Guid();

            var response = await UserTokenBrowser.Delete($"/v1/machines/{machineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validMachineId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var validMachineId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var validMachineId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsOk()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new MachineResponse()));

            var validMachineId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

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

            var response = await UserTokenBrowser.Get($"/v1/machines/{validMachineId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsFound()
        {
            //var validTenantId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new List<MachineResponse>()));

            //_machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
            //    .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var response = await UserTokenBrowser.Get("/v1/tenantmachines", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsInternalServerError()
        {
            //var validTenantId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>()))
                .Throws(new Exception());

            //_machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
            //    .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var response = await UserTokenBrowser.Get("/v1/tenantmachines", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsNotFoundException()
        {
            //var validTenantId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            //_machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
            //    .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var response = await UserTokenBrowser.Get("/v1/tenantmachines", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsValidationException()
        {
            //var validTenantId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(Enumerable.Empty<ValidationFailure>()));

            //_machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
            //    .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var response = await UserTokenBrowser.Get("/v1/tenantmachines", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByKeyReturnsOk()
        {
            _controllerMock.Setup(m => m.GetMachineByKeyAsync(It.IsAny<String>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new MachineResponse()));

            var validMachineKey = Guid.NewGuid().ToString();
            var response = await UserTokenBrowser.Get($"/v1/machines/machinekey/{validMachineKey}",
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                  });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByKeyReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetMachineByKeyAsync(It.IsAny<String>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new MachineResponse()));

            var validMachineKey = Guid.NewGuid();
            var response = await UnauthenticatedBrowser.Get($"/v1/machines/machinekey/{validMachineKey}",
                                                    with =>
                                                    {
                                                        with.HttpRequest();
                                                        with.Header("Accept", "application/json");
                                                        with.Header("Content-Type", "application/json");
                                                    });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByKeyReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetMachineByKeyAsync(It.IsAny<String>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var validMachineKey = Guid.NewGuid();
            var response = await UserTokenBrowser.Get($"/v1/machines/machinekey/{validMachineKey}",
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                  });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByKeyReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetMachineByKeyAsync(It.IsAny<String>(), It.IsAny<Guid>()))
                           .Throws(new NotFoundException(string.Empty));

            var validMachineKey = Guid.NewGuid();
            var response = await UserTokenBrowser.Get($"/v1/machines/machinekey/{validMachineKey}",
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                  });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539", ctx => BuildRequest(ctx, "bad request"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.UpdateMachineAsync(It.IsAny<UpdateMachineRequest>(), It.IsAny<Guid>())).Throws(new Exception());

            var response = await UserTokenBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539", ctx => BuildRequest(ctx,
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
            var response = await UserTokenBrowser.Put("/v1/machines/notavalidmachine", ctx => BuildRequest(ctx,
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
            var response = await UserTokenBrowser.Put("/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539", ctx => BuildRequest(ctx,
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