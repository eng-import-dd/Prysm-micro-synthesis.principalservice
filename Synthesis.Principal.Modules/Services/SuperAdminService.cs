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

        public async Task<bool> IsLastRemainingSuperAdminAsync(Guid userId)
        {
            var result = !await _userRepository.CreateItemQuery().AnyAsync(u =>
                u.Id != userId &&
                u.IsLocked == false &&
                u.Groups != null &&
                u.Groups.Contains(GroupIds.SuperAdminGroupId));

            return result;
        }
    }
}