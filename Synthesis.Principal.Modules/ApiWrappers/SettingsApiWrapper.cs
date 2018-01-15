using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.ApiWrappers.Interfaces;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.ApiWrappers
{
    public class SettingsApiWrapper : BaseApiWrapper, ISettingsApiWrapper
    {
        public SettingsApiWrapper(IMicroserviceHttpClientResolver httpClient, string serviceUrl) : base(httpClient, serviceUrl)
        {
        }

        public async Task<MicroserviceResponse<SettingsResponse>> GetSettingsAsync(Guid userId)
        {
            return await HttpClient.GetAsync<SettingsResponse>($"{ServiceUrl}/v1/settings/user/{userId}");
        }
    }
}
