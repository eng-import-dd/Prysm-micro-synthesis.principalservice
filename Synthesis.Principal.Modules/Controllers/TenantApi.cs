﻿using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    public class TenantApi : ITenantApi
    {
        private readonly IMicroserviceHttpClientResolver _microserviceHttpClientResolver;
        private readonly string _serviceUrl;

        public TenantApi(IMicroserviceHttpClientResolver microserviceHttpClientResolver)
        {
            _microserviceHttpClientResolver = microserviceHttpClientResolver;
            _serviceUrl = ConfigurationManager.AppSettings["TenantService.Url"];
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

        private static class Routes
        {
            public static string GetTenantDomainFormat => "/v1/tenantsdomain/{0}";

            public static string GetTenantDomainIdsFormat => "/v1/tenantsdomain/domainIds/{0}";
        }
    }
}