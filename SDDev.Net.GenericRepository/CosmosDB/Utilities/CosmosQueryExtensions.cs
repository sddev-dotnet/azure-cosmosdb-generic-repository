using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SDDev.Net.GenericRepository.CosmosDB.Utilities.FeedIterator;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB.Utilities;
public static class CosmosQueryExtensions
{
    private static ILogger _logger;
    private static IOptions<CosmosDbConfiguration> _configuration;

    public static void InitializeDependencies(ILogger logger, IOptions<CosmosDbConfiguration> configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Deprecated misspelling of <see cref="InitializeDependencies"/>, retained for backward
    /// compatibility. Prefer <see cref="InitializeDependencies"/>.
    /// </summary>
    public static void InitializeDependenices(ILogger logger, IOptions<CosmosDbConfiguration> configuration)
        => InitializeDependencies(logger, configuration);

    internal static IFeedIteratorProvider FeedIteratorProvider { get; set; } = new CosmosFeedIteratorProvider();

    public static async IAsyncEnumerable<TEntity> ToAsyncEnumerable<TEntity>(this IQueryable<TEntity> queryable)
    {
        var query = LogQuery(queryable);

        var iterator = FeedIteratorProvider.CreateIterator(queryable);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();

            LogResponseDetails(response);

            // Preferable to log inside the loop here, since we're not loading everything into memory it would help to see the request charge over time.
            LogRequestCharge(response.RequestCharge, query);

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    public static async Task<List<TEntity>> ToListAsync<TEntity>(this IQueryable<TEntity> queryable)
    {
        var results = new List<TEntity>();

        var queryText = LogQuery(queryable);

        var iterator = FeedIteratorProvider.CreateIterator(queryable);

        var charge = 0D;
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();

            charge += response.RequestCharge;

            LogResponseDetails(response);

            results.AddRange(response);
        }

        // Preferable to log outside the loop since everything's being loaded into memory immediately.
        LogRequestCharge(charge, queryText);

        return results;
    }

    public static async Task<TEntity> FirstOrDefaultAsync<TEntity>(this IQueryable<TEntity> queryable)
    {
        var queryText = LogQuery(queryable);

        var iterator = FeedIteratorProvider.CreateIterator(queryable);

        // Iterate pages until the first item is found; a leading empty page must not short-circuit
        // the whole query to default.
        var charge = 0D;
        TEntity result = default;
        var found = false;
        while (iterator.HasMoreResults && !found)
        {
            var response = await iterator.ReadNextAsync();
            charge += response.RequestCharge;

            LogResponseDetails(response);

            foreach (var item in response)
            {
                result = item;
                found = true;
                break;
            }
        }

        LogRequestCharge(charge, queryText);

        return result;
    }

    public static async Task<TEntity> SingleOrDefaultAsync<TEntity>(this IQueryable<TEntity> queryable)
    {
        var queryText = LogQuery(queryable);

        var iterator = FeedIteratorProvider.CreateIterator(queryable);

        // Read until we have collected two items (enough to assert non-uniqueness) or pages are
        // exhausted; matches Enumerable.SingleOrDefault semantics across page boundaries.
        var charge = 0D;
        var items = new List<TEntity>(2);
        while (iterator.HasMoreResults && items.Count < 2)
        {
            var response = await iterator.ReadNextAsync();
            charge += response.RequestCharge;

            LogResponseDetails(response);

            foreach (var item in response)
            {
                items.Add(item);
                if (items.Count >= 2) break;
            }
        }

        LogRequestCharge(charge, queryText);

        return items.SingleOrDefault();
    }

    private static void LogResponseDetails<TEntity>(FeedResponse<TEntity> response)
    {
        if (_logger is null || _configuration is null) return;

        if (_configuration.Value.PopulateIndexMetrics)
        {
            _logger.LogWarning("Index Metrics\n{metrics}", response.IndexMetrics);
        }
    }

    private static void LogRequestCharge(double charge, string query)
    {
        if (_logger is null) return;

        if (charge < 100)
            _logger.LogInformation($"Request used {charge} RUs.| Query: {query}");
        else if (charge < 200)
            _logger.LogInformation($"Moderate request to CosmosDb used {charge} RUs");
        else
            _logger.LogWarning($"Expensive request to CosmosDb. RUs: {charge} | Query: {query}");
    }

    private static string LogQuery<TEntity>(IQueryable<TEntity> queryable)
    {
        var query = queryable.ToString();
        _logger?.LogDebug("Executing query: {query}", query);
        return query;
    }
}
