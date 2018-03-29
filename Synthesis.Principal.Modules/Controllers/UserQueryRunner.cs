using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Controllers
{
    public class UserQueryRunner : IQueryRunner<User>
    {
        public async Task<IBatchResult<User>> RunQuery(IQueryable<User> query)
        {
            return await query.GetNextBatchAsync();
        }

        public int Count(IQueryable<User> query)
        {
            return query.Count();
        }
    }
}