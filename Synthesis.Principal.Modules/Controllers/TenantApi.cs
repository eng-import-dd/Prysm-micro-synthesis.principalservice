using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using Synthesis.Configuration;
using Synthesis.Configuration.Shared;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;

namespace Synthesis.PrincipalService.Controllers
{
    public class TenantApi : ITenantApi
    {
        private readonly IMicroserviceHttpClientResolver _microserviceHttpClientResolver;
        private readonly string _serviceUrl;

        public TenantApi(IMicroserviceHttpClientResolver microserviceHttpClientResolver, IAppSettingsReader appSettingsReader)
        {
            _microserviceHttpClientResolver = microserviceHttpClientResolver;
            _serviceUrl = appSettingsReader.GetValue<string>("Tenant.Url");
        }

        public Task<MicroserviceResponse<bool>> AddUserIdToTenantAsync(Guid tenantId, Guid userId)
        {

            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var route = string.Format(Routes.AddUserIdToTenantFormat,tenantId);
            return microserviceHttpClient.PostAsync<Guid,bool>(route, userId);
        }

        /// <inheritdoc />
        public Task<MicroserviceResponse<TenantDomain>> GetTenantDomainAsync(Guid tenantDomainId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var get = string.Format(Routes.GetTenantDomainFormat,tenantDomainId);
            return microserviceHttpClient.GetAsync<TenantDomain>($"{_serviceUrl}{get}");
        }

        /// <inheritdoc />
        public Task<MicroserviceResponse<List<Guid>>> GetTenantDomainIdsAsync(Guid tenantId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var get = string.Format(Routes.GetTenantDomainIdsFormat, tenantId);
            return microserviceHttpClient.GetAsync<List<Guid>>($"{_serviceUrl}{get}");
        }

        public Task<MicroserviceResponse<List<Guid?>>> GetTenantIdsByUserIdAsync(Guid userId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var get = string.Format(Routes.GetTenantIdsByUserIdFormat, userId);
            return microserviceHttpClient.GetAsync<List<Guid?>>($"{_serviceUrl}{get}");
        }

        public Task<MicroserviceResponse<List<Guid?>>> GetUserIdsByTenantIdAsync(Guid tenantId)
        {
            var microserviceHttpClient = _microserviceHttpClientResolver.Resolve();
            var get = string.Format(Routes.GetUserIdsByTenantIdFormat, tenantId);
            return microserviceHttpClient.GetAsync<List<Guid?>>($"{_serviceUrl}{get}");
        }

        private static class Routes
        {
            public static string GetTenantDomainFormat => "/v1/tenantsdomain/{0}";

            public static string GetTenantDomainIdsFormat => "/v1/tenantsdomain/domainIds/{0}";

            public static string GetUserIdsByTenantIdFormat => "/v1/tenants/{0}/userids";

            public static string GetTenantIdsByUserIdFormat => "/v1/users/{0}/tenantids";

            public static string AddUserIdToTenantFormat => "/v1/tenants/{0}/userids";
        }
    }
}
