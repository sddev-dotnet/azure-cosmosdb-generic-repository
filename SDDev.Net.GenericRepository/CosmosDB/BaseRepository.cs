using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using SDDev.Net.GenericRepository.Contracts;
using Microsoft.Azure.Documents;
using System.Threading.Tasks;
using System.Linq.Expressions;
using SDDev.Net.GenericRepository.Contracts.Search;
using System.Linq;
using Microsoft.Azure.Documents.Client;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository;

namespace SDDev.Net.GenericRepository.CosmosDB
{

    public abstract class BaseRepository<T> : IRepository<T> where T : IStorableEntity
    {
        /// <summary>
        /// Logger for outputting messages within the repository
        /// </summary>
        protected ILogger Log { get; set; }

        /// <summary>
        /// The name of the database you want to connect to
        /// </summary>
        /// <remarks>This will be created if it doesn't exist</remarks>
        protected virtual string DatabaseName { get; set; }

        /// <summary>
        /// The document collection you want to connect to. 
        /// </summary>
        /// <remarks>This will be created if it doesn't exist</remarks>
        protected virtual string CollectionName { get; set; }


        /// <summary>
        /// The type for the items in the collection
        /// This is used to allow us to store multiple types of objects in the same collection
        /// </summary>
        protected virtual string ItemType => typeof(T).Name;

        /// <summary>
        /// The connection to the Azure DocumentDB. This should be injected and configured in the Autofac Config class.
        /// </summary>
        protected IDocumentClient Client { get; set; }

        /// <summary>
        /// Retrieves a single instance of an object by ID. 
        /// </summary>
        /// <remarks>Null is returned if the object does not exist</remarks>
        /// <param name="id">The identifier for the document</param>
        /// <returns>the full object of type T that you are requesting or null if it doesn't exist</returns>
        public abstract Task<T> Get(Guid id);

        public abstract Task<ISearchResult<T>> Get(Expression<Func<T, bool>> predicate, ISearchModel model);

        public abstract Task<ISearchResult<T>> Get(string predicate, ISearchModel model);


        /// <summary>
        /// Inserts or updates a document. 
        /// </summary>
        /// <remarks>Update will do a full replace of the document, so ensure the state of Model is what you want the record to look like in the database</remarks>
        /// <param name="model"></param>
        /// <returns></returns>
        public abstract Task<T> Upsert(T model);

        /// <summary>
        /// Deletes a document from the database
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public abstract Task Delete(Guid id);

        protected BaseRepository(IDocumentClient client, ILogger log)
        {
            Log = log;
            Client = client;
            Setup();
        }

        protected BaseRepository(IDocumentClient client, ILogger log, string dbName, string collectionName)
        {
            Log = log;
            Client = client;
            DatabaseName = dbName;
            CollectionName = collectionName;
            Setup();
        }

        protected async void Setup()
        {
            //need db created before we can create collection, have to await here
            var db = await CreateDatabaseIfNotExists(DatabaseName);
            var collection = await CreateCollectionIfNotExists(CollectionName);

        }

        protected async Task<Database> CreateDatabaseIfNotExists(string dbName)
        {
            Database db = null;
            db = Client.CreateDatabaseQuery().Where(d => d.Id == DatabaseName).AsEnumerable().FirstOrDefault();

            if (db == null)
                db = await Client.CreateDatabaseAsync(new Database() { Id = dbName });

            return db;
        }

        protected async Task<DocumentCollection> CreateCollectionIfNotExists(string collectionName, string databaseName = null)
        {
            DocumentCollection collection = null;
            databaseName = databaseName ?? DatabaseName;


            collection = Client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(DatabaseName)).Where(c => c.Id == collectionName).AsEnumerable().FirstOrDefault();

            if (collection == null)
            {
                Log.LogDebug($"Collection {collectionName} does not exist in database {databaseName}. Creating it.");
                var collectionEntity = new DocumentCollection()
                {
                    Id = collectionName,
                    IndexingPolicy = new IndexingPolicy(new RangeIndex(DataType.String)
                    {
                        Precision = 1
                    })
                };

                try
                {
                    collection = await Client.CreateDocumentCollectionAsync(UriFactory.CreateDatabaseUri(databaseName), collectionEntity, new RequestOptions()
                    {
                        OfferThroughput = 400
                    });
                }
                catch (DocumentClientException ex)
                {
                    Log.LogError("Could not create DocumentCollection. Fatal Error.", ex);
                    throw;
                }
            }



            return collection;
        }
    }
}
