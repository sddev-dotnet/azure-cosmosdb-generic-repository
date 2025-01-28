using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB.Utilities;
public static class CosmosQueryExtensions
{
    private static ILogger _logger;
    private static IOptions<CosmosDbConfiguration> _configuration;

    public static void InitializeDependenices(ILogger logger, IOptions<CosmosDbConfiguration> configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public static async Task<List<TEntity>> ToListAsync<TEntity>(this IQueryable<TEntity> queryable)
    {
        var results = new List<TEntity>();

        LogQuery(queryable);

        var iterator = queryable.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();

            LogResponseDetails(response, iterator);

            results.AddRange(response);
        }

        return results;
    }

    public static async Task<TEntity> FirstOrDefaultAsync<TEntity>(this IQueryable<TEntity> queryable)
    {
        LogQuery(queryable);

        var iterator = queryable.ToFeedIterator();

        var response = await iterator.ReadNextAsync();

        LogResponseDetails(response, iterator);

        return response.FirstOrDefault();
    }

    public static async Task<TEntity> SingleOrDefaultAsync<TEntity>(this IQueryable<TEntity> queryable)
    {
        LogQuery(queryable);

        var iterator = queryable.ToFeedIterator();

        var response = await iterator.ReadNextAsync();

        LogResponseDetails(response, iterator);

        return response.SingleOrDefault();
    }

    public static async IAsyncEnumerable<TEntity> ToAsyncEnumerable<TEntity>(this IQueryable<TEntity> queryable)
    {
        LogQuery(queryable);

        var iterator = queryable.ToFeedIterator();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();

            LogResponseDetails(response, iterator);

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }

    private static void LogResponseDetails<TEntity>(FeedResponse<TEntity> response, FeedIterator<TEntity> result)
    {
        if (_logger == null) return;

        if (_configuration != null && _configuration.Value.PopulateIndexMetrics)
        {
            _logger.LogWarning("Index Metrics\n{metrics}", response.IndexMetrics);
        }

        if (response.RequestCharge < 100)
            _logger.LogInformation($"Request used {response.RequestCharge} RUs.| Query: {result}");
        else if (response.RequestCharge < 200)
            _logger.LogInformation($"Moderate request to CosmosDb used {response.RequestCharge} RUs");
        else
            _logger.LogWarning($"Expensive request to CosmosDb. RUs: {response.RequestCharge} | Query: {result}");
    }

    private static void LogQuery<TEntity>(IQueryable<TEntity> queryable)
    {
        _logger?.LogDebug("Executing query: {query}", queryable.ToQueryDefinition());
    }
}
