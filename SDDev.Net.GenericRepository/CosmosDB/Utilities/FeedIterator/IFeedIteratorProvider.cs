using Microsoft.Azure.Cosmos;
using System.Linq;

namespace SDDev.Net.GenericRepository.CosmosDB.Utilities.FeedIterator;
internal interface IFeedIteratorProvider
{
    FeedIterator<TEntity> CreateIterator<TEntity>(IQueryable<TEntity> queryable);
}
