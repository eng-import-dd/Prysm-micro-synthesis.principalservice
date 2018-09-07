using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.Http.Microservice;
using Synthesis.Http.Microservice.Constants;
using Synthesis.Logging;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Enumerations;
using Synthesis.Threading.Tasks;

namespace Synthesis.PrincipalService.Controllers
{
    public class TenantUserSearchBuilder : ITenantUserSearchBuilder
    {
        private readonly AsyncLazy<IRepository<User>> _userRepositoryAsyncLazy;
        private readonly IProjectAccessApi _projectApi;
        private readonly ILogger _logger;

        public TenantUserSearchBuilder(IRepositoryFactory repositoryFactory, IProjectAccessApi projectApi, ILogger logger)
        {
            _userRepositoryAsyncLazy = new AsyncLazy<IRepository<User>>(() => repositoryFactory.CreateRepositoryAsync<User>());
            _projectApi = projectApi;
            _logger = logger;
        }

        public async Task<IQueryable<User>> BuildSearchQueryAsync(Guid? currentUserId, List<Guid> userIds, UserFilteringOptions filteringOptions, Guid tenantId)
        {
            var userRepository = await _userRepositoryAsyncLazy;
            var query = userRepository.CreateItemQuery(UsersController.DefaultBatchOptions);
            query = await BuildWhereClauseAsync(query, currentUserId, userIds, filteringOptions, tenantId);
            var batch = BuildOrderByClause(filteringOptions, query);
            return batch;
        }

        private async Task<IQueryable<User>> BuildWhereClauseAsync(IQueryable<User> query, Guid? currentUserId, List<Guid> userIds, UserFilteringOptions filteringOptions, Guid tenantId)
        {
            query = query.Where(user => userIds.Contains(user.Id ?? Guid.Empty));

            if (filteringOptions.OnlyCurrentUser && currentUserId.HasValue)
            {
                query = query.Where(user => userIds.Contains(currentUserId.Value));
            }

            if (!filteringOptions.IncludeInactive)
            {
                query = query.Where(user => user.IsLocked == false);
            }

            switch (filteringOptions.IdpFilter)
            {
                case IdpFilter.IdpUsers:
                    query = query.Where(user => user.IsIdpUser == true);
                    break;

                case IdpFilter.LocalUsers:
                    query = query.Where(user => user.IsIdpUser == false);
                    break;

                case IdpFilter.NotSet:
                    query = query.Where(user => user.IsIdpUser == null);
                    break;
            }

            switch (filteringOptions.GroupingType)
            {
                case UserGroupingType.Project when !filteringOptions.UserGroupingId.Equals(Guid.Empty):
                    query = await AddFilterByProjectToQuery(query, filteringOptions, tenantId);
                    break;

                case UserGroupingType.Group when !filteringOptions.UserGroupingId.Equals(Guid.Empty):
                    query = filteringOptions.ExcludeUsersInGroup ?
                        query.Where(user => !user.Groups.Contains(filteringOptions.UserGroupingId)) :
                        query.Where(user => user.Groups.Contains(filteringOptions.UserGroupingId));
                    break;

                case UserGroupingType.None:
                    break;
            }

            if (string.IsNullOrEmpty(filteringOptions.SearchValue))
            {
                return query;
            }

            var searchValue = filteringOptions.SearchValue.ToLower();
            query = query.Where(
                user => (user.FirstName + " " + user.LastName).ToLower().Contains(searchValue)
                    || user.Email.ToLower().Contains(searchValue)
                    || user.Username.ToLower().Contains(searchValue));

            return query;
        }

        private async Task<IQueryable<User>> AddFilterByProjectToQuery(IQueryable<User> query, UserFilteringOptions userFilteringOptions, Guid tenantId)
        {
            var tenantHeader = HeaderKeys.CreateTenantHeaderKey(tenantId);
            var requestHeaders = new List<KeyValuePair<string, string>> { tenantHeader };

            var projectUserIdsResponse = await _projectApi.GetProjectMemberUserIdsAsync(userFilteringOptions.UserGroupingId, MemberRoleFilter.FullUser, requestHeaders);
            if (!projectUserIdsResponse.IsSuccess() || projectUserIdsResponse.Payload == null)
            {
                _logger.Error($"Could not find members for user grouping id: {userFilteringOptions.UserGroupingId}");
                return query;
            }

            var usersInProject = projectUserIdsResponse.Payload.ToList();

            query = userFilteringOptions.ExcludeUsersInGroup ?
                query.Where(user => !usersInProject.Contains(user.Id ?? Guid.Empty)) :
                query.Where(user => usersInProject.Contains(user.Id ?? Guid.Empty));

            return query;
        }

        private static IQueryable<User> BuildOrderByClause(UserFilteringOptions filteringOptions, IQueryable<User> query)
        {
            switch (filteringOptions.SortColumn?.ToLower())
            {
                case "lastname":
                    return ApplyOrderByClause(query, x => x.LastName, filteringOptions.SortDescending);

                case "email":
                    return ApplyOrderByClause(query, x => x.Email, filteringOptions.SortDescending);

                case "username":
                    return ApplyOrderByClause(query, x => x.Username, filteringOptions.SortDescending);

                case "firstname":
                    return ApplyOrderByClause(query, x => x.FirstName, filteringOptions.SortDescending);

                default:
                    return query;
            }
        }

        private static IQueryable<User> ApplyOrderByClause(IQueryable<User> query, Expression<Func<User, string>> keySelector, bool descending)
        {
            return descending
                ? query.OrderByDescending(keySelector)
                : query.OrderBy(keySelector);
        }
    }
}