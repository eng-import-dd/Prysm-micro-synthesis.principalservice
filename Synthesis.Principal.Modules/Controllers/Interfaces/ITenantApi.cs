﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public interface ITenantApi
    {
        Task<MicroserviceResponse<TenantDomain>> GetTenantDomainAsync(Guid tenantDomainId);

        Task<MicroserviceResponse<List<Guid>>> GetTenantDomainIdsAsync(Guid tenantId);
    }
}
