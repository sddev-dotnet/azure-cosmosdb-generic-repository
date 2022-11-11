using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB
{

    public abstract class BaseRepository<T> : IRepository<T> where T : IStorableEntity
    {

        protected BaseRepository(
                CosmosClient client,
                ILogger<BaseRepository<T>> log,
                IOptions<CosmosDbConfiguration> config,
                string collectionName = null,
                string databaseName = null,
                string partitionKey = null)
        {
            Log = log;
            DatabaseName = databaseName;
            Configuration = config.Value;

            if (string.IsNullOrEmpty(DatabaseName))
            {
                DatabaseName = config.Value.DefaultDatabaseName;
            }

            if (string.IsNullOrEmpty(CollectionName))
            {
                //If a Collection Name is not set during IOC registration, 
                //allow it to be passed into the constructor
                //and if its not, use the name of the object
                CollectionName = collectionName ?? typeof(T).Name;
            }

            if (string.IsNullOrEmpty(PartitionKey))
            {
                PartitionKey = partitionKey ?? "/PartitionKey"; //ItemType is the property on base storable entity that is stored as the class name always
            }
            Client = client.GetContainer(DatabaseName, CollectionName);
        }

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
        /// The Key that this collection is Partitioned by. Defaults to 
        /// </summary>
        protected virtual string PartitionKey { get; set; }

        /// <summary>
        /// The settings for Cosmos for this environment
        /// </summary>
        protected virtual CosmosDbConfiguration Configuration { get; set; }

        /// <summary>
        /// The connection to the Azure DocumentDB. This should be injected and configured in the Autofac Config class.
        /// </summary>
        public Container Client { get; set; }

        /// <summary>
        /// Retrieves a single instance of an object by ID. 
        /// </summary>
        /// <remarks>Null is returned if the object does not exist</remarks>
        /// <param name="id">The identifier for the document</param>
        /// <returns>the full object of type T that you are requesting or null if it doesn't exist</returns>
        public abstract Task<T> Get(Guid id, string partitionKey = null);

        public abstract Task<ISearchResult<T>> Get(Expression<Func<T, bool>> predicate, ISearchModel model);

        public abstract Task<ISearchResult<T>> Get(string predicate, ISearchModel model);

        public abstract Task<Guid> Create(T model);

        public abstract Task<Guid> Update(T model);
        public abstract Task<ISearchResult<T>> GetAll(Expression<Func<T, bool>> predicate, ISearchModel model);
        public abstract Task<T> FindOne(Expression<Func<T, bool>> predicate, string partitionKey = null, bool singleResult = false);

        /// <summary>
        /// Inserts or updates a document. 
        /// </summary>
        /// <remarks>Update will do a full replace of the document, so ensure the state of Model is what you want the record to look like in the database</remarks>
        /// <param name="model"></param>
        /// <returns></returns>
        public abstract Task<Guid> Upsert(T model);

        /// <summary>
        /// Deletes a document from the database
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public abstract Task Delete(Guid id, string partitionKey, bool force = false);



    }
}
