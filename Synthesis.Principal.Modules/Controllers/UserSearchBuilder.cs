using System;
using System.Collections.Generic;
using System.Linq;
using Synthesis.DocumentStorage;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public class UserSearchBuilder : IUserSearchBuilder
    {
        private readonly IRepository<User> _userRepository;

        public UserSearchBuilder(IRepositoryFactory repositoryFactor)
        {
            _userRepository = repositoryFactor.CreateRepository<User>();
        }

        public IQueryable<User> BuildSearchQuery(Guid? currentUserId, List<Guid> userIds, UserSearchOptions searchOptions)
        {
            //var batchOptions = new BatchOptions
            //{
            //    BatchSize = searchOptions.PageSize,
            //    ContinuationToken = searchOptions.ContinuationToken
            //};

            var query = _userRepository.CreateItemQuery();
            query = BuildWhereClause(currentUserId, userIds, searchOptions, query);
            var batch = BuildOrderByClause(searchOptions, query);
            return batch;
        }

        private IQueryable<User> BuildWhereClause(Guid? currentUserId, List<Guid> userIds, UserSearchOptions searchOptions, IQueryable<User> query)
        {
            query = query.Where(user => userIds.Contains(user.Id ?? Guid.Empty));

            if (searchOptions.OnlyCurrentUser)
            {
                query = query.Where(user => userIds.Contains(currentUserId ?? Guid.Empty));
            }

            if (!searchOptions.IncludeInactive)
            {
                query = query.Where(user => user.IsLocked == false);
            }
            switch (searchOptions.IdpFilter)
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

            if (!string.IsNullOrEmpty(searchOptions.SearchValue))
            {
                var searchValue = searchOptions.SearchValue.ToLower();
                query = query.Where(
                    user => (user.FirstName + " " + user.LastName).ToLower().Contains(searchValue)
                        || user.Email.ToLower().Contains(searchValue)
                        || user.Username.ToLower().Contains(searchValue));
            }

            return query;
        }

        private IQueryable<User> BuildOrderByClause(UserSearchOptions searchOptions, IQueryable<User> query)
        {
            // TODO: See CU-568 - Define an index for each of these attributes
            return query;
            //switch (searchOptions.SortColumn?.ToLower())
            //{
            //    case "lastname":
            //        return searchOptions.SortDescending ? query.OrderByDescending(x => x.LastName) : query.OrderBy(x => x.LastName);
            //    case "email":
            //        return searchOptions.SortDescending ? query.OrderByDescending(x => x.Email) : query.OrderBy(x => x.Email);
            //    case "username":
            //        return searchOptions.SortDescending ? query.OrderByDescending(x => x.Username) : query.OrderBy(x => x.Username);
            //    case "firstname":
            //    default:
            //        return searchOptions.SortDescending ? query.OrderByDescending(x => x.FirstName) : query.OrderBy(x => x.FirstName);
            //}
        }
    }
}