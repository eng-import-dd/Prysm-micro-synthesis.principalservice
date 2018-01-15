using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.ApiWrappers.Interfaces
{
    public interface ISettingsApiWrapper
    {
        Task<MicroserviceResponse<SettingsResponse>> GetSettingsAsync(Guid projectAccountId);
    }
}
