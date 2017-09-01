using Synthesis.PrincipalService.Dao.Models;
using System;
using System.Threading.Tasks;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public interface IUsersController
    {
        Task<UserResponse> CreateUserAsync(UserRequest model, Guid tenantId);

        Task<User> GetUserAsync(Guid userId);

        Task<User> UpdateUserAsync(Guid userId, User model);

        Task DeleteUserAsync(Guid userId);
    }
}
