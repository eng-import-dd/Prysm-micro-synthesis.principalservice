using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService.Services
{
    public interface ICloudShim
    {
        Task<MicroserviceResponse<bool>> ValidateSettingProfileId(Guid tenantId, Guid settingProfileId);

        Task<MicroserviceResponse<bool>> CopyMachineSettings(Guid machineId);

        Task<MicroserviceResponse<IEnumerable<Guid>>> GetSettingProfileIdsForTenant(Guid tenantId);
    }
}