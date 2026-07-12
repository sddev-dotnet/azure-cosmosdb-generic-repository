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
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Caching
{
    public class CachedRepository<T> : ICachedRepository<T> where T : class, IStorableEntity
    {
        protected int cacheSeconds = 60;
        protected bool refreshCache = true;
        private IRepository<T> _repo;
        private IDistributedCache _cache;

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
            _cache = cache;
            this.cacheSeconds = cacheSeconds;
            this.refreshCache = refreshCache;
            _repo = repository;
        }

        public Task<Guid> Create(T model) => Create(model, CancellationToken.None);

        public async Task<Guid> Create(T model, CancellationToken cancellationToken)
        {
            var item = await _repo.Create(model, cancellationToken);

            var options = GetCacheEntryOptions();
            await _cache.SetAsync(model.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model, _serializerSettings)), options, cancellationToken);

            return item;
        }

        public Task Delete(Guid id, string partitionKey, bool force = false) => Delete(id, partitionKey, force, CancellationToken.None);

        public async Task Delete(Guid id, string partitionKey, bool force, CancellationToken cancellationToken)
        {
            await _cache.RemoveAsync(id.ToString(), cancellationToken);
            await _repo.Delete(id, partitionKey, force, cancellationToken);
        }

        public Task Delete(T model, bool force = false)
        {
            return Delete(model.Id.Value, model.PartitionKey, force);
        }

        public Task Delete(T model, bool force, CancellationToken cancellationToken)
            => Delete(model.Id.Value, model.PartitionKey, force, cancellationToken);

        public Task<T> Get(Guid id, string partitionKey = null) => Get(id, partitionKey, CancellationToken.None);

        public async Task<T> Get(Guid id, string partitionKey, CancellationToken cancellationToken)
        {
            var item = await _cache.GetStringAsync(id.ToString(), cancellationToken);
            if (item != null)
            {
                var entity = JsonConvert.DeserializeObject<T>(item, _serializerSettings);
                return entity;
            }

            var result = await _repo.Get(id, partitionKey, cancellationToken);

            if (result != null)
            {
                var options = GetCacheEntryOptions();
                await _cache.SetAsync(result.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result, _serializerSettings)), options, cancellationToken);
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

        public Task<ISearchResult<T>> Get(Expression<Func<T, bool>> predicate, ISearchModel model, CancellationToken cancellationToken)
            => _repo.Get(predicate, model, cancellationToken);

        public Task<ISearchResult<T>> Get(string query, ISearchModel model)
        {
            return _repo.Get(query, model);
        }

        public Task<ISearchResult<T>> Get(string query, ISearchModel model, CancellationToken cancellationToken)
            => _repo.Get(query, model, cancellationToken);

        public Task<ISearchResult<T>> GetAll(Expression<Func<T, bool>> predicate, ISearchModel model)
        {
            return _repo.GetAll(predicate, model);
        }

        public Task<ISearchResult<T>> GetAll(Expression<Func<T, bool>> predicate, ISearchModel model, CancellationToken cancellationToken)
            => _repo.GetAll(predicate, model, cancellationToken);

        public Task<Guid> Update(T model) => Update(model, CancellationToken.None);

        public async Task<Guid> Update(T model, CancellationToken cancellationToken)
        {
            // Write to the database first; only cache after the write succeeds so a failed
            // write cannot poison the cache with unpersisted state.
            var result = await _repo.Update(model, cancellationToken);

            var options = GetCacheEntryOptions();
            await _cache.SetStringAsync(model.Id.ToString(), JsonConvert.SerializeObject(model, _serializerSettings), options, cancellationToken);

            return result;
        }

        public Task Patch(Guid id, string partitionKey, IPatchOperationCollection<T> operationCollection)
            => Patch(id, partitionKey, operationCollection, CancellationToken.None);

        public async Task Patch(Guid id, string partitionKey, IPatchOperationCollection<T> operationCollection, CancellationToken cancellationToken)
        {
            // Patch operations are not trivially replayable against the cached full-document copy,
            // so evict after a successful patch and let the next Get repopulate from Cosmos.
            await _repo.Patch(id, partitionKey, operationCollection, cancellationToken);
            await _cache.RemoveAsync(id.ToString(), cancellationToken);
        }

        public Task<Guid> Upsert(T model) => Upsert(model, CancellationToken.None);

        public async Task<Guid> Upsert(T model, CancellationToken cancellationToken)
        {
            // Write to the database first; only cache after the write succeeds.
            var result = await _repo.Upsert(model, cancellationToken);

            var options = GetCacheEntryOptions();
            await _cache.SetStringAsync(model.Id.ToString(), JsonConvert.SerializeObject(model, _serializerSettings), options, cancellationToken);
            return result;
        }

        public Task<T> FindOne(Expression<Func<T, bool>> predicate, string partitionKey = null, bool singleResult = false)
            => FindOne(predicate, partitionKey, singleResult, CancellationToken.None);

        public async Task<T> FindOne(Expression<Func<T, bool>> predicate, string partitionKey, bool singleResult, CancellationToken cancellationToken)
        {

            var result = await _repo.FindOne(predicate, partitionKey, singleResult, cancellationToken).ConfigureAwait(false);


            if (result != null)
            {
                var options = GetCacheEntryOptions();
                await _cache.SetAsync(result.Id.ToString(), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result, _serializerSettings)), options, cancellationToken);
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
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(entity, _serializerSettings)),
                GetCacheEntryOptions()
            );
        }

        public async Task<Model> Retrieve<Model>(string id)
        {
            var item = await _cache.GetStringAsync(id);
            if (item != null)
            {
                Model entity = JsonConvert.DeserializeObject<Model>(item, _serializerSettings);
                return entity;
            }

            return default;
        }

        public Task<dynamic> Retrieve(string key)
        {
            return Retrieve<dynamic>(key);
        }

        public Task<int> Count(Expression<Func<T, bool>> predicate, string partitionKey = null)
            => _repo.Count(predicate, partitionKey);

        public Task<int> Count(Expression<Func<T, bool>> predicate, string partitionKey, CancellationToken cancellationToken)
            => _repo.Count(predicate, partitionKey, cancellationToken);

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
