using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace SDDev.Net.GenericRepository.CosmosDB
{
    /// <summary>
	/// Class that allows for most CRUD operations on an Azure DocumentDB
	/// </summary>
	/// <typeparam name="TModel"></typeparam>
	public class GenericRepository<TModel> : BaseRepository<TModel> where TModel : class, IStorableEntity
    {
        private string _defaultPartitionKey;

        public override async Task<TModel> Get(Guid id, string partitionKey = null)
        {
            try
            {

                if(string.IsNullOrEmpty(partitionKey))
                    return await FindOne(x => x.Id == id).ConfigureAwait(false);
                
                var resp = await Client.ReadItemAsync<TModel>(id.ToString(), new PartitionKey(partitionKey));
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

        /// <summary>
        /// Query with a predicate you built using PredicateBuilder
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public override async Task<ISearchResult<TModel>> Get(Expression<Func<TModel, bool>> predicate, ISearchModel model)
        {
            var queryOptions = new QueryRequestOptions() { MaxItemCount = model.PageSize };
            if (!string.IsNullOrEmpty(model.PartitionKey))
                queryOptions.PartitionKey = new PartitionKey(model.PartitionKey);
            else
                Log.LogWarning($"Enabling Cross-Partition Query in repo {this.GetType().Name}");

            if (!string.IsNullOrEmpty(model.ContinuationToken))
            {
                var decoded = Convert.FromBase64String(model.ContinuationToken);
                var token = System.Text.Encoding.UTF8.GetString(decoded);
                model.ContinuationToken = token;
            }

            var response = new SearchResult<TModel>() { PageSize = model.PageSize };

            var query= Client
                .GetItemLinqQueryable<TModel>(requestOptions: queryOptions, continuationToken: model.ContinuationToken)
                .Where(x => x.ItemType.Contains(typeof(TModel).Name)) //force filtering by Item Type
                .Where(predicate);
                //.ToFeedIterator();

            if(!string.IsNullOrEmpty(model.SortByField))
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

            if(model.Offset > 0)
            {
                query = query.Skip(model.Offset).Take(model.PageSize);
            }

            Log.LogDebug(query.ToString());

            var result = query.ToFeedIterator();
            var res = await result.ReadNextAsync();
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
                    res = await result.ReadNextAsync();
                    ((List<TModel>)response.Results).AddRange(res.ToList());
                    continuation = res.ContinuationToken;
                }
            }

            response.ContinuationToken = !string.IsNullOrEmpty(continuation) ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(continuation)) : "";

            return response;

        }

        /// <summary>
        /// Build a query string using Dynamic Linq 
        /// </summary>
        /// <param name="query"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public async override Task<ISearchResult<TModel>> Get(string query, ISearchModel model)
        {
            var queryOptions = new QueryRequestOptions() { MaxItemCount = model.PageSize };

            if (!string.IsNullOrEmpty(model.PartitionKey))
                queryOptions.PartitionKey = new PartitionKey(model.PartitionKey);
            else
                Log.LogWarning($"Enabling Cross-Partition Query in repo {this.GetType().Name}");

            if (!string.IsNullOrEmpty(model.ContinuationToken))
            {
                var decoded = Convert.FromBase64String(model.ContinuationToken);
                var token = System.Text.Encoding.UTF8.GetString(decoded);
                model.ContinuationToken = token;
            }

            var response = new SearchResult<TModel>() { PageSize = model.PageSize };


            var q = Client
                .GetItemLinqQueryable<TModel>(requestOptions: queryOptions, continuationToken: model.ContinuationToken)
                .Where(x => x.ItemType.Contains(typeof(TModel).Name)) //force filtering by Item Type
                .Where(query);

            if (!string.IsNullOrEmpty(model.SortByField))
            {
                var order = $"{model.SortByField} {(model.SortAscending ? "ASC" : "DESC")}";
                q.OrderBy(order);
            };

            if (string.IsNullOrEmpty(model.ContinuationToken))
            {
                var totalResultsCount = await q.CountAsync().ConfigureAwait(false);

                response.TotalResults = totalResultsCount;

                if (totalResultsCount > 500)
                {
                    Log.LogWarning($"Large Resultset found. Query returned {totalResultsCount} results.");
                }
            }

            if (model.Offset > 0)
            {
                query.Skip(model.Offset).Take(model.PageSize);
            }

            Log.LogDebug(query.ToString());

            var result = q.ToFeedIterator();

            var res = await result.ReadNextAsync();
            if (res.RequestCharge < 100)
                Log.LogInformation($"Request used {res.RequestCharge} RUs.| Query: {result}");
            else if (res.RequestCharge < 200)
                Log.LogInformation($"Moderate request to CosmosDb used {res.RequestCharge} RUs");
            else
                Log.LogWarning($"Expensive request to CosmosDb. RUs: {res.RequestCharge} | Query: {result}");

            response.Results = res.ToList();

            if (response.Results.Count < model.PageSize & !string.IsNullOrEmpty(res.ContinuationToken))
            {
                while (response.Results.Count < model.PageSize && !string.IsNullOrEmpty(res.ContinuationToken))
                {
                    res = await result.ReadNextAsync();
                    ((List<TModel>)response.Results).AddRange(res.ToList());
                }
            }

            response.ContinuationToken = !string.IsNullOrEmpty(res.ContinuationToken) ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(res.ContinuationToken)) : "";

            return response;
        }

        /// <summary>
        /// This will perform a Replace, finding the document by Id and then replacing it with the document that is passed in
        /// </summary>
        /// <param name="model"></param>
        /// <deprecated>Use either Create or Update purposefully</deprecated>
        /// <returns></returns>
        public async override Task<Guid> Upsert(TModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                    model.Id = Guid.NewGuid();

                if (model is IAuditableEntity)
                {
                    var auditable = model as IAuditableEntity;
                    auditable.AuditMetadata.ModifiedDateTime = DateTime.UtcNow;
                }

                var resp = await Client.UpsertItemAsync<TModel>(model);
                Log.LogDebug($"CosmosDb upsert. RU cost:{resp.RequestCharge}");
                return model.Id.Value;
            }
            catch (CosmosException e)
            {
                Log.LogError(e, "Failed to upsert object.");
                throw;
            }
        }

        public override async Task Delete(Guid id, string partitionKey, bool force = false)
        {

            Log.LogInformation($"Deleting document {id} from Database {DatabaseName} Collection {CollectionName}");
            

            if (!force)
            {
                var item = await Get(id, partitionKey);
                if (item == null)
                {
                    Log.LogWarning($"Item with Id {id} could not be found. It must already be deleted or the incorrect partition key was supplied.");
                    return;
                }

                item.TimeToLive = Configuration.DeleteTTL; // set the ttl so it gets deleted after a configured amount of time by the db engine
                Log.LogInformation($"Force Delete false, setting TTL to {Configuration.DeleteTTL}");

                var resp = await Client.UpsertItemAsync(item);
                Log.LogDebug($"CosmosDb point read delete. RU cost:{resp.RequestCharge}");
            }
            else
            {
                Log.LogInformation("Force Delete was true. Automated Indexing operations will not be performed for this action.");
                try
                {
                    await Client.DeleteItemAsync<TModel>(id.ToString(), new Microsoft.Azure.Cosmos.PartitionKey(partitionKey));
                }
                catch (CosmosException e)
                {
                    Log.LogWarning(e, "Error deleting item.");
                    return; // don't care if there was an exception
                }
            }



        }
        public override async Task Delete(TModel model, bool force = false)
        {
            if (!force)
            {
                
                if (model == null)
                {
                    Log.LogWarning($"Item with Id {model.Id} could not be found. It must already be deleted or the incorrect partition key was supplied.");
                    return;
                }

                model.TimeToLive = Configuration.DeleteTTL; // set the ttl so it gets deleted after a configured amount of time by the db engine
                Log.LogInformation($"Force Delete false, setting TTL to {Configuration.DeleteTTL}");

                var resp = await Client.UpsertItemAsync(model);
                Log.LogDebug($"CosmosDb query delete. RU cost:{resp.RequestCharge}");

            }
            else
            {
                Log.LogInformation("Force Delete was true. Automated Indexing operations will not be performed for this action.");
                try
                {
                    await Client.DeleteItemAsync<TModel>(model.Id.ToString(), GetPartitionKey(model));
                }
                catch (CosmosException e)
                {
                    Log.LogWarning(e, "Error deleting item.");
                    return; // don't care if there was an exception
                }
            }
        }

        public override async Task<Guid> Create(TModel model)
        {
            if (!model.Id.HasValue)
                model.Id = Guid.NewGuid(); // always set the ID on create

            Log.LogDebug($"Creating Object type {typeof(TModel).Name} with ID {model.Id}");

            if (model is IAuditableEntity)
            {
                var auditable = (IAuditableEntity)model;
                if(auditable.AuditMetadata == null)
                    auditable.AuditMetadata = new AuditMetadata();

                ((IAuditableEntity)model).AuditMetadata.CreatedDateTime = DateTime.UtcNow;
                ((IAuditableEntity)model).AuditMetadata.ModifiedDateTime = DateTime.UtcNow;
            }

            try
            {
                var resp = await Client.CreateItemAsync<TModel>(model);
                Log.LogDebug($"CosmosDb create. RU cost:{resp.RequestCharge}");
                return model.Id.Value;
            }
            catch (Exception ex)
            {
                Log.LogError("Problem creating document.", ex);
                throw;
            }
        }

        public override async Task<Guid> Update(TModel model)
        {
            try
            {
                if (model is IAuditableEntity)
                {
                    var auditable = model as IAuditableEntity;
                    auditable.AuditMetadata.ModifiedDateTime = DateTime.UtcNow;
                }

                var resp = await Client.ReplaceItemAsync(model, model.Id.ToString());
                Log.LogDebug($"CosmosDb update. RU cost:{resp.RequestCharge}");

                return model.Id.Value;
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to update document", ex);
                throw;
            }
        }

        public override async Task<ISearchResult<TModel>> GetAll(Expression<Func<TModel, bool>> predicate, ISearchModel model)
        {
            var results = new List<TModel>();
            try
            {
                do
                {
                    var partial = await Get(predicate, model);
                    model.ContinuationToken = partial.ContinuationToken;
                    results.AddRange(partial.Results);
                } while (!string.IsNullOrEmpty(model.ContinuationToken));

                var searchResult = new SearchResult<TModel>()
                {
                    Results = results,
                    TotalResults = results.Count
                };

                return searchResult;
            } catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                    return new SearchResult<TModel>() { Results = new List<TModel>() };

                throw;
            }
            
        }

        public override async Task<TModel> FindOne(Expression<Func<TModel, bool>> predicate, string partitionKey = null, bool singleResult = false)
        {
            var queryOptions = new QueryRequestOptions() { MaxItemCount = 2 };

            if (!singleResult)
            {
                try
                {
                    var query = Client
                        .GetItemLinqQueryable<TModel>(requestOptions: queryOptions, allowSynchronousQueryExecution: true)
                        .Where(x => x.ItemType.Contains(typeof(TModel).Name)) //force filtering by Item Type
                                                                              //.Where(x => string.IsNullOrEmpty(partitionKey) ? x.IsActive : x.IsActive && x.PartitionKey == partitionKey)
                        .Where(predicate)
                        .AsQueryable();
                    var items = new List<TModel>();
                    var iterator = query.ToFeedIterator();
                    while (iterator.HasMoreResults)
                    {
                        items.AddRange(await iterator.ReadNextAsync());
                    }

                    return items.FirstOrDefault();
                }
                catch (CosmosException e)
                {
                    if (e.StatusCode == HttpStatusCode.NotFound)
                        return null;

                    throw;
                }
            }
                
            else
            {
                try
                {
                    var query = Client
                        .GetItemLinqQueryable<TModel>(requestOptions: queryOptions, allowSynchronousQueryExecution: true)
                        .Where(x => x.ItemType.Contains(typeof(TModel).Name)) //force filtering by Item Type
                                                                              //.Where(x => string.IsNullOrEmpty(partitionKey) ? x.IsActive : x.IsActive && x.PartitionKey == partitionKey)
                        .Where(predicate)
                        .AsQueryable();
                    var items = new List<TModel>();
                    var iterator = query.ToFeedIterator();
                    while (iterator.HasMoreResults)
                    {
                        items.AddRange(await iterator.ReadNextAsync());
                    }

                    return items.SingleOrDefault();
                }
                catch (CosmosException e)
                {
                    if (e.StatusCode == HttpStatusCode.NotFound)
                        return null;

                    throw;
                }
            }
        }

        private Microsoft.Azure.Cosmos.PartitionKey GetPartitionKey(TModel model)
        {
            var builder = new PartitionKeyBuilder();
            var props = model.GetType().GetProperties();

            
            var keyProperty = props.FirstOrDefault(f => f.Name == PartitionKey);
            if (keyProperty == null)
            {
                throw new ArgumentException($"Key {PartitionKey} does not exist on object {model.GetType().Name}");
            }
            var value = keyProperty.GetValue(model) as string;
            builder.Add(value);
            

            return builder.Build();
        }

        [ActivatorUtilitiesConstructor]
        public GenericRepository(
            CosmosClient client,
            ILogger<BaseRepository<TModel>> log,
            IOptions<CosmosDbConfiguration> config,
            string collectionName = null,
            string databaseName = null,
            string partitionKey = null) : base(client, log, config, collectionName, databaseName, partitionKey)
        {
            var defaultInstance = Activator.CreateInstance<TModel>();
            _defaultPartitionKey = defaultInstance.PartitionKey;
        }

        public GenericRepository(
            IContainerClient client,
            ILogger<BaseRepository<TModel>> log,
            IOptions<CosmosDbConfiguration> config,
            string collectionName = null,
            string databaseName = null,
            string partitionKey = null) : base(client, log, config, collectionName, databaseName, partitionKey)
            {
                var defaultInstance = Activator.CreateInstance<TModel>();
                _defaultPartitionKey = defaultInstance.PartitionKey;
            }
    }
}
