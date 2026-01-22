using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.CosmosDB;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Caching
{
    public class CachedRepository<T> : ICachedRepository<T> where T : class, IStorableEntity
    {
        protected int cacheSeconds = 60;
        protected bool refreshCache = true;
        private IRepository<T> _repo;
        private IDistributedCache _cache;
        private readonly ILogger<BaseRepository<T>> _logger;

        // Static tracking to detect multiple cache instances (indicates transient/scoped registration)
        private static readonly HashSet<object> _trackedCacheInstances = new HashSet<object>();
        private static readonly object _trackingLock = new object();

        private JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public CachedRepository(
            ILogger<BaseRepository<T>> log,
            IOptions<CosmosDbConfiguration> config,
            IRepository<T> repository,
            IDistributedCache cache,
            int cacheSeconds = 60,
            bool refreshCache = false
            )
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            // Automatic runtime validation: detect if multiple cache instances are being used
            // This indicates transient/scoped registration which causes connection pool exhaustion
            lock (_trackingLock)
            {
                if (_trackedCacheInstances.Count > 0 && !_trackedCacheInstances.Contains(cache))
                {
                    throw new InvalidOperationException(
                        "Multiple IDistributedCache instances detected. " +
                        "This indicates IDistributedCache is registered as Transient or Scoped, which causes connection pool exhaustion. " +
                        "IDistributedCache MUST be registered as Singleton to prevent Redis connection pool exhaustion in Kubernetes deployments. " +
                        "Solution: Use AddStackExchangeRedisCache() which registers as Singleton by default, or ensure your cache registration uses AddSingleton<IDistributedCache>(). " +
                        "For validation, call builder.Services.ValidateDistributedCacheRegistration() after registering your cache.");
                }

                _trackedCacheInstances.Add(cache);
            }

            _cache = cache;
            _logger = log;
            this.cacheSeconds = cacheSeconds;
            this.refreshCache = refreshCache;
            _repo = repository;
        }

        public async Task<Guid> Create(T model)
        {
            var item = await _repo.Create(model);

            try
            {
                var options = GetCacheEntryOptions();
                await _cache.SetAsync(model.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model, _serializerSettings)), options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache entity {EntityId} of type {EntityType} after create operation. Repository operation succeeded.", model.Id, typeof(T).Name);
            }

            return item;
        }

        public async Task Delete(Guid id, string partitionKey, bool force = false)
        {
            try
            {
                await _cache.RemoveAsync(id.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove entity {EntityId} of type {EntityType} from cache. Repository operation will continue.", id, typeof(T).Name);
            }

            await _repo.Delete(id, partitionKey, force);
        }

        public Task Delete(T model, bool force = false)
        {
            return Delete(model.Id.Value, model.PartitionKey, force);
        }

        public async Task<T> Get(Guid id, string partitionKey = null)
        {
            try
            {
                var item = await _cache.GetStringAsync(id.ToString());
                if (item != null)
                {
                    var entity = JsonConvert.DeserializeObject<T>(item, _serializerSettings);
                    return entity;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve entity {EntityId} of type {EntityType} from cache. Falling back to repository.", id, typeof(T).Name);
            }

            var result = await _repo.Get(id, partitionKey);

            if (result != null)
            {
                try
                {
                    var options = GetCacheEntryOptions();
                    await _cache.SetAsync(result.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result, _serializerSettings)), options);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache entity {EntityId} of type {EntityType} after retrieval. Entity returned from repository.", result.Id, typeof(T).Name);
                }
            }

            return result;
        }

        public IQueryable<T> Query(ISearchModel searchModel)
        {
            return _repo.Query(searchModel);
        }

        public Task<ISearchResult<T>> Get(Expression<Func<T, bool>> predicate, ISearchModel model)
        {
            return _repo.Get(predicate, model);
        }

        public Task<ISearchResult<T>> Get(string query, ISearchModel model)
        {
            return _repo.Get(query, model);
        }

        public Task<ISearchResult<T>> GetAll(Expression<Func<T, bool>> predicate, ISearchModel model)
        {
            return _repo.GetAll(predicate, model);
        }

        public async Task<Guid> Update(T model)
        {
            try
            {
                var options = GetCacheEntryOptions();
                await _cache.SetStringAsync(model.Id.ToString(), JsonConvert.SerializeObject(model, _serializerSettings), options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache entity {EntityId} of type {EntityType} after update operation. Repository operation will continue.", model.Id, typeof(T).Name);
            }

            return await _repo.Update(model);
        }

        public Task Patch(Guid id, string partitionKey, IPatchOperationCollection<T> operationCollection)
        {
            return _repo.Patch(id, partitionKey, operationCollection);
        }

        public async Task<Guid> Upsert(T model)
        {
            try
            {
                var options = GetCacheEntryOptions();
                await _cache.SetStringAsync(model.Id.ToString(), JsonConvert.SerializeObject(model, _serializerSettings), options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache entity {EntityId} of type {EntityType} after upsert operation. Repository operation will continue.", model.Id, typeof(T).Name);
            }

            return await _repo.Upsert(model);
        }

        public async Task<T> FindOne(Expression<Func<T, bool>> predicate, string partitionKey = null, bool singleResult = false)
        {
            var result = await _repo.FindOne(predicate, partitionKey, singleResult).ConfigureAwait(false);

            if (result != null)
            {
                try
                {
                    var options = GetCacheEntryOptions();
                    await _cache.SetAsync(result.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result, _serializerSettings)), options);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache entity {EntityId} of type {EntityType} after FindOne operation. Entity returned from repository.", result.Id, typeof(T).Name);
                }
            }

            return result;
        }

        public async Task Evict(string key)
        {
            try
            {
                await _cache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evict cache key {CacheKey}. Operation will continue.", key);
            }
        }

        public async Task Cache<T1>(T1 entity, string key)
        {
            try
            {
                await _cache.SetAsync(
                    key,
                    Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entity, _serializerSettings)),
                    GetCacheEntryOptions()
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache entity with key {CacheKey} of type {EntityType}. Operation will continue.", key, typeof(T1).Name);
            }
        }

        public async Task<Model> Retrieve<Model>(string id)
        {
            try
            {
                var item = await _cache.GetStringAsync(id);
                if (item != null)
                {
                    Model entity = JsonConvert.DeserializeObject<Model>(item, _serializerSettings);
                    return entity;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve entity with key {CacheKey} of type {ModelType} from cache.", id, typeof(Model).Name);
            }

            return default;
        }

        public Task<dynamic> Retrieve(string key)
        {
            return Retrieve<dynamic>(key);
        }

        public Task<int> Count(Expression<Func<T, bool>> predicate, string partitionKey = null)
        {
            return _repo.Count(predicate, partitionKey);
        }

        private DistributedCacheEntryOptions GetCacheEntryOptions()
        {
            var options = new DistributedCacheEntryOptions();

            if (refreshCache)
            {
                options.SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
            }

            return options;
        }
    }
}
