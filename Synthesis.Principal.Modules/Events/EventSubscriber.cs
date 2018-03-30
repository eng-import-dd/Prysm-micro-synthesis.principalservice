using Synthesis.EventBus;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.EventHandlers;
using Synthesis.TenantService.InternalApi.Models;

namespace Synthesis.PrincipalService.Events
{
    public class EventSubscriber
    {
        public EventSubscriber(IEventHandlerLocator eventHandlerLocator)
        {
            eventHandlerLocator
                .SubscribeEventHandler<TenantCreatedHandler, Tenant>(EventNamespaces.TenantService, EventNames.TenantCreated);
        }
    }
}
