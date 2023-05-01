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
    public class CachedRepository<T> : GenericRepository<T> where T : class, IStorableEntity
    {
        protected int cacheSeconds = 60;
        protected bool refreshCache = true;
        private IRepository<T> _repo;
        private IDistributedCache _cache;

        public CachedRepository(
            CosmosClient client, 
            ILogger<BaseRepository<T>> log, 
            IOptions<CosmosDbConfiguration> config, 
            IDistributedCache cache,
            int cacheSeconds = 60,
            bool refreshCache = true,
            string collectionName = null, 
            string databaseName = null, 
            string partitionKey = null) : base(client, log, config, collectionName, databaseName, partitionKey)
        {
            _cache = cache;
            this.cacheSeconds = cacheSeconds;
            this.refreshCache = refreshCache;

        }

        public override async Task<Guid> Create(T model)
        {
            var item = await base.Create(model);

            var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
            await _cache.SetAsync(model.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model)), options);

            return item;
        }

        public override async Task Delete(Guid id, string partitionKey, bool force = false)
        {
            await _cache.RemoveAsync(id.ToString());
            await base.Delete(id, partitionKey, force);
        }

        public override async Task<T> Get(Guid id, string partitionKey = null)
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
            var result = await base.Get(id, partitionKey);

            if(result != null)
            {
                var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
                await _cache.SetAsync(result.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result)), options);
            }
            return result;
        }

        public override async Task<Guid> Update(T model)
        {
            var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
            await _cache.SetStringAsync(model.Id.ToString(), JsonConvert.SerializeObject(model), options);

            return await base.Update(model);
        }

        public override async Task<T> FindOne(Expression<Func<T, bool>> predicate, string partitionKey = null, bool singleResult = false)
        {
            
            var result =  await base.FindOne(predicate, partitionKey, singleResult).ConfigureAwait(false);


            if (result != null)
            {
                var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheSeconds));
                await _cache.SetAsync(result.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result)), options);
            }


            return result;
        }
    }
}
