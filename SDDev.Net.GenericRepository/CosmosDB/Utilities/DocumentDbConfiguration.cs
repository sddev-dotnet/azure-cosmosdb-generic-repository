namespace SDDev.Net.GenericRepository.CosmosDB.Utilities
{
    public class CosmosDbConfiguration
    {
        public string Uri { get; set; }

        public string AuthKey { get; set; }

        public string DefaultDatabaseName { get; set; }

        public int DeleteTTL { get; set; } = 10; // 10 second ttl when you call delete

        public string ConnectionString { get; set; }

        public bool EnableBulkQuerying { get; set; } = true;
        public bool PopulateIndexMetrics { get; set; } = false;
        /// <summary>
        /// This will result in every <see cref="GenericRepository{TModel}.Get(System.Linq.Expressions.Expression{System.Func{TModel, bool}}, Contracts.Search.ISearchModel)"/>
        /// method call making an additional CountAsync call to get the total results count.
        /// If the majority of your usage requires the TotalResults, then this is convenient to have enabled.
        /// If you only need the total results count occasionally, then you should disable this and use the <see cref="Contracts.Search.ISearchModel.IncludeTotalResults"/> property.
        /// </summary>
        public bool IncludeTotalResultsByDefault { get; set; } = true;
    }
}
