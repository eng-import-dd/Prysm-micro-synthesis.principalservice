using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Synthesis.Configuration;

namespace Synthesis.PrincipalService.Dao
{
    public class DocumentDbRepository<T> : IBaseRepository<T> where T : class
    {
        private readonly DocumentClient _client;
        private readonly string _collectionName;
        private readonly string _databaseName;

        public DocumentDbRepository(IAppSettingsReader appSettingsReader)
        {
            _databaseName = appSettingsReader.GetValue<string>("DocumentDb.DatabaseId");
            var typeParameterType = typeof(T);
            _collectionName = typeParameterType.Name.ToLower(); // Collection name based on Model Name
            _client = new DocumentClient(new Uri(appSettingsReader.GetValue<string>("DocumentDb.endpoint")), appSettingsReader.GetValue<string>("DocumentDb.authKey"), new ConnectionPolicy { EnableEndpointDiscovery = false });
            Initialize();
        }

        public async Task<T> CreateItemAsync(T item)
        {
            Document result = await _client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(_databaseName, _collectionName), item, null, true);
            return (T)(dynamic)result;
        }

        public async Task DeleteItemAsync(string id)
        {
            await _client.DeleteDocumentAsync(UriFactory.CreateDocumentUri(_databaseName, _collectionName, id));
        }

        public async Task<T> GetItemAsync(string id)
        {
            Document document = await _client.ReadDocumentAsync(UriFactory.CreateDocumentUri(_databaseName, _collectionName, id));
            return (T)(dynamic)document;
        }

        public async Task<IEnumerable<T>> GetItemsAsync(Expression<Func<T, bool>> predicate)
        {
            var query = _client.CreateDocumentQuery<T>(
                                                       UriFactory.CreateDocumentCollectionUri(_databaseName, _collectionName),
                                                       new FeedOptions { MaxItemCount = -1 })
                               .Where(predicate)
                               .AsDocumentQuery();

            var results = new List<T>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<T>());
            }

            return results;
        }

        public async Task<T> UpdateItemAsync(string id, T item)
        {
            Document result = await _client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(_databaseName, _collectionName, id), item);
            return (T)(dynamic)result;
        }

        private void Initialize()
        {
            CreateDatabaseIfNotExistsAsync().Wait();
            CreateCollectionIfNotExistsAsync().Wait();
        }

        private async Task CreateDatabaseIfNotExistsAsync()
        {
            try
            {
                await _client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(_databaseName));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    await _client.CreateDatabaseAsync(new Database { Id = _databaseName });
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task CreateCollectionIfNotExistsAsync()
        {
            try
            {
                await _client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(_databaseName, _collectionName));
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    await _client.CreateDocumentCollectionAsync(
                                                                UriFactory.CreateDatabaseUri(_databaseName),
                                                                new DocumentCollection { Id = _collectionName },
                                                                new RequestOptions { OfferThroughput = 1000 });
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
