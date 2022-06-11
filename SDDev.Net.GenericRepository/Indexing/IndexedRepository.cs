﻿using AutoMapper;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Indexing;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Indexing
{ 
    /// <summary>
    /// Class used to store data both in Azure CosmosDB and index it in Azure Search
    /// </summary>
    /// <remarks>Uses the decorator pattern to add the capability of indexing documents on top of the IRepository interface</remarks>
    /// <typeparam name="T">The Entity object that is being stored in Cosmos</typeparam>
    /// <typeparam name="Y">The Index Model object that is being saved in Azure Cognitive Search</typeparam>
    public class IndexedRepository<T, Y> : IIndexedRepository<T, Y> where Y : IBaseIndexModel where T: IStorableEntity
    {
        public SearchClient _searchClient;
        public SearchIndexClient _adminClient;


        private IRepository<T> _repository;
        protected IndexRepositoryOptions _options;
        private readonly ILogger<IndexedRepository<T, Y>> _logger;
        private readonly IMapper _mapper;
        private readonly IAzureClientFactory<SearchClient> _clientFactory;
        private readonly IAzureClientFactory<SearchIndexClient> _adminFactory;

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
            _adminClient = _adminFactory.CreateClient(_options.IndexName);
            
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
            var indexModel = _mapper.Map<Y>(model);

           
            // upload to Azure Search
            var batch = IndexDocumentsBatch.Create(
                IndexDocumentsAction.MergeOrUpload(indexModel)
            );
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);

            return result;
        }

        public virtual async Task Delete(Guid id, string partitionKey, bool force = false)
        {
            Validate();
            var entity = await _repository.FindOne(x => x.Id == id).ConfigureAwait(false);

            await _repository.Delete(id, partitionKey, force).ConfigureAwait(false);

            if(!this._options.RemoveOnLogicalDelete && force == false)
            {
                var item = await _repository.FindOne(x => x.Id == id);
                var indexModel = _mapper.Map<Y>(item);
                var batch = IndexDocumentsBatch.Create(IndexDocumentsAction.MergeOrUpload(indexModel));
                await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);
            } else
            {
                var indexModel = _mapper.Map<Y>(entity);
                var batch = IndexDocumentsBatch.Create(IndexDocumentsAction.Delete(indexModel));
                await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);
            }
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


            // map to index model
            var indexModel = _mapper.Map<Y>(model);

            // upload to Azure Search
            var batch = IndexDocumentsBatch.Create(
                IndexDocumentsAction.MergeOrUpload(indexModel)
            );
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);

            return result;

        }

        public virtual async Task<Guid> Upsert(T model)
        {
            Validate();
            var result = await _repository.Upsert(model);

            // map to index model
            var indexModel = _mapper.Map<Y>(model);

            // upload to Azure Search
            var batch = IndexDocumentsBatch.Create(
                IndexDocumentsAction.MergeOrUpload(indexModel)
            );
            await _searchClient.IndexDocumentsAsync(batch).ConfigureAwait(false);

            return result;
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

    }
}