using System;
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

        Task<MicroserviceResponse<List<Guid?>>> GetTenantIdsByUserIdAsync(Guid userId);

        Task<MicroserviceResponse<List<Guid?>>> GetUserIdsByTenantIdAsync(Guid tenantId);

        Task<MicroserviceResponse<bool>> AddUserIdToTenantAsync(Guid tenantId, Guid userId);
    }
}
