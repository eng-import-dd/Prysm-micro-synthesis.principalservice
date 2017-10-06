﻿using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Workflow.Controllers;
using Synthesis.Nancy.MicroService;
using System;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using AutoMapper;
using Synthesis.License.Manager.Interfaces;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Requests;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;
using Synthesis.License.Manager.Models;
using Synthesis.PrincipalService.Utilities;

namespace Synthesis.PrincipalService.Modules.Test.Workflow
{
    public class MachinesControllerTest
    {
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IRepository<Machine>> _machineRepositoryMock = new Mock<IRepository<Machine>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly IMapper _mapper;
        private readonly IMachineController _controller;

        public MachinesControllerTest()
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

            // Mapper Mock
            _mapper = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile<MachineProfile>();
            }).CreateMapper();

            _controller = new MachinesController(_repositoryFactoryMock.Object,
                                              _validatorLocatorMock.Object,
                                              _loggerMock.Object,
                                              _mapper,
                                              _eventServiceMock.Object
                                              );
        }

        [Fact]
        public async Task CreateMachineAsyncReturnsNewMachineIfSuccessful()
        {
            _machineRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<Machine>())).Returns(Task.FromResult(new Machine()));

            var newMachine = new CreateMachineRequest();
            var result = await _controller.CreateMachineAsync(newMachine);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task CreateMachineAsyncThrowsValidationExceptionIfMachineKeyIsDuplicate()
        {
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>())).ReturnsAsync(new List<Machine> { new Machine() });
            var newMachineRequest = new CreateMachineRequest { MachineKey = "machinekey" };
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateMachineAsync(newMachineRequest));
            Assert.NotEqual(ex.Errors.ToList().Count, 0);
        }

        [Fact]
        public async Task CreateMachineAsyncThrowsValidationExceptionIfLocationIsDuplicate()
        {
            _machineRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Machine, bool>>>())).ReturnsAsync(new List<Machine> { new Machine() });
            var newMachineRequest = new CreateMachineRequest { AccountId = Guid.Parse("e4ae81cb-1ddb-4d04-9c08-307a40099620") };
            var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateMachineAsync(newMachineRequest));
            Assert.NotEqual(ex.Errors.ToList().Count, 0);
        }
    }
}
