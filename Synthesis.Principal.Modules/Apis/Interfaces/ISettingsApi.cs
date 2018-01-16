using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Apis.Interfaces
{
    public interface ISettingsApi
    {
        Task<MicroserviceResponse<SettingsResponse>> GetSettingsAsync(Guid projectAccountId);
    }
}
