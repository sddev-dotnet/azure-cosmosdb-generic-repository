using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Search;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.Repository
{
    /// <summary>
    /// Async repository pattern interface intended to handle all IO
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IRepository<T> where T : IStorableEntity
    {
        /// <summary>
        /// Retrieve a single object by ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<T> Get(Guid id, string partitionKey = null);

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
        /// Returns all of the items matching a query result. 
        /// </summary>
        /// <remarks>Not appropriate for large data sets, do your own paging clientside for those</remarks>
        /// <param name="predicate"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<ISearchResult<T>> GetAll(Expression<Func<T, bool>> predicate, ISearchModel model);

        /// <summary>
        /// Uses "FirstOrDefault" or "SingleOrDefault" to find 1 result
        /// </summary>
        /// <param name="predicate">The search criteria</param>
        /// <param name="partitionKey">The partition key to search in. Not providing a partition key results in a cross-partition search</param>
        /// <param name="singleResult">Whether to enforce that only a single result be found or now</param>
        /// <returns></returns>
        Task<T> FindOne(Expression<Func<T, bool>> predicate, string partitionKey = null, bool singleResult = false);

        /// <summary>
        /// Insert or Update an object in the repository
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<Guid> Upsert(T model);

        Task<Guid> Create(T model);

        Task<Guid> Update(T model);

        /// <summary>
        /// Remove the object from the repository
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task Delete(Guid id, string partitionKey, bool force = false);
    }
}