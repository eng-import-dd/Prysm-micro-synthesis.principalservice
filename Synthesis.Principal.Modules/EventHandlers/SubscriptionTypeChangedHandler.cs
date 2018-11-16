using System.Threading.Tasks;
using Synthesis.EventBus;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.SubscriptionService.InternalApi.Models;

namespace Synthesis.PrincipalService.EventHandlers
{
    public class SubscriptionTypeChangedHandler : IAsyncEventHandler<SubscriptionTypeChangedEvent>
    {
        private readonly IGroupsController _groupsController;
        private readonly IEventService _eventService;

        public SubscriptionTypeChangedHandler(IGroupsController groupsController, IEventService eventService)
        {
            _groupsController = groupsController;
            _eventService = eventService;
        }

        /// <inheritdoc />
        public async Task HandleEventAsync(SubscriptionTypeChangedEvent payload)
        {
            if (payload.OldSubscriptionType == SubscriptionType.Trial && payload.NewSubscriptionType == SubscriptionType.Enterprise)
            {
                var tenantGroups = await _groupsController.GetGroupsForTenantAsync(payload.Id);

                foreach (var group in tenantGroups)
                {
                    if (group.Type == GroupType.TenantAdmin || group.Type == GroupType.Basic)
                    {
                        _eventService.Publish(EventNames.RefreshGroupPolicy, group);
                    }
                }
            }
        }
    }
}