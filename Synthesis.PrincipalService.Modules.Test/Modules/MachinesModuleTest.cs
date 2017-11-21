using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Testing;
using Nancy.TinyIoc;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.License.Manager.Interfaces;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Serialization;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Utilities;
using Synthesis.PrincipalService.Workflow.Controllers;
using Synthesis.PrincipalService.Workflow.Exceptions;
using Xunit;
using ClaimTypes = System.Security.Claims.ClaimTypes;
using Synthesis.Nancy.MicroService;
using System.Linq;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public class MachinesModuleTest
    {
        private readonly Browser _browserAuth;
        private readonly Browser _browserNoAuth;

        private readonly Mock<IMachineController> _controllerMock = new Mock<IMachineController>();
        private readonly Mock<IRepository<Machine>> _machineRepositoryMock = new Mock<IRepository<Machine>>();

        public MachinesModuleTest()
        {
            _browserAuth = BrowserWithRequestStartup((container, pipelines, context) =>
            {
                context.CurrentUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
                                                                             {
                                                                                new Claim(ClaimTypes.Name, "TestUser"),
                                                                                new Claim(ClaimTypes.Email, "test@user.com"),
                                                                                new Claim("TenantId", "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3"),
                                                                                new Claim("UserId", "16367A84-65E7-423C-B2A5-5C42F8F1D5F2")
                                                                             },
                                                                             AuthenticationTypes.Basic));
            });
            _browserNoAuth = BrowserWithRequestStartup((container, pipelines, context) => { });
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
                var resource = new Machine()
                {
                    Id = Guid.Parse("64d54c99-f0e6-44a4-aa56-4792f782527c"),
                    MachineKey = string.Empty,
                    Location = string.Empty,
                    SettingProfileId = Guid.Parse("5558bf1d-8fa5-4332-9a79-dc7ee9a390e9"),
                    TenantId = Guid.Parse("24a5372f-b6b5-4901-82db-ca9779cf051f"),
                    DateCreated = DateTime.UtcNow,
                    DateModified = DateTime.UtcNow,
                    ModifiedBy = Guid.Parse("61be0f58-0a89-43d8-937a-6eae3d8d441b"),
                    SynthesisVersion = string.Empty,
                    LastOnline = DateTime.UtcNow
                };
                var repositoryMock = new Mock<IRepository<Machine>>();
                repositoryMock
                    .Setup(r => r.GetItemAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(resource);

                var repositoryFactoryMock = new Mock<IRepositoryFactory>();
                repositoryFactoryMock
                    .Setup(f => f.CreateRepository<Machine>())
                    .Returns(repositoryMock.Object);

                var eventServiceMock = new Mock<IEventService>();
                eventServiceMock.Setup(s => s.PublishAsync(It.IsAny<string>()));

                var validatorMock = new Mock<IValidator>();
                validatorMock
                    .Setup(v => v.ValidateAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult());
                var validatorLocatorMock = new Mock<IValidatorLocator>();
                validatorLocatorMock
                    .Setup(l => l.GetValidator(It.IsAny<Type>()))
                    .Returns(validatorMock.Object);
                var mapper = new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<MachineProfile>();
                }).CreateMapper();

                var mockEmailUtility = new Mock<IEmailUtility>();
                var mockLicenseApi = new Mock<ILicenseApi>();

                with.EnableAutoRegistration();
                with.RequestStartup(requestStartup);
                with.Dependency(new Mock<IMetadataRegistry>().Object);
                with.Dependency(loggerFactory);
                with.Dependency(logger);
                with.Dependency(validatorLocatorMock.Object);
                with.Dependency(repositoryFactoryMock.Object);
                with.Dependency(eventServiceMock.Object);
                with.Dependency(_controllerMock.Object);
                with.Dependency(mockEmailUtility.Object);
                with.Dependency(mockLicenseApi.Object);
                with.Module<MachinesModule>();
                with.Serializer<SynthesisJsonSerializer>();
            });
        }

        #region POST tests
        [Fact]
        public async Task CreateMachineReturnStatusOK()
        {
            var actual = await _browserAuth.Post("v1/machines", with =>
            {
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
                with.HttpRequest();
                with.JsonBody(new Machine());
            });
            Assert.Equal(HttpStatusCode.Created, actual.StatusCode);
        }

        [Fact]
        public async Task CreateMachineReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(x => x.CreateMachineAsync(It.IsAny<CreateMachineRequest>(), It.IsAny<Guid>())).Throws(new Exception());
            var actual = await _browserAuth.Post("v1/machines", with =>
            {
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
                with.HttpRequest();
                with.JsonBody(new Machine());
            });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Fact]
        public async Task CreateMachineRequestWithInvalidBodyReturnsBadRequest()
        {
            var invalidBody = "{]";
            var actual = await _browserAuth.Post("v1/machines", with =>
            {
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
                with.HttpRequest();
                with.JsonBody(invalidBody);
            });

            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, actual.ReasonPhrase);
        }

        [Fact]
        public async Task CreateMachineReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateMachineAsync(It.IsAny<CreateMachineRequest>(), It.IsAny<Guid>()))
                          .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var actual = await _browserAuth.Post("v1/machines", with =>
            {
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
                with.HttpRequest();
                with.JsonBody(new Machine());
            });

            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, actual.ReasonPhrase);
        }
        #endregion

        #region GET tests
        [Fact]
        public async Task GetMachineByIdReturnsOk()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new MachineResponse()));

            var validMachineId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/machines/{validMachineId}",
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validMachineId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/machines/{validMachineId}",
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new MachineResponse()));

            var validMachineId = Guid.NewGuid();
            var response = await _browserNoAuth.Get($"/v1/machines/{validMachineId}",
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var validMachineId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/machines/{validMachineId}",
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new NotFoundException(string.Empty));

            var validMachineId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/machines/{validMachineId}",
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetMachineByIdReturnsValidationFailedException()
        {
            var errors = Enumerable.Empty<ValidationFailure>();
            _controllerMock.Setup(m => m.GetMachineByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(errors));

            var validMachineId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/machines/{validMachineId}",
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        #endregion

        #region GET by MachineKey Tests
        [Fact]
        public async Task GetMachineByKeyReturnsOk()
        {
            _controllerMock.Setup(m => m.GetMachineByKeyAsync(It.IsAny<String>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new MachineResponse()));

            var validMachineKey = Guid.NewGuid().ToString();
            var response = await _browserAuth.Get($"/v1/machines/machinekey/{validMachineKey}",
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
            var response = await _browserNoAuth.Get($"/v1/machines/machinekey/{validMachineKey}",
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
            var response = await _browserAuth.Get($"/v1/machines/machinekey/{validMachineKey}",
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
            var response = await _browserAuth.Get($"/v1/machines/machinekey/{validMachineKey}",
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                  });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        #endregion

        #region UPDATE Tests
        [Fact]
        public async Task UpdateMachineReturnsOK()
        {
            var actual = await _browserAuth.Put(
                                                "/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Conteny-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new Machine()
                                                    {
                                                        MachineKey = "12345678901234567890",
                                                        Location = "TestLocation",
                                                        ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                                                        SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                                                        DateModified = DateTime.UtcNow
                                                    });
                                                });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.UpdateMachineAsync(It.IsAny<UpdateMachineRequest>(), It.IsAny<Guid>())).Throws(new Exception());
            var actual = await _browserAuth.Put(
                                                "/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Conteny-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new Machine()
                                                    {
                                                        MachineKey = "12345678901234567890",
                                                        Location = "TestLocation",
                                                        ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                                                        SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                                                        DateModified = DateTime.UtcNow
                                                    });
                                                });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsNotFound()
        {
            var actual = await _browserAuth.Put(
                                                "/v1/machines/notavalidmachine",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Conteny-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new Machine()
                                                    {
                                                        MachineKey = "12345678901234567890",
                                                        Location = "TestLocation",
                                                        ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                                                        SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                                                        DateModified = DateTime.UtcNow
                                                    });
                                                });
            Assert.Equal(HttpStatusCode.NotFound, actual.StatusCode);
        }

        [Fact]
        public async Task UpdateMachineReturnsUnauthorized()
        {
            var actual = await _browserNoAuth.Put(
                                                "/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Conteny-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new Machine()
                                                    {
                                                        MachineKey = "12345678901234567890",
                                                        Location = "TestLocation",
                                                        ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                                                        SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                                                        DateModified = DateTime.UtcNow
                                                    });
                                                });
            Assert.Equal(HttpStatusCode.Unauthorized, actual.StatusCode);
        }
        [Fact]
        public async Task UpdateMachineReturnsBadRequest()
        {
            var actual = await _browserAuth.Put(
                                                "/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Conteny-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(";)[");
                                                });
            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsOk()
        {
            var actual = await _browserAuth.Put(
                                                "/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Conteny-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new Machine()
                                                    {
                                                        MachineKey = "12345678901234567890",
                                                        Location = "TestLocation",
                                                        ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                                                        SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                                                        DateModified = DateTime.UtcNow
                                                    });
                                                });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.ChangeMachineAccountAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>())).Throws(new Exception());
            var actual = await _browserAuth.Put(
                                                "/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Conteny-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new Machine()
                                                    {
                                                        MachineKey = "12345678901234567890",
                                                        Location = "TestLocation",
                                                        ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                                                        SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                                                        DateModified = DateTime.UtcNow
                                                    });
                                                });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsNotFound()
        {
            var actual = await _browserAuth.Put(
                                                "/v1/machines/notavalidmachine/changeaccount",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Conteny-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new Machine()
                                                    {
                                                        MachineKey = "12345678901234567890",
                                                        Location = "TestLocation",
                                                        ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                                                        SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                                                        DateModified = DateTime.UtcNow
                                                    });
                                                });
            Assert.Equal(HttpStatusCode.NotFound, actual.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsUnauthorized()
        {
            var actual = await _browserNoAuth.Put(
                                                  "/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount",
                                                  with =>
                                                  {
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Conteny-Type", "application/json");
                                                      with.HttpRequest();
                                                      with.JsonBody(new Machine()
                                                      {
                                                          MachineKey = "12345678901234567890",
                                                          Location = "TestLocation",
                                                          ModifiedBy = Guid.Parse("1d31260e-22cd-4cc2-8177-b6946f76ca10"),
                                                          SettingProfileId = Guid.Parse("f8d5b613-9d21-4e84-acac-c70f3679d1e6"),
                                                          DateModified = DateTime.UtcNow
                                                      });
                                                  });
            Assert.Equal(HttpStatusCode.Unauthorized, actual.StatusCode);
        }

        [Fact]
        public async Task ChangeMachineAccountReturnsBadRequest()
        {
            var actual = await _browserAuth.Put(
                                                "/v1/machines/6b47560d-772a-41e5-8196-fb1ec6178539/changeaccount",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Conteny-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(";)[");
                                                });
            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
        }
        #endregion

        #region DELETE Tests

        [Fact]
        public async Task DeleteMachineReturnsNoContent()
        {
            var machineId = new Guid();
            var actual = await _browserAuth.Delete(
                                                   $"/v1/machines/{machineId}", with =>
                                                               {
                                                                   with.Header("Accept", "application/json");
                                                                   with.Header("Content-Type", "application/json");
                                                                   with.HttpRequest();
                                                               });
            Assert.Equal(HttpStatusCode.NoContent, actual.StatusCode);
        }

        [Fact]
        public async Task DeleteMachineReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.DeleteMachineAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));
            var machineId = new Guid();
            var actual = await _browserAuth.Delete(
                                                   $"/v1/machines/{machineId}", with =>
                                                                                {
                                                                                    with.Header("Accept", "application/json");
                                                                                    with.Header("Content-Type", "application/json");
                                                                                    with.HttpRequest();
                                                                                });
            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, actual.ReasonPhrase);
        }

        [Fact]
        public async Task DeleteMachineReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.DeleteMachineAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());
            var machineId = new Guid();
            var actual = await _browserAuth.Delete(
                                                   $"/v1/machines/{machineId}", with =>
                                                                                {
                                                                                    with.Header("Accept", "application/json");
                                                                                    with.Header("Content-Type", "application/json");
                                                                                    with.HttpRequest();
                                                                                });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        #endregion

        #region Get Tenant Machines Tests

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsFound()
        {
            var validTenantId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetTenantMachinesAsync(It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<MachineResponse>()));

            _machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
                               .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var response = await _browserAuth.Get("/v1/tenantmachines",
                                                    with =>
                                                    {
                                                        with.HttpRequest();
                                                        with.Header("Accept", "application/json");
                                                        with.Header("Content-Type", "application/json");
                                                    });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

            var response = await _browserAuth.Get("/v1/tenantmachines",
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                  });
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

            var response = await _browserAuth.Get("/v1/tenantmachines",
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                  });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

            var response = await _browserAuth.Get("/v1/tenantmachines",
                                                  with =>
                                                  {
                                                      with.HttpRequest();
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                  });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        
        #endregion

    }
}
