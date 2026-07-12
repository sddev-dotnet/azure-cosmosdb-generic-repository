using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Tests.Utilities;

internal class MockFeedIterator<TEntity> : FeedIterator<TEntity>
{
    private readonly Queue<IReadOnlyList<TEntity>> _pages;


    public MockFeedIterator(IQueryable<TEntity> queryable, int pageSize, bool prependEmptyPage = false)
    {
        var data = queryable.ToList();

        // Group items by page size.
        var pages = data
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / pageSize)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();

        // Simulate Cosmos returning an empty leading page (a real behavior when a page's items are
        // all filtered out server-side) so paging logic that reads only the first page is exposed.
        if (prependEmptyPage)
            pages.Insert(0, new List<TEntity>());

        _pages = new Queue<IReadOnlyList<TEntity>>(pages);
    }

    public override bool HasMoreResults => _pages.Any();

    public override Task<FeedResponse<TEntity>> ReadNextAsync(CancellationToken cancellationToken = default)
    {
        if (!HasMoreResults)
            throw new InvalidOperationException("No more results.");

        var page = _pages.Dequeue();
        var response = new MockFeedResponse<TEntity>(page);
        return Task.FromResult(response as FeedResponse<TEntity>);
    }
}
