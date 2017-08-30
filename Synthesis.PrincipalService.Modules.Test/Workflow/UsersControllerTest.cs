using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.PrincipalService.Validators;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Workflow.Controllers;
using Synthesis.Nancy.MicroService;
using System;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using Synthesis.Nancy.MicroService.Validation;

namespace Synthesis.PrincipalService.Modules.Test.Workflow
{
    public class UsersControllerTest
    {
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IRepository<User>> _repositoryMock = new Mock<IRepository<User>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly IUsersController _controller;

        public UsersControllerTest()
        {
            // repository mock
            _repositoryFactoryMock.Setup(m => m.CreateRepository<User>())
                                  .Returns(_repositoryMock.Object);

            // event service mock
            _eventServiceMock.Setup(m => m.PublishAsync(It.IsAny<ServiceBusEvent<User>>()));
            

            _validatorMock.Setup(m => m.ValidateAsync(It.IsAny<object>(), CancellationToken.None))
                          .ReturnsAsync(new ValidationResult());

            // validator mock
            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                                 .Returns(_validatorMock.Object);            

            _controller = new UsersController(_repositoryFactoryMock.Object, 
                                                _validatorLocatorMock.Object,
                                                     _eventServiceMock.Object,
                                                     _loggerMock.Object);
        }

        [Fact]
        public async Task GetUserByIdAsyncReturnsUserIfExists()
        {
            _repositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new User()));

            var userId = Guid.NewGuid();
            var result = await _controller.GetUserAsync(userId);

            Assert.IsType<User>(result);
        }

        [Fact]
        public async Task GetUserByIdAsyncThrowsNotFoundExceptionIfUserDoesNotExist()
        {
            _repositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                           .Returns(Task.FromResult(default(User)));

            var userId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetUserAsync(userId));
        }
    }
}
