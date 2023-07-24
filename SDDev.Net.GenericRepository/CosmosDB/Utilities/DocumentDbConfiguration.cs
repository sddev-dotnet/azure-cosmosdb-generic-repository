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
    }
}
