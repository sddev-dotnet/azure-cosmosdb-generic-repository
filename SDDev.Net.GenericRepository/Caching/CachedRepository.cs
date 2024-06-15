using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.CosmosDB;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

        public CachedRepository(
            ILogger<BaseRepository<T>> log, 
            IOptions<CosmosDbConfiguration> config,
            IRepository<T> repository,
            IDistributedCache cache,
            int cacheSeconds = 60,
            bool refreshCache = true
            ) 
        {
            _cache = cache;
            this.cacheSeconds = cacheSeconds;
            this.refreshCache = refreshCache;
            _repo = repository;
        }

        public async Task<Guid> Create(T model)
        {
            var item = await _repo.Create(model);

            var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
            await _cache.SetAsync(model.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model)), options);

            return item;
        }

        public async Task Delete(Guid id, string partitionKey, bool force = false)
        {
            await _cache.RemoveAsync(id.ToString());
            await _repo.Delete(id, partitionKey, force);
        }

        public Task Delete(T model, bool force = false)
        {
            return Delete(model.Id.Value, model.PartitionKey, force);
        }

        public async Task<T> Get(Guid id, string partitionKey = null)
        {
            var item = await _cache.GetStringAsync(id.ToString());
            if(item != null)
            {
                if (refreshCache)
                {
                    // reset the cache expiration because we retrieved the object
                    _cache.Refresh(id.ToString());
                }
                var entity = JsonConvert.DeserializeObject<T>(item);
                return entity;
            }
            var result = await _repo.Get(id, partitionKey);

            if(result != null)
            {
                var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
                await _cache.SetAsync(result.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result)), options);
            }
            return result;
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
            var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
            await _cache.SetStringAsync(model.Id.ToString(), JsonConvert.SerializeObject(model), options);

            return await _repo.Update(model);
        }

        public async Task<Guid> Upsert(T model)
        {
            var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
            await _cache.SetStringAsync(model.Id.ToString(), JsonConvert.SerializeObject(model), options);
            return await _repo.Upsert(model);
        }

        public async Task<T> FindOne(Expression<Func<T, bool>> predicate, string partitionKey = null, bool singleResult = false)
        {
            
            var result =  await _repo.FindOne(predicate, partitionKey, singleResult).ConfigureAwait(false);


            if (result != null)
            {
                var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
                await _cache.SetAsync(result.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result)), options);
            }


            return result;
        }

        public async Task Evict(string key)
        {
            await _cache.RemoveAsync(key);
        }

        public Task Cache<T1>(T1 entity, string key)
        {
            return _cache.SetAsync(
                key, 
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entity)), 
                new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds))
            );
        }

        public async Task<Model> Retrieve<Model>(string id)
        {
            var item = await _cache.GetStringAsync(id);
            if (item != null)
            {
                if (refreshCache)
                {
                    // reset the cache expiration because we retrieved the object
                    _cache.Refresh(id.ToString());
                }
                Model entity = JsonConvert.DeserializeObject<Model>(item);
                return entity;
            }

            return default(Model);
        }

        public Task<dynamic> Retrieve(string key)
        {
            return Retrieve<dynamic>(key);
        }

        public Task<int> Count(Expression<Func<T, bool>> predicate, string partitionKey = null)
        {
            return _repo.Count(predicate, partitionKey);
        }
    }
}
