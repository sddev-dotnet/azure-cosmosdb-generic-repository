namespace SDDev.Net.GenericRepository.Contracts.Search
{
    public interface ISearchModel
    {
        int PageSize { get; set; }

        int Offset { get; set; }

        bool SortAscending { get; set; }

        string SortByField { get; set; }

        string ContinuationToken { get; set; }

        string PartitionKey { get; set; }
        /// <summary>
        /// <see cref="ISearchResult{T}"/> will include a <see cref="ISearchResult{T}.TotalResults"/> count, which requires an additional query (RU usage and latency).
        /// This behavior can be disabled via Configuration at CosmosDbConfiguration.IncludeTotalResultsByDefault.
        /// This property will override the global configuration. The global configuration is true by default.
        /// </summary>
        bool? IncludeTotalResults { get; set; }
    }
}