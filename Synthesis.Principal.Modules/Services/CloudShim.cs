using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.Configuration;
using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService.Services
{
    public class CloudShim : ICloudShim
    {
        private readonly IMicroserviceHttpClientResolver _microserviceHttpClientResolver;
        private readonly string _serviceUrl;

        public CloudShim(IMicroserviceHttpClientResolver microserviceHttpClientResolver, IAppSettingsReader appSettingsReader)
        {
            _microserviceHttpClientResolver = microserviceHttpClientResolver;
            _serviceUrl = appSettingsReader.GetValue<string>("SynthesisCloud.Url");
        }

        public Task<MicroserviceResponse<bool>> ValidateSettingProfileId(Guid tenantId, Guid settingProfileId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var get = string.Format(Routes.ValidateSettingProfileId, tenantId, settingProfileId);
            return microserviceHttpClient.GetAsync<bool>($"{_serviceUrl}{get}");
        }

        public async Task<MicroserviceResponse<bool>> CopyMachineSettings(Guid machineId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var post = string.Format(Routes.CopyMachineSettings, machineId);
            var result = await microserviceHttpClient.PostAsync($"{_serviceUrl}{post}", true);
            return result;
        }

        public Task<MicroserviceResponse<IEnumerable<Guid>>> GetSettingProfileIdsForTenant(Guid tenantId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var get = string.Format(Routes.GetSettingProfileIdsForAccount, tenantId);
            return microserviceHttpClient.GetAsync<IEnumerable<Guid>>($"{_serviceUrl}{get}");
        }

        private static class Routes
        {
            public static string ValidateSettingProfileId => "/api/v1/settings/{0}/{1}/validate";

            public static string CopyMachineSettings => "/api/v1/settings/{0}/copyMachineSettings";

            public static string GetSettingProfileIdsForAccount => "/api/v1/accounts/{0}/settingprofileids";
        }
    }
}