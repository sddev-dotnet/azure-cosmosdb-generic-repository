using Microsoft.Azure.Cosmos;
using System;

namespace SDDev.Net.GenericRepository.CosmosDB
{
    public abstract class BaseContainerClient : IContainerClient
    {
        public abstract Container GetClient(string containerName);
    }
}