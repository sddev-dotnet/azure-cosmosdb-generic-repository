using AutoMapper;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Indexing;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Indexing
{
    /// <summary>
    /// Class used to store data both in Azure CosmosDB and index it in Azure Search
    /// </summary>
    /// <remarks>Uses the decorator pattern to add the capability of indexing documents on top of the IRepository interface</remarks>
    /// <typeparam name="T">The Entity object that is being stored in Cosmos</typeparam>
    /// <typeparam name="Y">The Index Model object that is being saved in Azure Cognitive Search</typeparam>
    public class IndexedRepository<T, Y> : IIndexedRepository<T, Y> where Y : IBaseIndexModel where T : IStorableEntity
    {
        public SearchClient _searchClient;
        public SearchIndexClient _adminClient;


        private IRepository<T> _repository;
        protected IndexRepositoryOptions _options;
        private readonly ILogger<IndexedRepository<T, Y>> _logger;
        private readonly IMapper _mapper;
        private readonly IAzureClientFactory<SearchClient> _clientFactory;
        private readonly IAzureClientFactory<SearchIndexClient> _adminFactory;

        public event Action<Y, T> AfterMapping;
        public event Func<Y, T, Task> AfterMappingAsync;

        public IndexedRepository(
            IAzureClientFactory<SearchClient> searchFactory, 
            IAzureClientFactory<SearchIndexClient> adminFactory,  
            IMapper mapper,
            ILogger<IndexedRepository<T, Y>> logger)
        {
            _mapper = mapper;
            _logger = logger;
            _clientFactory = searchFactory;
            _adminFactory = adminFactory;
            
        }

        public void SetRepository(IRepository<T> repository)
        {
            _logger.LogDebug($"Setting repository instance to {repository.GetType().Name}");
            _repository = repository;
        }


        /// <summary>
        /// This is the name of the index client that has been registered in your 
        /// </summary>
        public void SetIndexClientName(string name)
        {
            _logger.LogDebug($"Setting Index client to {name}");
            _searchClient = _clientFactory.CreateClient(name);

            _options.AdminClientName = _options.AdminClientName ?? _options.IndexName; // backwards compatibility we separated the two properties so we can reuse clients better
            _adminClient = _adminFactory.CreateClient(_options.AdminClientName);
            
        }

        public void Initialize(string indexClientName, IRepository<T> repository, IndexRepositoryOptions options = null)
        {
            _options = options ?? new IndexRepositoryOptions();
            SetIndexClientName(indexClientName);
            SetRepository(repository);
        }

        /// <summary>
        /// Creates the index or DROPS AND RECREATES the index if it has to.
        /// This should not be used in the APIs because it will not force reindexing
        /// of all of the documents.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual async Task CreateOrUpdateIndex()
        {
            var index = new SearchIndex(_options.IndexName)
            {
                Fields = new FieldBuilder().Build(typeof(Y)),
            };

            try
            {
                await _adminClient.CreateOrUpdateIndexAsync(index);
            }
            catch (RequestFailedException)
            {
                // for now, we assume that we failed because the index could not be updated
                await _adminClient.DeleteIndexAsync(_options.IndexName);
                await _adminClient.CreateOrUpdateIndexAsync(index);
            }
        }

        public virtual async Task<Guid> Create(T model)
        {
            Validate();

            // insert document into repository
            var result = await _repository.Create(model);

            // map to index model
            var indexModel = await PerformMap(model).ConfigureAwait(false);
           
            // upload to Azure Search
            var batch = IndexDocumentsBatch.Create(
                IndexDocumentsAction.MergeOrUpload(indexModel)
            );
            var resp = await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);

            return result;
        }
        
        /// <summary>
        /// This is used if you want to perform your own mapping logic outside the 
        /// indexed repository. The ID from the created entity will be set on the model for you
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public virtual async Task<Guid> Create(T entity, Y model)
        {
            Validate();

            // insert document into repository
            var result = await _repository.Create(entity);

            // map to index model
            model.Id = entity.Id.ToString();


            // upload to Azure Search
            var batch = IndexDocumentsBatch.Create(
                IndexDocumentsAction.MergeOrUpload(model)
            );
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);

            return result;
        }

        public virtual async Task Delete(Guid id, string partitionKey, bool force = false)
        {
            Validate();
            var entity = await _repository.Get(id, partitionKey);

            await _repository.Delete(entity, force).ConfigureAwait(false);

            
            var indexModel = _mapper.Map<Y>(entity);
            var batch = IndexDocumentsBatch.Create(IndexDocumentsAction.Delete(indexModel));
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);
            
        }

        public virtual async Task Delete(T model, bool force = false)
        {
            Validate();

            await _repository.Delete(model, force).ConfigureAwait(false);

            var indexModel = _mapper.Map<Y>(model);
            var batch = IndexDocumentsBatch.Create(IndexDocumentsAction.Delete(indexModel));
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);
        }

        public virtual Task<T> FindOne(Expression<Func<T, bool>> predicate, string partitionKey = null, bool singleResult = false)
        {
            Validate();
            return _repository.FindOne(predicate, partitionKey, singleResult);
        }

        public virtual Task<T> Get(Guid id, string partitionKey = null)
        {
            Validate();
            return _repository.Get(id, partitionKey);
        }

        public virtual Task<ISearchResult<T>> Get(Expression<Func<T, bool>> predicate, ISearchModel model)
        {
            Validate();
            return _repository.Get(predicate, model);
        }

        public virtual Task<ISearchResult<T>> Get(string query, ISearchModel model)
        {
            Validate();
            return _repository.Get(query, model);
        }

        public virtual Task<ISearchResult<T>> GetAll(Expression<Func<T, bool>> predicate, ISearchModel model)
        {
            Validate();
            return _repository.GetAll(predicate, model);
        }

        public virtual async Task<Guid> Update(T model)
        {
            Validate();
            var result = await _repository.Update(model).ConfigureAwait(false);

            if (model.IsActive == false)
            {
                if (_options.RemoveOnLogicalDelete)
                {
                    var deleteModel = _mapper.Map<Y>(model);
                    var deleteBatch = IndexDocumentsBatch.Create(IndexDocumentsAction.Delete(deleteModel));
                    await _searchClient.IndexDocumentsAsync(deleteBatch).ConfigureAwait(false);
                    return result;
                }
            }

            // map to index model
            var indexModel = await PerformMap(model).ConfigureAwait(false);

            // upload to Azure Search
            var batch = IndexDocumentsBatch.Create(
                IndexDocumentsAction.MergeOrUpload(indexModel)
            );
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);

            return result;

        }

        public async Task UpdateIndex(T entity)
        {
            Validate();
            if (entity.IsActive == false)
            {
                if (_options.RemoveOnLogicalDelete)
                {
                    var deleteModel = _mapper.Map<Y>(entity);
                    var deleteBatch = IndexDocumentsBatch.Create(IndexDocumentsAction.Delete(deleteModel));
                    await _searchClient.IndexDocumentsAsync(deleteBatch).ConfigureAwait(false);
                    return;
                }
            }

            // map to index model
            var indexModel = await PerformMap(entity).ConfigureAwait(false);

            // upload to Azure Search
            var batch = IndexDocumentsBatch.Create(IndexDocumentsAction.MergeOrUpload(indexModel));
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);
        }

        public Task UpdateIndex(Guid id, string partitionKey)
        {
            return Get(id, partitionKey).ContinueWith(async x =>
            {
                var entity = await x;
                if (entity != null)
                {
                    await UpdateIndex(entity).ConfigureAwait(false);
                }
            });
        }

        /// <summary>
        /// This will update a list of items in the index using Azure Search batches. This will not modify data in the cosmosdb.
        /// </summary>
        /// <param name="entities"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task UpdateIndex(IList<T> entities, int maxDegreeOfParallelism = 1)
        {
            Validate();

            if (maxDegreeOfParallelism <= 0)
            {
                throw new System.InvalidOperationException("maxDegreeOfParallelism must be a positive value.");
            }

            var groups = entities.Partition(900); // Azure Search has a limit of 1000 documents per batch, leave a little space for margin of error

            Parallel.ForEach(groups, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, async group =>
            {

                var tasks = new List<Task<Y>>();
                var actions = new List<IndexDocumentsAction<Y>>();
                foreach(var item in group)
                {

                    if (item.IsActive == false)
                    {
                        if (_options.RemoveOnLogicalDelete)
                        {
                            var deleteModel = _mapper.Map<Y>(item);
                            // Create a batch to delete the document from Azure Search
                            actions.Add(IndexDocumentsAction.Delete(deleteModel));
                            continue;       
                        }
                    }

                    tasks.Add(PerformMap(item));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach(var task in tasks)
                {
                    var item = await task;
                    actions.Add(IndexDocumentsAction.MergeOrUpload(item));
                }

                // map to index and  upload to Azure Search
                var batch = IndexDocumentsBatch.Create(actions.ToArray());

                try
                {
                    var result = await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);
                    foreach(var resultItem in result.Value.Results)
                    {
                        // logs each 
                        if(!resultItem.Succeeded)
                        {
                            _logger.LogError("Failed to update index for item {0}. Status: {1}| Message: {2}", resultItem.Key, resultItem.Status, resultItem.ErrorMessage);
                        }
                    }
                }
                catch (RequestFailedException ex)
                {
                   // this is a failure of the whole batch, log and rethrow
                   _logger.LogError(ex, "Failed to update index for batch");
                    throw;
                }
            });
        }

        /// <summary>
        /// Perform your own mapping instead of using our mapping logic
        /// </summary>
        /// <remarks>assumes that you have performed all of the mapping required outside of this operation</remarks>
        /// <param name="entity"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual async Task<Guid> Update(T entity, Y model)
        {
            Validate();
            var result = await _repository.Update(entity).ConfigureAwait(false);

            if (entity.IsActive == false)
            {
                if (_options.RemoveOnLogicalDelete)
                {
                    var indexModel = _mapper.Map<Y>(entity);
                    var deleteBatch = IndexDocumentsBatch.Create(IndexDocumentsAction.Delete(indexModel));
                    await _searchClient.IndexDocumentsAsync(deleteBatch).ConfigureAwait(false);
                    return result;
                }
            }

            // upload to Azure Search
            var batch = IndexDocumentsBatch.Create(
                IndexDocumentsAction.MergeOrUpload(model)
            );
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);

            return result;
        }

        public virtual async Task<Guid> Upsert(T model)
        {
            Validate();
            var result = await _repository.Upsert(model);

            if (model.IsActive == false)
            {
                if (_options.RemoveOnLogicalDelete)
                {
                    var deleteModel = _mapper.Map<Y>(model);
                    var deleteBatch = IndexDocumentsBatch.Create(IndexDocumentsAction.Delete(deleteModel));
                    await _searchClient.IndexDocumentsAsync(deleteBatch).ConfigureAwait(false);
                    return result;
                }
            }

            // map to index model
            var indexModel = await PerformMap(model).ConfigureAwait(false);

            // upload to Azure Search
            var batch = IndexDocumentsBatch.Create(
                IndexDocumentsAction.MergeOrUpload(indexModel)
            );
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);

            return result;
        }
        
        /// <summary>
        /// Performs a search against the index and returns the full Azure response
        /// </summary>
        /// <param name="request">The search text and the options for the search</param>
        /// <returns></returns>
        public async Task<IndexSearchResult<Y>> Search(SearchRequest request)
        {
            Validate();
            var response = new IndexSearchResult<Y>();
            var results =  await _searchClient.SearchAsync<Y>(request.SearchText, request.Options);

            var metadata = new IndexSearchMetadata();
            var facetOutput = metadata.Facets;
            metadata.TotalResults = results.Value?.TotalCount ?? 0;
            if(results?.Value?.Facets?.Count > 0)
            {
                foreach(var facetResult in results?.Value?.Facets)
                {
                    facetOutput[facetResult.Key] = facetResult.Value
                           .Select(x => new FacetValue { Value = x.Value.ToString(), Count = x.Count })
                           .ToList();
                }
            }

            response.Metadata = metadata;
            // get all of the results and load them into the response
            await foreach(var result in results.Value.GetResultsAsync())
            {
                response.Results.Add(result);
            }

            return response;
        }

        protected void Validate()
        {
            if(this._repository == null)
            {
                throw new ApplicationException("Failed to set repository instance prior to calling. Ensure you call SetRepository before attempting to call any methods.");
            }

            if(this._searchClient == null)
            {
                throw new ApplicationException("The Search Client is not initialized. Ensure that you have registered the SearchClient and provided the search client name.");
            }
        }

        private async Task<Y> PerformMap(T model)
        {
            var indexModel = _mapper.Map<Y>(model);

            if (AfterMapping != null || AfterMappingAsync != null)
            {
                if (AfterMapping != null)
                {
                    AfterMapping(indexModel, model); // allow the user to define additional mapping 
                    return indexModel;
                }

                if (AfterMappingAsync != null)
                {
                    await AfterMappingAsync(indexModel, model).ConfigureAwait(false);
                    return indexModel;
                }
            }

            return indexModel;
        }

        public Task<int> Count(Expression<Func<T, bool>> predicate, string partitionKey = null)
        {
            return _repository.Count(predicate, partitionKey);
        }
    }
}
