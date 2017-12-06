using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    public class TenantApi : ITenantApi
    {
        private readonly IMicroserviceHttpClient _microserviceHttpClient;
        private readonly string _serviceUrl;

        public TenantApi(IMicroserviceHttpClient microserviceHttpClient)
        {
            _microserviceHttpClient = microserviceHttpClient;
            _serviceUrl = ConfigurationManager.AppSettings["TenantService.Url"];
        }
        /// <inheritdoc />
        public Task<MicroserviceResponse<TenantDomain>> GetTenantDomainAsync(Guid tenantDomainId)
        {
            var get = string.Format(Routes.GetTenantDomainFormat,tenantDomainId);
            return _microserviceHttpClient.GetAsync<TenantDomain>($"{_serviceUrl}{get}");
        }

        /// <inheritdoc />
        public Task<MicroserviceResponse<List<Guid>>> GetTenantDomainIdsAsync(Guid tenantId)
        {
            var get = string.Format(Routes.GetTenantDomainIdsFormat, tenantId);
            return _microserviceHttpClient.GetAsync<List<Guid>>($"{_serviceUrl}{get}");
        }

        private static class Routes
        {
            public static string GetTenantDomainFormat => "/v1/tenantsdomain/{0}";

            public static string GetTenantDomainIdsFormat => "/v1/tenantsdomain/domainIds/{0}";
        }
    }
}
