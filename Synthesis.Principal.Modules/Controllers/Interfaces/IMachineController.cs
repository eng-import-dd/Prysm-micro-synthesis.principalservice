using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IMachineController
    {
        Task<Machine> CreateMachineAsync(Machine model, Guid tenantId);
        Task<Machine> GetMachineByIdAsync(Guid id, Guid tenantId, bool isServiceCall);
        Task<Machine> GetMachineByKeyAsync(string machineKey, Guid tenantId, bool isServiceCall);
        Task<Machine> UpdateMachineAsync(Machine model, Guid tenantId, bool isServiceCall);
        Task DeleteMachineAsync(Guid id, Guid tenantId);
        Task<Machine> ChangeMachineTenantasync(Guid machineId, Guid tenantId, Guid settingProfileId);
        Task<List<Machine>> GetTenantMachinesAsync(Guid tenantId);
    }
}
