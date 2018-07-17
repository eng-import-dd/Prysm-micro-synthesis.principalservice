using System;
using System.Threading.Tasks;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Extensions;
using Synthesis.TenantService.InternalApi.Models;

namespace Synthesis.PrincipalService.EventHandlers
{
    public class TenantCreatedHandler : IAsyncEventHandler<Tenant>
    {
        private readonly ILogger _logger;
        private readonly IGroupsController _policyChangesController;

        public TenantCreatedHandler(ILoggerFactory factory, IGroupsController policyChangesController)
        {
            _logger = factory.GetLogger(this);
            _policyChangesController = policyChangesController;
        }

        public async Task HandleEventAsync(Tenant tenant)
        {
           await _policyChangesController.CreateBuiltInGroupsAsync(tenant.Id.GetValueOrDefault());
        }
    }
}
