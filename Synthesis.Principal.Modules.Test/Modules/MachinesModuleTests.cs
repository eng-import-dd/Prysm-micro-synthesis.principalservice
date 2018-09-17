using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Requests;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    public class MachinesModuleTests : BaseModuleTests<MachinesModule>
    {
        /// <inheritdoc />
        protected override List<object> BrowserDependencies { get; }

        private readonly Mock<IMachinesController> _controllerMock = new Mock<IMachinesController>();

        public MachinesModuleTests()
        {
            BrowserDependencies = new List<object> { _controllerMock.Object };
        }

        [Fact]
        public async Task ChangeMachineTenantReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Put(string.Format(Routing.ChangeMachineTenantBase, "6b47560d-772a-41e5-8196-fb1ec6178539"), ctx => BuildRequest(ctx, "bad request"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineTenantReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.ChangeMachineTenantAsync(It.IsAny<ChangeMachineTenantRequest>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Put(string.Format(Routing.ChangeMachineTenantBase, "6b47560d-772a-41e5-8196-fb1ec6178539"), ctx => BuildRequest(ctx,
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
        public async Task ChangeMachineTenantReturnsNotFound()
        {
            var response = await UserTokenBrowser.Put(string.Format(Routing.ChangeMachineTenantBase, "notavalidmachine"), ctx => BuildRequest(ctx,
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
        public async Task ChangeMachineTenantReturnsOk()
        {
            var response = await UserTokenBrowser.Put(string.Format(Routing.ChangeMachineTenantBase, "6b47560d-772a-41e5-8196-fb1ec6178539"), ctx => BuildRequest(ctx,
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
        public async Task ChangeMachineTenantReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Put(string.Format(Routing.ChangeMachineTenantBase, "6b47560d-772a-41e5-8196-fb1ec6178539"), ctx => BuildRequest(ctx,
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
            var response = await UserTokenBrowser.Post(Routing.Machines, ctx => BuildRequest(ctx, "invalid machine"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateMachineReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateMachineAsync(It.IsAny<Machine>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(Routing.Machines, ctx => BuildRequest(ctx, new Machine { MachineKey = "TestMachineKey", Location = "Dummy", SettingProfileId = Guid.NewGuid() }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateMachineReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(x => x.CreateMachineAsync(It.IsAny<Machine>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Post(Routing.Machines, ctx => BuildRequest(ctx, new Machine { MachineKey = "TestMachineKey", Location = "Dummy", SettingProfileId = Guid.NewGuid() }));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateMachineReturnStatusOk()
        {
            var response = await UserTokenBrowser.Post(Routing.Machines, ctx => BuildRequest(ctx, new Machine { MachineKey = "TestMachineKey", Location = "Dummy", SettingProfileId = Guid.NewGuid() }));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task DeleteMachineReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.DeleteMachineAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var machineId = new Guid();

            var response = await UserTokenBrowser.Delete(string.Format(Routing.MachinesWithIdBase, machineId), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task DeleteMachineReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.DeleteMachineAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());
            var machineId = new Guid();

            var response = await UserTokenBrowser.Delete(string.Format(Routing.MachinesWithIdBase, machineId), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task DeleteMachineReturnsNoContent()
        {
            var machineId = new Guid();

            var response = await UserTokenBrowser.Delete(string.Format(Routing.MachinesWithIdBase, machineId), BuildRequest);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validMachineId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.MachinesWithIdBase, validMachineId), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());

            var validMachineId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.MachinesWithIdBase, validMachineId), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException(string.Empty));

            var validMachineId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.MachinesWithIdBase, validMachineId), BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsOk()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new Machine()));

            var validMachineId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.MachinesWithIdBase, validMachineId), BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new Machine()));

            var validMachineId = Guid.NewGuid();

            var response = await UnauthenticatedBrowser.Get(string.Format(Routing.MachinesWithIdBase, validMachineId), BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsValidationFailedException()
        {
            var errors = Enumerable.Empty<ValidationFailure>();
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Throws(new ValidationFailedException(errors));

            var validMachineId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.MachinesWithIdBase, validMachineId), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsFound()
        {
            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new List<Machine>()));

            var response = await UserTokenBrowser.Get(Routing.Machines, BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Get(Routing.Machines, BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsNotFoundException()
        {
            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await UserTokenBrowser.Get("/v1/tenantmachines", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsValidationException()
        {
            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Throws(new ValidationFailedException(Enumerable.Empty<ValidationFailure>()));

            var response = await UserTokenBrowser.Get(Routing.Machines, BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByKeyReturnsOk()
        {
            _controllerMock.Setup(m => m.GetMachineByKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.FromResult(new Machine()));

            var validMachineKey = Guid.NewGuid().ToString();
            var response = await UserTokenBrowser.Get(Routing.Machines,
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                      with.Query("machinekey", validMachineKey);
                                                  });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByKeyReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetMachineByKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                           .Returns(Task.FromResult(new Machine()));

            var validMachineKey = "0123456789";
            var response = await UnauthenticatedBrowser.Get(Routing.Machines,
                                                    with =>
                                                    {
                                                        with.HttpRequest();
                                                        with.Header("Accept", "application/json");
                                                        with.Header("Content-Type", "application/json");
                                                        with.Query("machinekey", validMachineKey);
                                                    });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByKeyReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetMachineByKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());

            const string validMachineKey = "0123456789";
            var response = await UserTokenBrowser.Get(Routing.Machines,
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                      with.Query("machinekey", validMachineKey);
                                                  });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByKeyReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetMachineByKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await UserTokenBrowser.Get(Routing.Machines,
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                      with.Query("machinekey", "0123456789");
                                                  });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Put(string.Format(Routing.MachinesWithIdBase, "6b47560d-772a-41e5-8196-fb1ec6178539"), ctx => BuildRequest(ctx, "bad request"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.UpdateMachineAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Put(string.Format(Routing.MachinesWithIdBase, "6b47560d-772a-41e5-8196-fb1ec6178539"), ctx => BuildRequest(ctx,
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
            var response = await UserTokenBrowser.Put(string.Format(Routing.MachinesWithIdBase, "notavalidmachine"), ctx => BuildRequest(ctx,
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
            var response = await UserTokenBrowser.Put(string.Format(Routing.MachinesWithIdBase, "6b47560d-772a-41e5-8196-fb1ec6178539"), ctx => BuildRequest(ctx,
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
            var response = await UnauthenticatedBrowser.Put(string.Format(Routing.MachinesWithIdBase, "6b47560d-772a-41e5-8196-fb1ec6178539"), ctx => BuildRequest(ctx,
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