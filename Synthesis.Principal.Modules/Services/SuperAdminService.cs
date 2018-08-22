using System;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.Threading.Tasks;

namespace Synthesis.PrincipalService.Services
{
    public class SuperAdminService : ISuperAdminService
    {
        private readonly AsyncLazy<IRepository<User>> _userRepositoryAsyncLazy;

        public SuperAdminService(IRepositoryFactory repositoryFactory)
        {
            _userRepositoryAsyncLazy = new AsyncLazy<IRepository<User>>(() => repositoryFactory.CreateRepositoryAsync<User>());
        }

        public async Task<bool> IsSuperAdminAsync(Guid userId)
        {
            var userRepository = await _userRepositoryAsyncLazy;
            var user = await userRepository.GetItemAsync(userId);

            if (user?.Groups == null)
            {
                return false;
            }

            return user.Groups != null && user.Groups.Contains(GroupIds.SuperAdminGroupId);
        }

        public async Task<bool> IsLastRemainingSuperAdminAsync(Guid userId)
        {
            var userRepository = await _userRepositoryAsyncLazy;
            var result = !await userRepository.CreateItemQuery().AnyAsync(u =>
                u.Id != userId &&
                u.IsLocked == false &&
                u.Groups != null &&
                u.Groups.Contains(GroupIds.SuperAdminGroupId));

            return result;
        }

        public bool IsSuperAdminGroup(Guid groupId)
        {
            return groupId == GroupIds.SuperAdminGroupId;
        }
    }
}