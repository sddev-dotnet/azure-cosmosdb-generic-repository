using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.Repository
{
    public interface ICachedRepository<T> : IRepository<T> where T : class, IStorableEntity
    {
        /// <summary>
        /// Remove an item from the cache without touching the underlying repository
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task Evict(string key);

        /// <summary>
        /// Add any serializable object you want to the cache
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="entity"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        Task Cache<TModel>(TModel entity, string key);

        /// <summary>
        /// Retrieve an object from the cache and dynamically cast it to the type you want
        /// </summary>
        /// <typeparam name="Model"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<Model> Retrieve<Model>(string key);

        /// <summary>
        /// Retrieve an object from the cache without knowing the type
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<dynamic> Retrieve(string key);
    }
}
