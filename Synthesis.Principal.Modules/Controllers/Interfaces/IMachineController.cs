using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IMachineController
    {
        Task<MachineResponse> CreateMachineAsync(CreateMachineRequest model, Guid tenantId);
        Task<MachineResponse> GetMachineByIdAsync(Guid id, Guid tenantId);
        Task<MachineResponse> UpdateMachineAsync(UpdateMachineRequest model, Guid tenantId);
        Task DeleteMachineAsync(Guid id, Guid tenantId);
        Task<MachineResponse> ChangeMachineAccountAsync(Guid machineId, Guid tenantId, Guid settingProfileId);
        Task<List<MachineResponse>> GetTenantMachinesAsync(Guid tenantId);
    }
}
