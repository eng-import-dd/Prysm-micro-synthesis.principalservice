using System;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public interface IPrincipalserviceController
    {
        Task<PrincipalserviceResponse> GetPrincipalserviceAsync(Guid principalserviceId);
        Task<PrincipalserviceResponse> CreatePrincipalserviceAsync(Principalservice model);
        Task<PrincipalserviceResponse> UpdatePrincipalserviceAsync(Principalservice model);
        Task<PrincipalserviceDeleteResponse> DeletePrincipalserviceAsync(Guid principalserviceId);
    }
}
