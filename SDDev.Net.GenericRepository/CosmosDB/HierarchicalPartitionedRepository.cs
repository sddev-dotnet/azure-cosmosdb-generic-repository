using Azure;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB
{
    public class HierarchicalPartitionedRepository<TModel> : GenericRepository<TModel>, IHierarchicalPartitionRepository<TModel> where TModel : class, IStorableEntity
    {

        /// <summary>
        /// The Key that this collection is Partitioned by. Defaults to 
        /// </summary>
        protected virtual IList<string> PartitionKeys { get; set; }

        /// <summary>
        /// The connection to the Azure DocumentDB. This should be injected and configured in the Autofac Config class.
        /// </summary>
        public Container Client { get; set; }

        public HierarchicalPartitionedRepository(
            CosmosClient client,
            ILogger<HierarchicalPartitionedRepository<TModel>> log,
            IOptions<CosmosDbConfiguration> config,
            IList<string> partitionKeys,
            string collectionName = null,
            string databaseName = null
        ) : base(client, log, config, collectionName, databaseName, partitionKeys.First()) {
            Log = log;
            Configuration = config.Value;
            PartitionKeys = partitionKeys;
            DatabaseName = databaseName;

            if (string.IsNullOrEmpty(DatabaseName))
            {
                DatabaseName = config.Value.DefaultDatabaseName;
            }

            if (string.IsNullOrEmpty(CollectionName))
            {
                //If a Collection Name is not set during IOC registration, 
                //allow it to be passed into the constructor
                //and if its not, use the name of the object
                CollectionName = collectionName ?? typeof(TModel).Name;
            }

            Client = client.GetContainer(DatabaseName, CollectionName);
        }

        /*******************************************************************************************
         *                         Hierarchical Partitioned Repository Method
         * *****************************************************************************************/

        public async Task<TModel> Get(Guid id, List<string> partitionKeys)
        {
            try
            {

                var resp = await Client.ReadItemAsync<TModel>(id.ToString(), GetPartitionKey(partitionKeys));
                Log.LogDebug($"CosmosDb query. RU cost:{resp.RequestCharge}");

                return resp;
            }
            catch (CosmosException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                    return null;

                throw;
            }
            catch (Exception e)
            {
                Log.LogError($"There was a problem with the GET operation for object {typeof(TModel).Name}", e);
                throw;
            }
        }

        public async Task<Guid> Create(TModel model, List<string> partitionKeys)
        {
            if (!model.Id.HasValue)
                model.Id = Guid.NewGuid(); // always set the ID on create

            Log.LogDebug($"Creating Object type {typeof(TModel).Name} with ID {model.Id}");

            if (model is IAuditableEntity)
            {
                var auditable = (IAuditableEntity)model;
                if (auditable.AuditMetadata == null)
                    auditable.AuditMetadata = new AuditMetadata();

                ((IAuditableEntity)model).AuditMetadata.CreatedDateTime = DateTime.UtcNow;
                ((IAuditableEntity)model).AuditMetadata.ModifiedDateTime = DateTime.UtcNow;
            }

            try
            {
                await Client.CreateItemAsync<TModel>(model, GetPartitionKey(partitionKeys));
                return model.Id.Value;
            }
            catch (Exception ex)
            {
                Log.LogError("Problem creating document.", ex);
                throw;
            }
        }

        /*******************************************************************************************
         *                         Overrides of GenericRepository
         * *****************************************************************************************/

        /// <summary>
        /// When we have a heirarchical partition key, we assume that the string that is passed in is the first level of the partition key
        /// </summary>
        /// <param name="id"></param>
        /// <param name="partitionKey"></param>
        /// <returns></returns>
        public override async Task<TModel> Get(Guid id, string partitionKey = null)
        {
            if (string.IsNullOrEmpty(partitionKey))
                return await FindOne(x => x.Id == id).ConfigureAwait(false);

            var queryOptions = new QueryRequestOptions() { MaxItemCount = 2 };
            var partitionKeyQuery = $"{PartitionKeys.First()}=\"{partitionKey}\"";
            var query = Client
                        .GetItemLinqQueryable<TModel>(requestOptions: queryOptions, allowSynchronousQueryExecution: true)
                        .Where(x => x.Id == id)
                        .Where(partitionKeyQuery)
                        .AsQueryable();
            var items = new List<TModel>();
            var iterator = query.ToFeedIterator();
            while (iterator.HasMoreResults)
            {
                items.AddRange(await iterator.ReadNextAsync());
            }

            var resp = items.FirstOrDefault();
            //Log.LogDebug($"CosmosDb query. RU cost:{query.RequestCharge}");

            return resp;
        }


        public override async Task Delete(Guid id, string partitionKey, bool force = false)
        {

            if (!force)
            {
                await base.Delete(id, partitionKey).ConfigureAwait(false);
                return;
            }

            var item = await Get(id, partitionKey).ConfigureAwait(false);

            if(item == null)
            {
                Log.LogInformation($"Item {id} does not exist. Nothing to delete.");
                return;
            }

            Log.LogInformation("Force Delete was true. Automated Indexing operations will not be performed for this action.");
            try
            {
                await Client.DeleteItemAsync<TModel>(id.ToString(), GetPartitionKey(item));
            }
            catch (CosmosException e)
            {
                Log.LogWarning(e, "Error deleting item.");
                return; // don't care if there was an exception
            }
        }

        public override Task<ISearchResult<TModel>> Get(string query, ISearchModel model){
            var queryOptions = new QueryRequestOptions() { MaxItemCount = model.PageSize };

            if (!string.IsNullOrEmpty(model.ContinuationToken))
            {
                var decoded = Convert.FromBase64String(model.ContinuationToken);
                var token = System.Text.Encoding.UTF8.GetString(decoded);
                model.ContinuationToken = token;
            }

            var q = Client
                .GetItemLinqQueryable<TModel>(requestOptions: queryOptions, continuationToken: model.ContinuationToken)
                .Where(x => x.ItemType.Contains(typeof(TModel).Name)) //force filtering by Item Type
                .Where(query);

            return GetResults(q, model);
        }

        public override Task<ISearchResult<TModel>> Get(Expression<Func<TModel, bool>> predicate, ISearchModel model)
        {
            var queryOptions = new QueryRequestOptions() { MaxItemCount = model.PageSize };
            
            if (!string.IsNullOrEmpty(model.ContinuationToken))
            {
                var decoded = Convert.FromBase64String(model.ContinuationToken);
                var token = System.Text.Encoding.UTF8.GetString(decoded);
                model.ContinuationToken = token;
            }

            var query = Client
                .GetItemLinqQueryable<TModel>(requestOptions: queryOptions, continuationToken: model.ContinuationToken)
                .Where(x => x.ItemType.Contains(typeof(TModel).Name)) //force filtering by Item Type
                .Where(predicate);
            //.ToFeedIterator();

            return GetResults(query, model);

        }

        protected virtual async Task<ISearchResult<TModel>> GetResults(IQueryable<TModel> query, ISearchModel model)
        {
            var response = new SearchResult<TModel>() { PageSize = model.PageSize };
            if (!string.IsNullOrEmpty(model.SortByField))
            {
                var order = $"{model.SortByField} {(model.SortAscending ? "" : "DESC")}".Trim();
                query = query.OrderBy(order);
            }

            if (string.IsNullOrEmpty(model.ContinuationToken))
            {

                var totalResultsCount = await query.CountAsync().ConfigureAwait(false);
                response.TotalResults = totalResultsCount;

                if (totalResultsCount > 500)
                {
                    Log.LogWarning($"Large Resultset found. Query returned {totalResultsCount} results.");
                }
            }
            if (model.Offset > 0)
            {
                query = query.Skip(model.Offset).Take(model.PageSize);
            }

            Log.LogDebug(query.ToString());

            var result = query.ToFeedIterator();
            var res = await result.ReadNextAsync().ConfigureAwait(false);
            if (res.RequestCharge < 100)
                Log.LogInformation($"Request used {res.RequestCharge} RUs.| Query: {result}");
            else if (res.RequestCharge < 200)
                Log.LogInformation($"Moderate request to CosmosDb used {res.RequestCharge} RUs");
            else
                Log.LogWarning($"Expensive request to CosmosDb. RUs: {res.RequestCharge} | Query: {result}");



            response.Results = res.ToList();
            var continuation = res.ContinuationToken;

            if (response.Results.Count < model.PageSize & !string.IsNullOrEmpty(res.ContinuationToken))
            {
                while (response.Results.Count < model.PageSize && !string.IsNullOrEmpty(res.ContinuationToken))
                {
                    res = await result.ReadNextAsync().ConfigureAwait(false);
                    ((List<TModel>)response.Results).AddRange(res.ToList());
                    continuation = res.ContinuationToken;
                }
            }

            response.ContinuationToken = !string.IsNullOrEmpty(continuation) ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(continuation)) : "";

            return response;
        }

        /// <summary>
        /// Gets the Partition Key object for a point read or a point write from the object
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected virtual Microsoft.Azure.Cosmos.PartitionKey GetPartitionKey(TModel model)
        {
            var builder = new PartitionKeyBuilder();
            var props = model.GetType().GetProperties();

            foreach(var property in PartitionKeys)
            {
                var keyProperty = props.FirstOrDefault(f => f.Name == property);
                if(keyProperty == null)
                {
                    throw new ArgumentException($"Key {property} does not exist on object {model.GetType().Name}");
                }
                var value = keyProperty.GetValue(model) as string;
                builder.Add(value);
            }

            return builder.Build();
        }

        protected virtual Microsoft.Azure.Cosmos.PartitionKey GetPartitionKey(IList<string> keys)
        {
            var builder = new PartitionKeyBuilder();

            if (keys.Count != PartitionKeys.Count)
                throw new ArgumentException($"You provided {keys.Count} partitionKeys, but this collection is partitioned by {PartitionKeys.Count} partition keys.");

            foreach ( var key in keys)
            {
                builder.Add(key);
            }

            return builder.Build();
        }

    }
}
