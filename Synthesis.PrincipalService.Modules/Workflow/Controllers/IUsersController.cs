using Synthesis.PrincipalService.Dao.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public interface IUsersController
    {
        Task<UserResponse> CreateUserAsync(CreateUserRequest model, Guid tenantId);

        Task<User> GetUserAsync(Guid userId);

        Task<User> UpdateUserAsync(Guid userId, User model);

        Task DeleteUserAsync(Guid userId);

        Task<IEnumerable<UserResponse>> GetUsersForAccount(GetUsersParams getUsersParams, Guid tenantId);
    }
}
