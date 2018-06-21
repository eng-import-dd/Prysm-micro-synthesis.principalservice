using System;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Services
{
    public class SuperAdminService : ISuperAdminService
    {
        private readonly IRepository<User> _userRepository;

        public SuperAdminService(IRepositoryFactory repositoryFactory)
        {
            _userRepository = repositoryFactory.CreateRepository<User>();
        }

        public async Task<bool> IsSuperAdminAsync(Guid userId)
        {
            var user = await _userRepository.GetItemAsync(userId);

            if (user?.Groups == null)
            {
                return false;
            }

            return user.Groups != null && user.Groups.Contains(GroupIds.SuperAdminGroupId);
        }

        public async Task<bool> UserIsLastSuperAdminAsync(Guid userId)
        {
            if (!await IsSuperAdminAsync(userId))
            {
                return false;
            }

            var items = await _userRepository.GetItemsAsync(u =>
                u.Id != userId &&
                u.Groups != null && u.Groups.Contains(GroupIds.SuperAdminGroupId));

            return !items.Any();
        }
    }
}