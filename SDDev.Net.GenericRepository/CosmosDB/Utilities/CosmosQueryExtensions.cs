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

    public static void InitializeDependenices(ILogger logger, IOptions<CosmosDbConfiguration> configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

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

        var response = await iterator.ReadNextAsync();

        LogResponseDetails(response);

        LogRequestCharge(response.RequestCharge, queryText);

        return response.FirstOrDefault();
    }

    public static async Task<TEntity> SingleOrDefaultAsync<TEntity>(this IQueryable<TEntity> queryable)
    {
        var queryText = LogQuery(queryable);

        var iterator = FeedIteratorProvider.CreateIterator(queryable);

        var response = await iterator.ReadNextAsync();

        LogResponseDetails(response);

        LogRequestCharge(response.RequestCharge, queryText);

        return response.SingleOrDefault();
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
