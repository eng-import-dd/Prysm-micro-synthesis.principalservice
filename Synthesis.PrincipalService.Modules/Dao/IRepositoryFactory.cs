
using Synthesis.Configuration;

namespace Synthesis.PrincipalService.Dao
{
    public interface IRepositoryFactory
    {
        IBaseRepository<T> CreateRepository<T>(IAppSettingsReader appSettingsReader) where T : class;
    }
}