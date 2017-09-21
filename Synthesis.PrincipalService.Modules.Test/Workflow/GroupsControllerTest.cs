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
    public class GroupsControllerTest
    {
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IRepository<Group>> _groupRepositoryMock = new Mock<IRepository<Group>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly IGroupsController _controller;

        public GroupsControllerTest()
        {
            // repository mock
            _repositoryFactoryMock.Setup(m => m.CreateRepository<Group>())
                                  .Returns(_groupRepositoryMock.Object);

            // event service mock
            _eventServiceMock.Setup(m => m.PublishAsync(It.IsAny<ServiceBusEvent<Group>>()));


            _validatorMock.Setup(m => m.ValidateAsync(It.IsAny<object>(), CancellationToken.None))
                          .ReturnsAsync(new ValidationResult());

            // validator mock
            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                                 .Returns(_validatorMock.Object);

            _controller = new GroupsController(_repositoryFactoryMock.Object,
                                              _validatorLocatorMock.Object,
                                              _eventServiceMock.Object,
                                              _loggerMock.Object);
        }

        [Fact]
        public async Task CreateGroupAsyncReturnsNewGroupIfSuccessful()
        {
            _groupRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<Group>()))
                                      .Returns(Task.FromResult(new Group()));

            var newGroupRequest = new Group(); 
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var result = await _controller.CreateGroupAsync(newGroupRequest, tenantId,userId);
            Assert.IsType<Group>(result);
        }
    }
}