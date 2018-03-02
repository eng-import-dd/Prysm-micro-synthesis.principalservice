using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using UserInvite = Synthesis.PrincipalService.InternalApi.Models.UserInvite;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IUserInvitesController
    {
        Task<List<UserInvite>> CreateUserInviteListAsync(List<UserInvite> userInviteList, Guid tenantId);
        Task<List<UserInvite>> ResendEmailInviteAsync(List<UserInvite> userInviteList, Guid tenantId);

        Task<PagingMetadata<UserInvite>> GetUsersInvitedForTenantAsync(Guid tenantId, bool allUsers);
    }
}
