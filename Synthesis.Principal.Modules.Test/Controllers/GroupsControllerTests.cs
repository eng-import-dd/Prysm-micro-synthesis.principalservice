using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Services;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Controllers
{
    public class GroupsControllerTests
    {
        public GroupsControllerTests()
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

            _validatorFailureMock
                .Setup(x => x.ValidateAsync(It.IsAny<object>(), CancellationToken.None))
                .ReturnsAsync(new ValidationResult(new List<ValidationFailure> {new ValidationFailure("", "")}));

            // validator mock
            _validatorLocatorMock.Setup(m => m.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(_loggerMock.Object);

            _controller = new GroupsController(_repositoryFactoryMock.Object,
                _validatorLocatorMock.Object,
                _eventServiceMock.Object,
                _superadminServiceMock.Object,
                loggerFactoryMock.Object);
        }

        private readonly Mock<IRepositoryFactory> _repositoryFactoryMock = new Mock<IRepositoryFactory>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IValidatorLocator> _validatorLocatorMock = new Mock<IValidatorLocator>();
        private readonly Mock<ISuperAdminService> _superadminServiceMock = new Mock<ISuperAdminService>();
        private readonly Mock<IRepository<Group>> _groupRepositoryMock = new Mock<IRepository<Group>>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<IValidator> _validatorFailureMock = new Mock<IValidator>();
        private readonly GroupsController _controller;

        [Theory]
        [InlineData(GroupType.Basic, GroupNames.Basic)]
        [InlineData(GroupType.TenantAdmin, GroupNames.TenantAdmin)]
        public async Task DefaultGroupsAreCreated(GroupType type, string groupName)
        {
            _groupRepositoryMock
                .Setup(m => m.CreateItemAsync(It.IsAny<Group>()))
                .ReturnsAsync(new Group());

            var tenantId = Guid.NewGuid();
            await _controller.CreateBuiltInGroupsAsync(tenantId);

            _groupRepositoryMock.Verify(y => y.CreateItemAsync(It.Is<Group>(x =>
                x.TenantId == tenantId && x.Name == groupName &&
                x.IsLocked && x.Type == type)));
        }

        [Fact]
        public async Task CreateGroupAsyncReturnsNewGroupIfSuccessful()
        {
            _groupRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<Group>()))
                .Returns(Task.FromResult(new Group()));

            var newGroupRequest = new Group();
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var result = await _controller.CreateGroupAsync(newGroupRequest, tenantId, userId, false);
            Assert.IsType<Group>(result);
        }

        [Fact]
        public async Task CreateGroupAsyncThrowsValidationExceptionIfBuiltInGroupsAreNotPermitted()
        {
            _groupRepositoryMock
                .Setup(m => m.CreateItemAsync(It.IsAny<Group>()))
                .Returns(Task.FromResult(new Group()));

            _validatorLocatorMock
                .Setup(m => m.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorFailureMock.Object);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.CreateGroupAsync(new Group()
            {
                Type = GroupType.Basic
            },  Guid.NewGuid(), Guid.NewGuid(), false));
        }

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
            Assert.True(result);
        }

        [Fact]
        public async Task OnlySuperAdminsCanCreateLockedGroups()
        {
            _groupRepositoryMock.Setup(m => m.CreateItemAsync(It.IsAny<Group>()))
                .ReturnsAsync(new Group());

            await _controller.CreateGroupAsync(new Group { IsLocked = true }, Guid.NewGuid(), Guid.NewGuid(), false);

            _groupRepositoryMock
                .Verify(x => x.CreateItemAsync(It.Is<Group>(g => g.IsLocked == false)));
        }

        [Fact]
        public async Task LockedGroupIsNotDeletedIfUserIsNotSuperAdmin()
        {
            _superadminServiceMock
                .Setup(x => x.IsSuperAdminAsync(It.IsAny<Guid>()))
                .ReturnsAsync(false);

            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new Group() { IsLocked = true });

            var result = await _controller.DeleteGroupAsync(Guid.NewGuid(), Guid.NewGuid());

            _groupRepositoryMock
                .Verify(x => x.DeleteItemAsync(It.IsAny<Guid>()), Times.Never);

            Assert.False(result);
        }

        [Fact]
        public async Task GetGroupByIdReturnsGroupIfExists()
        {
            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new Group
                {
                    TenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3")
                });

            var groupId = Guid.NewGuid();
            var result = await _controller.GetGroupByIdAsync(groupId);

            Assert.IsType<Group>(result);
        }

        [Fact]
        public async Task GetGroupByIdThrowsNotFoundExceptionIfGroupDoesNotExist()
        {
            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(default(Group));

            var groupId = Guid.NewGuid();
            await Assert.ThrowsAsync<NotFoundException>(() => _controller.GetGroupByIdAsync(groupId));
        }

        [Fact]
        public async Task GetGroupByIdThrowsValidationException()
        {
            var errors = Enumerable.Empty<ValidationFailure>();
            _groupRepositoryMock.Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(errors));

            var groupId = Guid.NewGuid();
            await Assert.ThrowsAsync<ValidationFailedException>(() => _controller.GetGroupByIdAsync(groupId));
        }

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
                    return Task.FromResult(items);
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
                    return Task.FromResult(items);
                });

            var result = await _controller.GetGroupsForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>());

            Assert.Empty(result);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupAsyncReturnstrueIfDocumentNotFound()
        {
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            var userId = Guid.NewGuid();

            var group = new Group
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
        public async Task UpdateGroupAsyncReturnsTrueIfSuccessful()
        {
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            var userId = Guid.NewGuid();

            var group = new Group
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
            Assert.Equal(group, result);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupByIdThrowsValidationException()
        {
            var tenantId = Guid.Parse("dbae315b-6abf-4a8b-886e-c9cc0e1d16b3");
            var userId = Guid.NewGuid();

            var group = new Group
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
    }
}
