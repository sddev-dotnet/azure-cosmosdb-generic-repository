using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;


namespace SDDev.Net.GenericRepository.CosmosDB
{
    public class CosmosDbClient : BaseContainerClient
    {
        private IOptions<CosmosDbConfiguration> _config;

        public CosmosDbClient(IOptions<CosmosDbConfiguration> config)
        {

            _config = config;
        }
        public override Container GetClient(string containerName)
        {
            var cosmosConnectionString = _config.Value.ConnectionString;
            var databaseName = _config.Value.DefaultDatabaseName;
            var client = new CosmosClient(cosmosConnectionString);
            var Client = client.GetContainer(databaseName, containerName);
            return Client;
        }
    }
}
