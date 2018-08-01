using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IMachineController
    {
        Task<Machine> CreateMachineAsync(Machine model);
        Task<Machine> GetMachineByIdAsync(Guid machineId);
        Task<Machine> GetMachineByKeyAsync(string machineKey);
        Task<Machine> UpdateMachineAsync(Machine model);
        Task DeleteMachineAsync(Guid id);
        Task<Machine> ChangeMachineTenantasync(Guid machineId, Guid tenantId, Guid settingProfileId);
        Task<List<Machine>> GetTenantMachinesAsync(Guid tenantId);
    }
}
