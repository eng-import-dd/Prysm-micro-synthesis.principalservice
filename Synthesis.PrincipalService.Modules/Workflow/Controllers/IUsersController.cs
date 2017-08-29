using Synthesis.PrincipalService.Dao.Models;
using System;
using System.Threading.Tasks;

namespace Synthesis.PrincipalService.Workflow.Controllers
{
    public interface IUsersController
    {
        Task<User> CreateUserAsync(User model);

        Task<User> GetUserAsync(Guid userId);

        Task<User> UpdateUserAsync(Guid userId, User model);

        Task DeleteUserAsync(Guid userId);
    }
}
