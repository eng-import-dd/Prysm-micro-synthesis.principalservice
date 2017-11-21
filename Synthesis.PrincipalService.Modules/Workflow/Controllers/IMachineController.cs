﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Requests;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public interface IMachineController
    {
        Task<MachineResponse> CreateMachineAsync(CreateMachineRequest model, Guid tenantId);
        Task<MachineResponse> GetMachineByIdAsync(Guid id, Guid tenantId);
        Task<MachineResponse> GetMachineByKeyAsync(string machineKey, Guid tenantId);
        Task<MachineResponse> UpdateMachineAsync(UpdateMachineRequest model, Guid tenantId);
        Task DeleteMachineAsync(Guid id, Guid tenantId);
        Task<MachineResponse> ChangeMachineAccountAsync(Guid machineId, Guid tenantId, Guid settingProfileId);
        Task<List<MachineResponse>> GetTenantMachinesAsync(Guid tenantId);
    }
}
