using Synthesis.EventBus;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.SubscriptionService.InternalApi.Models;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.EventHandlers
{
    public class SubscriptionCreatedHandler : IAsyncEventHandler<Subscription>
    {
        private readonly IGroupsController _groupsController;
        private readonly IEventService _eventService;

        public SubscriptionCreatedHandler(IGroupsController groupsController, IEventService eventService)
        {
            _groupsController = groupsController;
            _eventService = eventService;
        }

        /// <inheritdoc />
        public async Task HandleEventAsync(Subscription payload)
        {
            if (payload.SubscriptionType == SubscriptionType.Trial)
            {
                var tenantGroups = await _groupsController.GetGroupsForTenantAsync(payload.Id.GetValueOrDefault());

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