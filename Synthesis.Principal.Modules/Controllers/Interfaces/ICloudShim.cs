using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService.Controllers
{
    public interface ICloudShim
    {
        Task<MicroserviceResponse<bool>> ValidateSettingProfileId(Guid tenantId, Guid settingProfileId);

        Task<MicroserviceResponse<bool>> CopyMachineSettings(Guid machineId);
    }
}
