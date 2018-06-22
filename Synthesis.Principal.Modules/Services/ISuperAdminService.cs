using System;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Services
{
    public interface ISuperAdminService
    {
        Task<bool> IsSuperAdminAsync(Guid userId);
        Task<bool> IsLastRemainingSuperAdminAsync(Guid userId);
    }
}