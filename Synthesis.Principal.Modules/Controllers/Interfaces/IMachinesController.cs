using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IMachinesController
    {
        Task<Machine> CreateMachineAsync(Machine model, Guid tenantId, CancellationToken cancellationToken = default(CancellationToken));

        Task<Machine> GetMachineByIdAsync(Guid id, Guid? tenantId, CancellationToken cancellationToken = default(CancellationToken));

        Task<Machine> GetMachineByKeyAsync(string machineKey, Guid? tenantId, CancellationToken cancellationToken = default(CancellationToken));

        Task<Guid> GetMachineTenantIdAsync(Guid machineId, Guid? tenantId, CancellationToken cancellationToken = default(CancellationToken));

        Task<Machine> UpdateMachineAsync(Machine model, CancellationToken cancellationToken = default(CancellationToken));

        Task DeleteMachineAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default(CancellationToken));

        Task<Machine> ChangeMachineTenantAsync(ChangeMachineTenantRequest request, Guid? sourceTenantId, CancellationToken cancellationToken = default(CancellationToken));

        Task<List<Machine>> GetTenantMachinesAsync(Guid tenantId, CancellationToken cancellationToken = default(CancellationToken));
    }
}