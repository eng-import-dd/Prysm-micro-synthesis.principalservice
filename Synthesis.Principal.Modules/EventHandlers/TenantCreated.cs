using System.Threading.Tasks;
using Synthesis.EventBus;
using Synthesis.PrincipalService.Controllers;
using Synthesis.TenantService.InternalApi.Models;

namespace Synthesis.PrincipalService.EventHandlers
{
    public class TenantCreatedHandler : IAsyncEventHandler<Tenant>
    {
        private readonly IGroupsController _policyChangesController;

        public TenantCreatedHandler(IGroupsController policyChangesController)
        {
            _policyChangesController = policyChangesController;
        }

        public async Task HandleEventAsync(Tenant tenant)
        {
            await _policyChangesController.CreateBuiltInGroupsAsync(tenant.Id.GetValueOrDefault());
        }
    }
}