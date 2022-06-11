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

        Task CreateOrUpdateIndex();
    }
}
