using System.Collections.ObjectModel;
using Microsoft.Azure.Documents;
using Synthesis.DocumentStorage.DocumentDB;
using User = Synthesis.PrincipalService.InternalApi.Models.User;

namespace Synthesis.PrincipalService.Services
{
    public class DocumentDbIndexRegistrar : IIndexRegistrar<DocumentDbContext>
    {
        public void RegisterDatabaseIndexes(DocumentDbContext dbContext)
        {
            var indexingPolicy = new IndexingPolicy();
            AddRangeIndex(indexingPolicy, nameof(User.LastName), -1);
            AddRangeIndex(indexingPolicy, nameof(User.FirstName), -1);
            AddRangeIndex(indexingPolicy, nameof(User.Username), -1);
            AddRangeIndex(indexingPolicy, nameof(User.Email), -1);

            AddDefaultIndex(indexingPolicy);

            dbContext.RegisterIndexingPolicy<User>(indexingPolicy);
        }

        private void AddDefaultIndex(IndexingPolicy indexingPolicy)
        {
            indexingPolicy.IncludedPaths.Add(new IncludedPath
            {
                Path = "/*",
                Indexes = new Collection<Index> {
                    new HashIndex(DataType.String) { Precision = 3 },
                    new RangeIndex(DataType.Number) { Precision = -1 }
                }
            });
        }

        private void AddRangeIndex(IndexingPolicy indexingPolicy, string path, short? precision)
        {
            indexingPolicy.IncludedPaths.Add(new IncludedPath
            {
                Path = $"/{path}/?",
                Indexes = new Collection<Index>
                {
                    new RangeIndex(DataType.String) { Precision = precision }
                }
            });
        }
    }
}
