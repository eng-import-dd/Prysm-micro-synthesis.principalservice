using Synthesis.EventBus;
using Synthesis.PrincipalService.EventHandlers;
using Synthesis.SubscriptionService.InternalApi.Models;
using Synthesis.TenantService.InternalApi.Models;
using Synthesis.TenantService.InternalApi;
using Synthesis.TenantService.InternalApi.Constants;

using SubscriptionEventNames = Synthesis.SubscriptionService.InternalApi.Constants.EventNames;
using SubscriptionServiceInfo = Synthesis.SubscriptionService.InternalApi.Constants.ServiceInformation;

namespace Synthesis.PrincipalService.Events
{
    public class EventSubscriber
    {
        public EventSubscriber(IEventHandlerLocator eventHandlerLocator)
        {
            eventHandlerLocator
                .SubscribeAsyncEventHandler<TenantCreatedHandler, Tenant>(ServiceInformation.ServiceName, EventNames.TenantCreated);

            eventHandlerLocator
                .SubscribeAsyncEventHandler<SubscriptionTypeChangedHandler, SubscriptionTypeChangedEvent>(SubscriptionServiceInfo.ServiceName, SubscriptionEventNames.SubscriptionTypeChanged);

            eventHandlerLocator
                .SubscribeAsyncEventHandler<SubscriptionCreatedHandler, Subscription>(SubscriptionServiceInfo.ServiceName, SubscriptionEventNames.SubscriptionCreated);
        }
    }
}