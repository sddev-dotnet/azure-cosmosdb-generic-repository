using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.Repository
{
    public interface IHierarchicalPartitionRepository<T> : IRepository<T> where T : class, IStorableEntity
    {
        /// <summary>
        /// Retrieve a single object by ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<T> Get(Guid id, List<string> partitionKeys);

        /// <summary>
        /// Creates an instance of the object in the partition defined
        /// </summary>
        /// <param name="model"></param>
        /// <param name="partitionKeys"></param>
        /// <returns></returns>
        Task<Guid> Create(T model, List<string> partitionKeys); 
    }
}
