using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Search;

namespace SDDev.Net.GenericRepository.Contracts.Repository
{
    /// <summary>
    /// Async repository pattern interface intended to handle all IO needs for EventLoom
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IRepository<T> where T : IStorableEntity
    {
        /// <summary>
        /// Retrieve a single object by ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<T> Get(Guid id);

        /// <summary>
        /// Dynamically search the database by passing an expression
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<ISearchResult<T>> Get(Expression<Func<T, bool>> predicate, ISearchModel model);

        /// <summary>
        /// Dynamically search the database by passing an expression
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<ISearchResult<T>> Get(string query, ISearchModel model);

        /// <summary>
        /// Insert or Update an object in the repository
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<T> Upsert(T model);

        /// <summary>
        /// Remove the object from the repository
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task Delete(Guid id);
    }
}