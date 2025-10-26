using Azure.Search.Documents.Models;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Indexing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.Repository
{
    public interface IIndexedRepository<T, Y> : IRepository<T> where Y : IBaseIndexModel where T : IStorableEntity
    {
        /// <summary>
        /// This event is called after the DML operation is called on the T object and then the T object is mapped to Y object.
        /// It allows you to perform additional mapping that is not possible within the library (like adding additional data to the index model) before indexing it
        /// </summary>
        public event Action<Y, T> AfterMapping;

        public event Func<Y, T, Task> AfterMappingAsync;

        /// <summary>
        /// Call this method to set the repository that is being decorated. This exists because we have multiple extensions of the IRepository 
        /// and some require additional setup. So we don't constructor inject this property and instead let the consumer decide what concrete
        /// repository to pass in
        /// </summary>
        /// <param name="repository"></param>
        void SetRepository(IRepository<T> repository);

        /// <summary>
        /// Sets the Index Client name so an Azure Search Index Client can be instantiated (from the Azure Client factory)
        /// </summary>
        /// <param name="name"></param>
        void SetIndexClientName(string name);

        /// <summary>
        /// Shorthand to set repository and index client in a single method
        /// </summary>
        /// <param name="indexClientName"></param>
        /// <param name="repository"></param>
        void Initialize(string indexClientName, IRepository<T> repository, IndexRepositoryOptions options = null);

        /// <summary>
        /// Allows you to provide your own mapped object. We will set the ID property correctly, but all other mapping is owned by the caller
        /// </summary>
        /// <remarks>This is useful if you want to control mapping from one object to another or have additional data you need to map in for the index</remarks>
        /// <param name="entity"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<Guid> Create(T entity, Y model);


        Task<Guid> Update(T entity, Y model);

        Task CreateOrUpdateIndex();

        Task UpdateIndex(T entity);

        Task UpdateIndex(Guid id, string partitionKey = null);

        Task UpdateIndex(IList<T> entities, int maxDegreeOfParallelism = 1, int groupSize = 900);


        /// <summary>
        /// Perform mapping yourself and just provide the models to be indexed.
        /// </summary>
        /// <param name="models"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="groupSize">The size batches to upload to Azure Search. Do not exceed 1000</param>
        /// <returns></returns>
        Task UpdateIndex(IList<Y> models, int maxDegreeOfParallelism = 1, int groupSize = 900);

        /// <summary>
        /// Update a single model in the index that you have already mapped
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task UpdateIndex(Y model);

        Task<IndexSearchResult<Y>> Search(SearchRequest request);
    }
}
