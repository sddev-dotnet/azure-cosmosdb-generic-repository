using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq;

namespace SDDev.Net.GenericRepository.CosmosDB.Utilities.FeedIterator;
internal class CosmosFeedIteratorProvider : IFeedIteratorProvider
{
    public FeedIterator<TEntity> CreateIterator<TEntity>(IQueryable<TEntity> queryable)
    {
        return queryable.ToFeedIterator();
    }
}
