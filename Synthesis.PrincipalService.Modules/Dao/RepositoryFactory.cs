
using Synthesis.Configuration;

namespace Synthesis.PrincipalService.Dao
{
    public class RepositoryFactory : IRepositoryFactory
    {
        public IBaseRepository<T> CreateRepository<T>(IAppSettingsReader appSettingsReader) where T : class
        {
            return new DocumentDbRepository<T>(appSettingsReader);
        }
    }
}