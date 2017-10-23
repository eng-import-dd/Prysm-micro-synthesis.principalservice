using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Workflow.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.Nancy.MicroService;

namespace Synthesis.PrincipalService.Modules.Test.Workflow
{
    public class GroupsControllerTest
    {
        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<IRepository<Group>> _groupRepositoryMock = new Mock<IRepository<Group>>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly IGroupsController _controller;

        public GroupsControllerTest()
        {
            // repository mock
            _repositoryFactoryMock.Setup(m => m.CreateRepository<Group>())
                                  .Returns(_groupRepositoryMock.Object);

            _repositoryFactoryMock.Setup(m => m.CreateRepository<User>())
                                  .Returns(_userRepositoryMock.Object);

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

        #region Get Group By Id Test Cases

        [Fact]
        public async Task GetGroupByIdReturnsGroupIfExists()
        {
            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(new Group()
                                {
                                    TenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3")
                                });

            var groupId = Guid.NewGuid();
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            var result = await _controller.GetGroupByIdAsync(groupId, tenantId);

            Assert.IsType<Group>(result);
        }

        [Fact]
        public async Task GetGroupByIdThrowsNotFoundExceptionIfGroupDoesNotExist()
        {
            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                               .ReturnsAsync(default(Group));

            var groupId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetGroupByIdAsync(groupId, tenantId));
        }

        [Fact]
        public async Task GetGroupByIdThrowsValidationException()
        {
            var errors = Enumerable.Empty<ValidationFailure>();
            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                                .Throws(new ValidationFailedException(errors));

            var groupId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.GetGroupByIdAsync(groupId, tenantId));
        }

        #endregion

        #region Get Groups For Tenant Test Cases

        [Trait("GetGroupsForTenant", "Get Groups For Tenant Test Cases")]
        [Fact]
        public async Task GetGroupsForTenantReturnsGroupsIfExists()
        {
            const int count = 5;
            _groupRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Group, bool>>>()))
                                .Returns(() =>
                                {
                                    var itemsList = new List<Group>();
                                    for (var i = 0; i < count; i++)
                                    {
                                        itemsList.Add(new Group());
                                    }

                                    IEnumerable<Group> items = itemsList;
                                    return (Task.FromResult(items));
                                });

            var result = await _controller.GetGroupsForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>());

            Assert.Equal(count, result.Count());
        }

        [Trait("GetGroupsForTenant", "Get Groups For Tenant Test Cases")]
        [Fact]
        public async Task GetGroupsForTenantReturnsNoMatchingRecords()
        {
            const int count = 0;
            _groupRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<Group, bool>>>()))
                                .Returns(() =>
                                         {
                                             var itemsList = new List<Group>();
                                             for (var i = 0; i < count; i++)
                                             {
                                                 itemsList.Add(new Group());
                                             }

                                             IEnumerable<Group> items = itemsList;
                                             return (Task.FromResult(items));
                                         });

            var result = await _controller.GetGroupsForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>());

            Assert.Equal(0, result.Count());
        }

        #endregion
        
        #region Delete Group Test Cases
        [Fact]
        public async Task DeleteGroupAsyncReturnsTrueIfSuccessful()
        {
            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                                .Returns(Task.FromResult(new Group()));
            _groupRepositoryMock.Setup(m => m.DeleteItemAsync(It.IsAny<Guid>()))
                                .Returns(Task.FromResult(Guid.NewGuid()));
            _userRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<User>()))
                               .ReturnsAsync(new User());
            var groupId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var result = await _controller.DeleteGroupAsync(groupId, userId);
            Assert.Equal(true, result);
        }

        [Fact]
        public async Task DeleteGroupAsyncReturnstrueIfDocumentNotFound()
        {
            _groupRepositoryMock.Setup(m => m.DeleteItemAsync(It.IsAny<Guid>()))
                                .Throws(new DocumentNotFoundException());
            var userId = Guid.NewGuid();
            var result = await _controller.DeleteGroupAsync(Guid.Empty, userId);
            Assert.Equal(true, result);
        }

        [Fact]
        public async Task DeleteGroupAsyncReturnsFalseDueToException()
        {
            _groupRepositoryMock.Setup(m => m.DeleteItemAsync(It.IsAny<Guid>()))
                                .Throws(new Exception());
            var userId = Guid.NewGuid();
            var result = await _controller.DeleteGroupAsync(Guid.Empty, userId);
            Assert.Equal(false, result);
        }
        #endregion

        #region Update Group Test Cases

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupAsyncReturnsTrueIfSuccessful()
        {
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            var userId = Guid.NewGuid();

            Group group = new Group()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Prysm Group",
                IsLocked = false
            };

            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                                .Returns(Task.FromResult(group));

            _groupRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<Group>()))
                .Returns(Task.FromResult(group));
           

            var result = await _controller.UpdateGroupAsync(group, tenantId, userId);
            //Assert.NotNull(result);
            Assert.Equal(group, result);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupAsyncReturnstrueIfDocumentNotFound()
        {
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            var userId = Guid.NewGuid();

            Group group = new Group()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Prysm Group",
                IsLocked = false
            };


            _groupRepositoryMock.Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<Group>()))
                                .Throws(new NotFoundException(string.Empty));

            await Assert.ThrowsAsync<NotFoundException>(() => _controller.UpdateGroupAsync(group, tenantId, userId));
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupByIdThrowsValidationException()
        {
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            var userId = Guid.NewGuid();

            Group group = new Group()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Prysm Group",
                IsLocked = false
            };

            var errors = Enumerable.Empty<ValidationFailure>();

            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                                .Throws(new ValidationFailedException(errors));

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.UpdateGroupAsync(group, tenantId, userId));
        }
       
        #endregion
    }
}
