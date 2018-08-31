using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.DocumentStorage.TestTools.Mocks;
using Synthesis.EventBus;
using Synthesis.Http.Microservice;
using Synthesis.Http.Microservice.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Services;
using Synthesis.TenantService.InternalApi.Api;
using Synthesis.TenantService.InternalApi.Models;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Controllers
{
    public class MachinesControllerTests
    {
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IRepository<Machine>> _machineRepositoryMock = new Mock<IRepository<Machine>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<ICloudShim> _cloudShimMock = new Mock<ICloudShim>();
        private readonly Mock<ITenantApi> _tenantApiMock = new Mock<ITenantApi>();
        private readonly IMachinesController _controller;

        public MachinesControllerTests()
        {
            // repository mock
            _repositoryFactoryMock.Setup(m => m.CreateRepositoryAsync<Machine>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_machineRepositoryMock.Object);

            _validatorMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult());

            // validator mock
            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            // logger factory mock
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(_loggerMock.Object);

            _controller = new MachinesController(
                _repositoryFactoryMock.Object,
                _validatorLocatorMock.Object,
                loggerFactoryMock.Object,
                _eventServiceMock.Object,
                _cloudShimMock.Object,
                _tenantApiMock.Object);
        }

        [Fact]
        public async Task ChangeMachineTenantAsync_WhenMachineNotFound_ThrowsNotFoundException()
        {
            _machineRepositoryMock.SetupCreateItemQuery();

            var request = new ChangeMachineTenantRequest
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                SettingProfileId = Guid.NewGuid()
            };

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.ChangeMachineTenantAsync(request, null));
        }

        [Fact]
        public async Task ChangeMachineTenantAsync_WhenSuccessfulChange_ReturnsUpdatedMachine()
        {
            var machineId = Guid.NewGuid();

            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { new Machine { Id = machineId } });

            _tenantApiMock.Setup(m => m.GetTenantByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new Tenant()));

            _cloudShimMock.Setup(m => m.ValidateSettingProfileId(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, true));

            _machineRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<Machine>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid i, Machine m, UpdateOptions o, CancellationToken t) => m);

            var request = new ChangeMachineTenantRequest
            {
                Id = machineId,
                TenantId = Guid.NewGuid(),
                SettingProfileId = Guid.NewGuid()
            };
            var result = await _controller.ChangeMachineTenantAsync(request, null, CancellationToken.None);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task ChangeMachineTenantAsync_WhenEmptyTenantId_ThrowsValidationFailedException()
        {
            var request = new ChangeMachineTenantRequest
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                SettingProfileId = Guid.NewGuid()
            };

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.ChangeMachineTenantAsync(request, null));

            Assert.Collection(ex.Errors, failure => Assert.Equal(nameof(ChangeMachineTenantRequest.TenantId), failure.PropertyName));
        }

        [Fact]
        public async Task ChangeMachineTenantAsync_WhenTenantNotFound_ThrowsValidationFailedException()
        {
            var machineId = Guid.NewGuid();

            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { new Machine { Id = machineId } });

            _tenantApiMock.Setup(m => m.GetTenantByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<Tenant>(HttpStatusCode.NotFound, (ErrorResponse)null, "Not found"));

            var request = new ChangeMachineTenantRequest
            {
                Id = machineId,
                TenantId = Guid.NewGuid(),
                SettingProfileId = Guid.NewGuid()
            };

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.ChangeMachineTenantAsync(request, null));

            Assert.Collection(ex.Errors, failure => Assert.Equal(nameof(ChangeMachineTenantRequest.TenantId), failure.PropertyName));
        }

        [Fact]
        public async Task ChangeMachineTenantAsync_WhenInvalidSettingProfileId_ThrowsValidationFailedException()
        {
            var machineId = Guid.NewGuid();

            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { new Machine { Id = machineId } });

            _tenantApiMock.Setup(m => m.GetTenantByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid tenantId) => MicroserviceResponse.Create(HttpStatusCode.OK, new Tenant { Id = tenantId }));

            _cloudShimMock.Setup(m => m.ValidateSettingProfileId(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, false));

            var request = new ChangeMachineTenantRequest
            {
                Id = machineId,
                TenantId = Guid.NewGuid(),
                SettingProfileId = Guid.NewGuid()
            };

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.ChangeMachineTenantAsync(request, null));

            Assert.Collection(ex.Errors, failure => Assert.Equal(nameof(ChangeMachineTenantRequest.SettingProfileId), failure.PropertyName));
        }

        [Fact]
        public async Task CreateMachineAsync_WhenSuccessfulCreation_ReturnsCreatedMachine()
        {
            _machineRepositoryMock.SetupCreateItemQuery();

            _machineRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<Machine>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Machine m, CancellationToken ct) => m);

            var newMachine = Machine.Example();

            var result = await _controller.CreateMachineAsync(newMachine, CancellationToken.None);

            Assert.Same(newMachine, result);
        }

        [Fact]
        public async Task CreateMachineAsync_WhenLocationIsDuplicate_ThrowsValidationFailedException()
        {
            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { new Machine { Location = "location", MachineKey = string.Empty } });

            var newMachineRequest = new Machine { TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620"), Location = "location", MachineKey = string.Empty };

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateMachineAsync(newMachineRequest));
        }

        [Fact]
        public async Task CreateMachineAsync_WhenMachineKeyIsDuplicate_ThrowsValidationFailedException()
        {
            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { new Machine { Id = Guid.NewGuid(), Location = string.Empty, MachineKey = "MACHINEKEY" } });

            var newMachineRequest = new Machine { Id = Guid.NewGuid(), MachineKey = "machinekey", Location = string.Empty };

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateMachineAsync(newMachineRequest));
        }

        [Fact]
        public async Task DeleteMachineAsync_WhenMachineNotFound_DoesNotThrow()
        {
            _machineRepositoryMock.Setup(m => m.DeleteItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new DocumentNotFoundException());

            await _controller.DeleteMachineAsync(Guid.NewGuid(), Guid.NewGuid());
        }

        [Fact]
        public async Task DeleteMachinAsync_WhenDeletionSuccessful_DoesNotThrow()
        {
            _machineRepositoryMock.Setup(m => m.DeleteItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Guid.NewGuid()));

            await _controller.DeleteMachineAsync(Guid.NewGuid(), Guid.NewGuid());
        }

        [Fact]
        public async Task DeleteMachineAsync_WhenRepositoryThrowsOtherThanDoucmentNotFound_ThrowsSameException()
        {
            _machineRepositoryMock.Setup(m => m.DeleteItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.DeleteMachineAsync(Guid.NewGuid(), Guid.NewGuid()));

            Assert.IsType<InvalidOperationException>(ex);
        }

        [Fact]
        public async Task GetMachineByIdAsync_WhenMachineFound_ReturnsMachine()
        {
            var machineId = Guid.NewGuid();
            var testMachine = new Machine
            {
                Id = machineId,
                TenantId = Guid.Parse("d65bb77a-2658-4576-9b78-a6fc01a57c47")
            };
            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { testMachine });

            var tenantId = Guid.Parse("d65bb77a-2658-4576-9b78-a6fc01a57c47");
            var result = await _controller.GetMachineByIdAsync(machineId, tenantId);

            Assert.IsType<Machine>(result);
        }

        [Fact]
        public async Task GetMachineByIdAsync_WhenMachineNotFound_ThrowsNotFoundException()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(Machine));

            var testMachine = new Machine
            {
                Id = Guid.NewGuid()
            };
            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { testMachine });

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetMachineByIdAsync(Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task GetMachineById_WhenValidationFails_ThrowsValidationFailedException()
        {
            const string propertyName = "PropertyName";

            _validatorMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult(new[] { new ValidationFailure(propertyName, string.Empty) }));

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.GetMachineByIdAsync(Guid.NewGuid(), Guid.NewGuid()));

            Assert.Collection(ex.Errors, failure => Assert.Equal(propertyName, failure.PropertyName));
        }

        [Fact]
        [Trait("Tenant Machines", "Tenant Machines")]
        public async Task GetTenantMachinesAsync_WhenSuccessful_ReturnsRepositoryQueryResults()
        {
            var validTenantId = Guid.NewGuid();
            var expectedData = Enumerable.Range(0, 10)
                .Select(_ => new Machine { Id = Guid.NewGuid(), TenantId = validTenantId })
                .ToList();

            _machineRepositoryMock.SetupCreateItemQuery(o => expectedData);

            var result = await _controller.GetTenantMachinesAsync(validTenantId);

            Assert.Equal<Machine>(expectedData, result);
        }

        [Trait("Tenant Machines", "Tenant Machines")]
        [Fact]
        public async Task GetTenantMachinesAsync_WhenDataDoesNotExist_ReturnsEmptyList()
        {
            // Collection contains 10 documents.
            _machineRepositoryMock.SetupCreateItemQuery(o => Enumerable.Range(0, 10).Select(_ => Machine.Example()));

            // A TenantId of Guid.NewGuid() will not match any machines above.
            var result = await _controller.GetTenantMachinesAsync(Guid.NewGuid());

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMachineByKeyAsync_WhenKeyMatched_ReturnsMachine()
        {
            var machineKey = Guid.NewGuid().ToString();
            var testMachine = new Machine
            {
                MachineKey = machineKey.ToUpperInvariant(),
                TenantId = Guid.Parse("d65bb77a-2658-4576-9b78-a6fc01a57c47")
            };
            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { testMachine });

            var result = await _controller.GetMachineByKeyAsync(machineKey, Guid.Parse("d65bb77a-2658-4576-9b78-a6fc01a57c47"));

            Assert.IsType<Machine>(result);
        }

        [Fact]
        public async Task GetMachineByKeyAsync_WhenMachineDoesNotExist_ThrowsNotFoundException()
        {
            var testMachine = new Machine
            {
                MachineKey = Guid.NewGuid().ToString(),
                TenantId = Guid.Parse("d65bb77a-2658-4576-9b78-a6fc01a57c47")
            };

            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { testMachine });

            var machineKey = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid();

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetMachineByKeyAsync(machineKey, tenantId));
        }

        [Fact]
        public async Task UpdateMachineAsync_WhenSuccessful_ReturnsUpdatedMachine()
        {
            var testMachine = new Machine
            {
                TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620"),
                MachineKey = "1122334455",
                Location = "Location"
            };

            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testMachine);

            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { testMachine });

            _machineRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<Machine>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, Machine m, UpdateOptions o, CancellationToken t) => m);

            var newMachine = new Machine { Id = Guid.NewGuid(), MachineKey = "1234567890", Location = "New Location" };

            var result = await _controller.UpdateMachineAsync(newMachine, CancellationToken.None);

            Assert.NotNull(result);
        }

        [Fact]
        public async Task UpdateMachineAsync_WhenMachineNotFound_ThrowsNotFoundException()
        {
            _machineRepositoryMock.SetupCreateItemQuery();

            var machine = new Machine
            {
                Location = "location",
                MachineKey = Guid.NewGuid().ToString()
            };

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.UpdateMachineAsync(machine));
        }

        [Fact]
        public async Task UpdateMachineAsync_WhenLocationIsDuplicate_ThrowsValidationFailedException()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Machine { TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620"), Location = string.Empty, MachineKey = string.Empty });

            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { new Machine { Location = string.Empty, MachineKey = string.Empty } });

            var newMachineRequest = new Machine { TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620"), Location = string.Empty, MachineKey = string.Empty };

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.UpdateMachineAsync(newMachineRequest));

            Assert.NotEmpty(ex.Errors.ToList());
        }

        [Fact]
        public async Task UpdateMachineAsync_WhenMachineKeyIsDuplicate_ThrowsValidationFailedException()
        {
            _machineRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Machine { TenantId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620") });

            _machineRepositoryMock.SetupCreateItemQuery(o => new List<Machine> { new Machine { MachineKey = "machinekey", Location = "Abc" } });

            var newMachineRequest = new Machine { MachineKey = "machinekey", Location = string.Empty };

            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.UpdateMachineAsync(newMachineRequest));

            Assert.NotEmpty(ex.Errors.ToList());
        }
    }
}