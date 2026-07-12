using Microsoft.Azure.Cosmos;
using SDDev.Net.GenericRepository.CosmosDB.Utilities.FeedIterator;
using System.Linq;

namespace SDDev.Net.GenericRepository.Tests.Utilities;

internal class MockFeedIteratorProvider : IFeedIteratorProvider
{
    private readonly int _pageSize;
    private readonly bool _prependEmptyPage;

    public MockFeedIteratorProvider(int pageSize, bool prependEmptyPage = false)
    {
        _pageSize = pageSize;
        _prependEmptyPage = prependEmptyPage;
    }

    public FeedIterator<TEntity> CreateIterator<TEntity>(IQueryable<TEntity> queryable)
    {
        return new MockFeedIterator<TEntity>(queryable, _pageSize, _prependEmptyPage);
    }
}
