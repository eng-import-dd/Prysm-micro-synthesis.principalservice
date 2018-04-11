namespace Synthesis.PrincipalService.Services
{
    public interface IIndexRegistrar<in T>
    {
        void RegisterDatabaseIndexes(T dbContext);
    }
}