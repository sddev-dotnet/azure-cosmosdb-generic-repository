using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SDDev.Net.GenericRepository.Tests.Utilities;

internal class MockFeedResponse<TEntity> : FeedResponse<TEntity>
{
    private readonly IEnumerable<TEntity> _resource;
    public MockFeedResponse(IEnumerable<TEntity> resource)
    {
        _resource = resource;
    }

    public override string ContinuationToken => string.Empty;

    public override int Count => Resource.Count();

    public override string IndexMetrics => string.Empty;

    public override Headers Headers => new Headers();

    public override IEnumerable<TEntity> Resource => _resource;

    public override HttpStatusCode StatusCode => HttpStatusCode.OK;

    public override CosmosDiagnostics Diagnostics => throw new System.NotImplementedException();

    public override IEnumerator<TEntity> GetEnumerator() => _resource.GetEnumerator();
}
