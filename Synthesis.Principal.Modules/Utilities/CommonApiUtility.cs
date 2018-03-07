using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Controllers;
using Synthesis.TenantService.InternalApi.Api;

namespace Synthesis.PrincipalService.Utilities
{
    public static class CommonApiUtility
    {
        public static async Task<List<string>> GetTenantDomains(ITenantDomainApi tenantDomainApi, Guid tenantId)
        {
            var domainList = new List<string>();
            var result = await tenantDomainApi.GetTenantDomainIdsAsync(tenantId);

            if (result.ResponseCode == HttpStatusCode.NotFound)
            {
                return new List<string>();
            }

            if (result.ResponseCode != HttpStatusCode.OK)
            {
                throw new Exception(result.ReasonPhrase);
            }

            if (result.Payload != null)
            {
                foreach (var domainId in result.Payload)
                {
                    var tenantDomain = await tenantDomainApi.GetTenantDomainByIdAsync(domainId);
                    if (tenantDomain.ResponseCode != HttpStatusCode.OK || tenantDomain.Payload == null)
                    {
                        throw new Exception(tenantDomain.ReasonPhrase);
                    }

                    domainList.Add(tenantDomain.Payload.Domain);
                }
            }

            return domainList;
        }
    }
}
