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
    public class SubscriptionCreatedHandlerTests
    {
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IGroupsController> _groupsControllerMock = new Mock<IGroupsController>();

        private readonly SubscriptionCreatedHandler _target;

        public SubscriptionCreatedHandlerTests()
        {
            _target = new SubscriptionCreatedHandler(_groupsControllerMock.Object, _eventServiceMock.Object);
        }

        [Fact]
        public async Task HandleEventAsync_ForTrialSubscription_PublishesRefreshGroupPolicyEvent()
        {
            var subscription = new Subscription() { SubscriptionType = SubscriptionType.Trial };
            var groups = new List<Group>() { new Group() { Type = GroupType.Basic }, new Group() { Type = GroupType.TenantAdmin }, new Group() { Type = GroupType.Custom } };

            _groupsControllerMock.Setup(x => x.GetGroupsForTenantAsync(It.IsAny<Guid>(), default(CancellationToken))).ReturnsAsync(groups.AsEnumerable());

            await _target.HandleEventAsync(subscription);

            _eventServiceMock.Verify(s => s.PublishAsync(It.Is<ServiceBusEvent<Group>>(e => e.Payload.Type == GroupType.TenantAdmin || e.Payload.Type == GroupType.Basic)), Times.Exactly(2));
        }

        [Fact]
        public async Task HandleEventAsync_ForEnterpriseSubscription_DoesNotPublishRefreshGroupPolicyEvent()
        {
            var subscription = new Subscription() { SubscriptionType = SubscriptionType.Enterprise };
            var groups = new List<Group>() { new Group() { Type = GroupType.Basic }, new Group() { Type = GroupType.TenantAdmin }, new Group() { Type = GroupType.Custom } };

            _groupsControllerMock.Setup(x => x.GetGroupsForTenantAsync(It.IsAny<Guid>(), default(CancellationToken))).ReturnsAsync(groups.AsEnumerable());

            await _target.HandleEventAsync(subscription);

            _eventServiceMock.Verify(s => s.PublishAsync(It.IsAny<ServiceBusEvent<Group>>()), Times.Never);
        }
    }
}