using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;

namespace Synthesis.PrincipalService.Controllers
{
    public interface IQueryRunner<T>
    {
        Task<IBatchResult<T>> RunQuery(IQueryable<T> query);
        int Count(IQueryable<T> query);
    }
}