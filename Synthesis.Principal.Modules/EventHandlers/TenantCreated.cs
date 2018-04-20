using System;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Extensions;
using Synthesis.TenantService.InternalApi.Models;

namespace Synthesis.PrincipalService.EventHandlers
{
    public class TenantCreatedHandler : IEventHandler<Tenant>
    {
        private readonly ILogger _logger;
        private readonly IGroupsController _policyChangesController;

        public TenantCreatedHandler(ILoggerFactory factory, IGroupsController policyChangesController)
        {
            _logger = factory.GetLogger(this);
            _policyChangesController = policyChangesController;
        }

        public async void HandleEvent(Tenant tenant)
        {
            try
            {
                await _policyChangesController.CreateBuiltInGroupsAsync(tenant.Id.ToGuid());
            }
            catch (Exception ex)
            {
                _logger.Error($"BuiltIn groups were not created for tenant {tenant.Id}", ex);
            }
        }
    }
}
