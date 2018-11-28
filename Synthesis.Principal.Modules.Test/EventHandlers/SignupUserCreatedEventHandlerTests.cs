using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.EventHandlers;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.TrialService.InternalApi.Models;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.EventHandlers
{
    public class SignupUserCreatedEventHandlerTests
    {
        private readonly Mock<IGroupsController> _groupsControllerMock = new Mock<IGroupsController>();
        private readonly Mock<IUsersController> _usersControllerMock = new Mock<IUsersController>();

        private readonly SignupUserCreatedEventHandler _target;

        public SignupUserCreatedEventHandlerTests()
        {
            _target = new SignupUserCreatedEventHandler(_groupsControllerMock.Object, _usersControllerMock.Object);
        }

        [Fact]
        public async Task HandleEventAsync_WhenGroupTypeTenantAdmin_UserGroupUpdated()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var payload = new TrialSignupUser { TenantId = tenantId, UserId = userId };
            var adminGroup = Group.Example();
            adminGroup.Type = GroupType.TenantAdmin;
            var groups = new List<Group> { adminGroup };
            _groupsControllerMock
                .Setup(x => x.GetGroupsForTenantAsync(It.IsAny<Guid>(), CancellationToken.None))
                .ReturnsAsync(groups);
            _usersControllerMock
                .Setup(x => x.CreateUserGroupAsync(It.IsAny<UserGroup>(), It.IsAny<Guid>()))
                .ReturnsAsync(It.IsAny<UserGroup>());

            await _target.HandleEventAsync(payload);

            _usersControllerMock.Verify(x => x.CreateUserGroupAsync(It.IsAny<UserGroup>(), userId), Times.Once);
        }

        [Fact]
        public async Task HandleEventAsync_WhenGroupTypeNotTenantAdmin_UserGroupNotUpdated()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var payload = new TrialSignupUser { TenantId = tenantId, UserId = userId };
            var adminGroup = Group.Example();
            var groups = new List<Group> { adminGroup };
            _groupsControllerMock
                .Setup(x => x.GetGroupsForTenantAsync(It.IsAny<Guid>(), CancellationToken.None))
                .ReturnsAsync(groups);
            _usersControllerMock
                .Setup(x => x.CreateUserGroupAsync(It.IsAny<UserGroup>(), It.IsAny<Guid>()))
                .ReturnsAsync(It.IsAny<UserGroup>());

            await _target.HandleEventAsync(payload);

            _usersControllerMock.Verify(x => x.CreateUserGroupAsync(It.IsAny<UserGroup>(), userId), Times.Never);
        }
    }
}
