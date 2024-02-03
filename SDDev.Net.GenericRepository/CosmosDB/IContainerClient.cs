using Microsoft.Azure.Cosmos;

namespace SDDev.Net.GenericRepository.CosmosDB
{
    public interface IContainerClient
    {
        Container GetClient(string containerName);
    }
}
