using Synthesis.PrincipalService.Dao.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public interface IUserInvitesController
    {
        Task<List<UserInviteResponse>> CreateUserInviteListAsync(List<UserInviteRequest> userInviteList, Guid tenantId);
    }
}
