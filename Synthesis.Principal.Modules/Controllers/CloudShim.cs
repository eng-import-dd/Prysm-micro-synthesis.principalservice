using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.PrincipalService.Controllers
{
    public class CloudShim : ICloudShim
    {
        private readonly IMicroserviceHttpClientResolver _microserviceHttpClientResolver;
        private readonly string _serviceUrl;

        public CloudShim(IMicroserviceHttpClientResolver microserviceHttpClientResolver)
        {
            _microserviceHttpClientResolver = microserviceHttpClientResolver;
            _serviceUrl = "http://localhost:8090";
        }

        public Task<MicroserviceResponse<bool>> ValidateSettingProfileId(Guid tenantId, Guid settingProfileId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var get = string.Format(Routes.ValidateSettingProfileId, tenantId, settingProfileId);
            return microserviceHttpClient.GetAsync<bool>($"{_serviceUrl}{get}");
        }

        public Task<MicroserviceResponse<IEnumerable<Guid>>> GetSettingProfileIdsForTenant(Guid tenantId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var get = string.Format(Routes.GetSettingProfileIdsForAccount, tenantId);
            return microserviceHttpClient.GetAsync<IEnumerable<Guid>>($"{_serviceUrl}{get}");
        }

        public async Task<MicroserviceResponse<bool>> CopyMachineSettings(Guid machineId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var post = string.Format(Routes.CopyMachineSettings, machineId);
            bool dummy = true;
            var result = await microserviceHttpClient.PostAsync($"{_serviceUrl}{post}", dummy);
            return result;
        }

        private static class Routes
        {
            public static string ValidateSettingProfileId => "/api/v1/settings/{0}/{1}/validate";

            public static string GetSettingProfileIdsForAccount => "/api/v1/accounts/{0}/settingprofileids";

            public static string CopyMachineSettings => "/api/v1/settings/{0}/copyMachineSettings";
        }
    }
}