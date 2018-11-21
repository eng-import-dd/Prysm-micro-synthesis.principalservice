using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Synthesis.EventBus;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.EventHandlers;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.SubscriptionService.InternalApi.Models;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.EventHandlers
{
    public class SubscriptionTypeChangedHandlerTests
    {
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IGroupsController> _groupsControllerMock = new Mock<IGroupsController>();

        private readonly SubscriptionTypeChangedHandler _target;

        public SubscriptionTypeChangedHandlerTests()
        {
            _target = new SubscriptionTypeChangedHandler(_groupsControllerMock.Object, _eventServiceMock.Object);
        }

        [Fact]
        public async Task HandleEventAsync_ForTrialSubscription_PublishesRefreshGroupPolicyEvent()
        {
            var evt = new SubscriptionTypeChangedEvent { OldSubscriptionType = SubscriptionType.Trial, NewSubscriptionType = SubscriptionType.Enterprise };
            var groups = new List<Group>() { new Group() { Type = GroupType.Basic }, new Group() { Type = GroupType.TenantAdmin }, new Group() { Type = GroupType.Custom } };

            _groupsControllerMock.Setup(x => x.GetGroupsForTenantAsync(It.IsAny<Guid>(), default(CancellationToken))).ReturnsAsync(groups.AsEnumerable());

            await _target.HandleEventAsync(evt);

            _eventServiceMock.Verify(s => s.PublishAsync(It.IsAny<ServiceBusEvent<Group>>()), Times.Exactly(2));
        }

        [Fact]
        public async Task HandleEventAsync_ForEnterpriseSubscription_DoesNotPublishRefreshGroupPolicyEvent()
        {
            var evt = new SubscriptionTypeChangedEvent { OldSubscriptionType = SubscriptionType.Enterprise, NewSubscriptionType = SubscriptionType.Trial };
            var groups = new List<Group>() { new Group() { Type = GroupType.Basic }, new Group() { Type = GroupType.TenantAdmin }, new Group() { Type = GroupType.Custom } };

            _groupsControllerMock.Setup(x => x.GetGroupsForTenantAsync(It.IsAny<Guid>(), default(CancellationToken))).ReturnsAsync(groups.AsEnumerable());

            await _target.HandleEventAsync(evt);

            _eventServiceMock.Verify(s => s.PublishAsync(It.IsAny<ServiceBusEvent<Group>>()), Times.Never);
        }
    }
}