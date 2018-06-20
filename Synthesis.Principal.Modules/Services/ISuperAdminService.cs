using System;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Services
{
    public interface ISuperAdminService
    {
        Task<bool> IsSuperAdminAsync(Guid userId);
        Task<bool> UserIsLastSuperAdminAsync(Guid userId);
    }
}