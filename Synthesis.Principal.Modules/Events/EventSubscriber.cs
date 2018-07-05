using Synthesis.EventBus;
using Synthesis.PrincipalService.EventHandlers;
using Synthesis.TenantService.InternalApi.Models;
using Synthesis.TenantService.InternalApi;
using Synthesis.TenantService.InternalApi.Constants;

namespace Synthesis.PrincipalService.Events
{
    public class EventSubscriber
    {
        public EventSubscriber(IEventHandlerLocator eventHandlerLocator)
        {
            eventHandlerLocator
                .SubscribeAsyncEventHandler<TenantCreatedHandler, Tenant>(ServiceInformation.ServiceName, EventNames.TenantCreated);
        }
    }
}
