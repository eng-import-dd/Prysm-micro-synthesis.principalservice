using System;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IPrincipalsController
    {
        Task<Principal> CreatePrincipalAsync(Principal model);

        Task<Principal> GetPrincipalAsync(Guid principalId);

        Task<Principal> UpdatePrincipalAsync(Guid principalId, Principal model);

        Task DeletePrincipalAsync(Guid principalId);
    }
}
