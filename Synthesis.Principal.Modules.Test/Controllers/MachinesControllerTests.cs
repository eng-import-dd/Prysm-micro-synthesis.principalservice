using System;
using System.Collections.Generic;
using System.IdentityModel;
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
using Synthesis.EventBus;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Controllers
{
    public class MachinesControllerTests
    {
        public MachinesControllerTests()
        {
            // repository mock
            _repositoryFactoryMock.Setup(m => m.CreateRepository<Machine>())
                .Returns(_machineRepositoryMock.Object);

            // event service mock
            _eventServiceMock.Setup(m => m.PublishAsync(It.IsAny<ServiceBusEvent<Machine>>()));

            _validatorMock.Setup(m => m.ValidateAsync(It.IsAny<object>(), CancellationToken.None))
                .ReturnsAsync(new ValidationResult());

            // validator mock
            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            // logger factory mock
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(_loggerMock.Object);

            // Mapper Mock
            var mapper = new MapperConfiguration(cfg => { cfg.AddProfile<MachineProfile>(); }).CreateMapper();

            _controller = new MachinesController(_repositoryFactoryMock.Object,
                _validatorLocatorMock.Object,
                loggerFactoryMock.Object,
                mapper,
                _eventServiceMock.Object,
                _cloudShimMock.Object
            );
        }

        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IRepository<Machine>> _machineRepositoryMock = new Mock<IRepository<Machine>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<ICloudShim> _cloudShimMock = new Mock<ICloudShim>();
        private readonly MachinesController _controller;

        [Fact]
        public async Task ChangeMachineAccountAsyncNotFoundException()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync((Machine)null);
            var tenantId = Guid.NewGuid();
            var machineId = Guid.NewGuid();
            var settingProfileId = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<NotFoundException>(() => _controller.ChangeMachineAccountAsync(machineId, tenantId, settingProfileId));
            Assert.IsType<NotFoundException>(ex);
        }

        [Fact]
        public async Task ChangeMachineAccountAsyncReturnsNewMachineIfSuccessful()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync(new Machine());
            _cloudShimMock.Setup(m => m.ValidateSettingProfileId(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, true));
            var tenantId = Guid.NewGuid();
            var machineId = Guid.NewGuid();
            var settingProfileId = Guid.NewGuid();
            var result = await _controller.ChangeMachineAccountAsync(machineId, tenantId, settingProfileId);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task ChangeMachineAccountAsyncThrowsbadRequestException()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync(new Machine());
            var tenantId = Guid.NewGuid();
            var machineId = Guid.NewGuid();
            var settingProfileId = Guid.Empty;
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _controller.ChangeMachineAccountAsync(machineId, tenantId, settingProfileId));
            Assert.IsType<BadRequestException>(ex);
        }

        [Fact]
        public async Task CreateMachineAsyncReturnsNewMachineIfSuccessful()
        {
            _machineRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<Machine>())).Returns(Task.FromResult(new Machine()));
            var newMachine = new CreateMachineRequest();
            var tenantId = Guid.NewGuid();
            var result = await _controller.CreateMachineAsync(newMachine, tenantId);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateMachineAsyncThrowsValidationExceptionIfLocationIsDuplicate()
        {
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>())).ReturnsAsync(new List<Machine> { new Machine() });
            var newMachineRequest = new CreateMachineRequest { TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620") };
            var tenantId = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateMachineAsync(newMachineRequest, tenantId));
            Assert.NotEmpty(ex.Errors.ToList());
        }

        [Fact]
        public async Task CreateMachineAsyncThrowsValidationExceptionIfMachineKeyIsDuplicate()
        {
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>())).ReturnsAsync(new List<Machine> { new Machine() });
            var newMachineRequest = new CreateMachineRequest { MachineKey = "machinekey" };
            var tenantId = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateMachineAsync(newMachineRequest, tenantId));
            Assert.NotEmpty(ex.Errors.ToList());
        }

        [Fact]
        public async Task DeleteMachineReturnsTrueIfDocumentNotFound()
        {
            _machineRepositoryMock.Setup(m => m.DeleteItemAsync(It.IsAny<Guid>())).Throws(new NotFoundException("Not Found"));
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync(new Machine());
            var machineId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<NotFoundException>(() => _controller.DeleteMachineAsync(machineId, tenantId));
            Assert.IsType<NotFoundException>(ex);
        }

        [Fact]
        public async Task DeleteMachineReturnsTrueIfSuccessful()
        {
            _machineRepositoryMock.Setup(m => m.DeleteItemAsync(It.IsAny<Guid>())).Returns(Task.FromResult(Guid.NewGuid()));
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync(new Machine());
            var machineId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            await _controller.DeleteMachineAsync(machineId, tenantId);
        }

        [Fact]
        public async Task DeleteMachineReturnsTrueReturnsFalseIfException()
        {
            _machineRepositoryMock.Setup(m => m.DeleteItemAsync(It.IsAny<Guid>())).Throws(new Exception());
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync(new Machine());
            var machineId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<Exception>(() => _controller.DeleteMachineAsync(machineId, tenantId));
            Assert.IsType<Exception>(ex);
        }

        [Fact]
        public async Task GetMachineByIdReturnsMachineIfExists()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new Machine
                {
                    TenantId = Guid.Parse("d65bb77a-2658-4576-9b78-a6fc01a57c47")
                });

            var machineId = Guid.NewGuid();
            var tenantId = Guid.Parse("d65bb77a-2658-4576-9b78-a6fc01a57c47");
            var result = await _controller.GetMachineByIdAsync(machineId, tenantId);

            Assert.IsType<MachineResponse>(result);
        }

        [Fact]
        public async Task GetMachineByIdThrowsInvalidOperationException()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Throws(new InvalidOperationException());

            var machineId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.GetMachineByIdAsync(machineId, tenantId));
        }

        [Fact]
        public async Task GetMachineByIdThrowsNotFoundExceptionIfMachineDoesNotExist()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(Machine));

            var machineId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetMachineByIdAsync(machineId, tenantId));
        }

        [Fact]
        public async Task GetMachineByIdThrowsValidationException()
        {
            var errors = Enumerable.Empty<ValidationFailure>();
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(errors));

            var machineId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.GetMachineByIdAsync(machineId, tenantId));
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesReturnsDataIfExists()
        {
            var validTenantId = Guid.NewGuid();

            _machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
                .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var result = await _controller.GetTenantMachinesAsync(It.IsAny<Guid>());

            Assert.IsType<List<MachineResponse>>(result);
        }

        [Trait("Tenant Machines", "Tenant Machines")]
        [Fact]
        public async Task GetTenantMachinesThrowsNotFoundExceptionIfDataDoesNotExist()
        {
            var validTenantId = Guid.NewGuid();

            _machineRepositoryMock.Setup(m => m.GetItemsAsync(t => t.TenantId == validTenantId))
                .Returns(Task.FromResult(Enumerable.Empty<Machine>()));

            var result = await _controller.GetTenantMachinesAsync(It.IsAny<Guid>());

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMachineByKeyReturnsMachineIfExists()
        {
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>()))
                                  .ReturnsAsync(new List<Machine>
                                  {
                                      new Machine
                                      {
                                          TenantId = Guid.Parse("d65bb77a-2658-4576-9b78-a6fc01a57c47")
                                      }
                                  });

            var machineKey = Guid.NewGuid().ToString();
            var tenantId = Guid.Parse("d65bb77a-2658-4576-9b78-a6fc01a57c47");
            var result = await _controller.GetMachineByKeyAsync(machineKey, tenantId);

            Assert.IsType<MachineResponse>(result);
        }

        [Fact]
        public async Task GetMachineByKeyThrowsNotFoundExceptionIfMachineDoesNotExist()
        {
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>()))
                                  .ReturnsAsync(new List<Machine>
                                  {
                                      default(Machine)
                                  });

            var machineKey = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetMachineByKeyAsync(machineKey, tenantId));
        }

        [Fact]
        public async Task GetMachineByKeyThrowsInvalidOperationException()
        {
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>()))
                                  .Throws(new InvalidOperationException());

            var machineKey = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();
            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.GetMachineByKeyAsync(machineKey, tenantId));
        }

        [Fact]
        public async Task UpdateMachineAsyncReturnsNewMachineIfSuccessful()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync(new Machine { TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620") });

            var newMachine = new UpdateMachineRequest() { Id = Guid.NewGuid() };
            var tenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620");
            var result = await _controller.UpdateMachineAsync(newMachine, tenantId);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateMachineAsyncThrowsUnauthorizedException()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync(new Machine());
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>())).ReturnsAsync(new List<Machine> { new Machine { TenantId = Guid.Parse("d1532c34-09dd-4cf8-b893-894afde2adad") } });

            var newMachineRequest = new UpdateMachineRequest { TenantId = Guid.Parse("cb37242e-af65-45b4-bcb4-bd259f0b4c76") };
            var tenantId = Guid.NewGuid();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.UpdateMachineAsync(newMachineRequest, tenantId));
            Assert.IsType<InvalidOperationException>(ex);
        }

        [Fact]
        public async Task UpdateMachineAsyncThrowsValidationExceptionIfLocationIsDuplicate()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync(new Machine { TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620") });
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>())).ReturnsAsync(new List<Machine> { new Machine() });

            var newMachineRequest = new UpdateMachineRequest { TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620") };
            var tenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620");
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.UpdateMachineAsync(newMachineRequest, tenantId));
            Assert.NotEmpty(ex.Errors.ToList());
        }

        [Fact]
        public async Task UpdateMachineAsyncThrowsValidationExceptionIfMachineKeyIsDuplicate()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>())).ReturnsAsync(new Machine { TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620") });
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>())).ReturnsAsync(new List<Machine> { new Machine() });

            var newMachineRequest = new UpdateMachineRequest { MachineKey = "machinekey" };
            var tenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620");
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.UpdateMachineAsync(newMachineRequest, tenantId));
            Assert.NotEmpty(ex.Errors.ToList());
        }
    }
}
