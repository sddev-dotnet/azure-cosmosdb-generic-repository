# SDDev.Net Generic Repository for Azure Cosmos DB

The **SDDev.Net.GenericRepository** package implements the repository pattern on top of Azure Cosmos DB. It allows you to model your data as Plain Old C# Objects (POCOs) while the library handles container access, partition routing, query construction, and cross-cutting features such as caching, indexing, and patch updates. The goal is to make the common 90% of Cosmos DB development frictionless while still giving you escape hatches (for example, direct access to the `Container` client) for the remaining scenarios.

The repository project ships together with a contracts package that contains base entity abstractions and search helpers. This repository hosts both packages, a test suite, and an example application that demonstrate how to use the tooling end-to-end.

## Key capabilities

- **POCO-first data access** – inherit from `BaseStorableEntity` or `BaseAuditableEntity` and interact with your models directly.
- **Flexible querying** – build strongly-typed queries with LINQ expressions or dynamic queries through `System.Linq.Dynamic.Core`.
- **Pagination support** – use `SearchModel` to request page sizes, continuation tokens, offsets, and sorting information.
- **Logical and physical deletes** – toggle between setting a TTL for soft deletes or forcing an immediate removal.
- **Patch support** – issue partial updates using `CosmosPatchOperationCollection` without replacing entire documents.
- **Caching decorator** – wrap repositories with `CachedRepository<T>` to reduce hot reads.
- **Hierarchical partition support** – target containers with composite partition keys through `HierarchicalPartitionedRepository<T>`.
- **Indexing helpers** – integrate with Azure Cognitive Search using the indexing abstractions when needed.

## Packages

Add the following packages to your application:

```bash
 dotnet add package SDDev.Net.GenericRepository
 dotnet add package SDDev.Net.GenericRepository.Contracts
```

> `SDDev.Net.GenericRepository` already references the contracts package. Adding the contracts package explicitly is helpful when you want to compile shared models in a separate project.

## Configuration

1. **Bind Cosmos DB settings** – add the configuration section to `appsettings.json`:

```json
"CosmosDb": {
  "Uri": "https://<your-account>.documents.azure.com:443/",
  "AuthKey": "<your-key>",
  "DefaultDatabaseName": "AppDatabase",
  "DeleteTTL": 3600,
  "IncludeTotalResultsByDefault": true,
  "PopulateIndexMetrics": false
}
```

2. **Register dependencies** – configure the Cosmos client, repository options, and repositories in `Program.cs` or your DI setup:

```csharp
builder.Services.Configure<CosmosDbConfiguration>(
    builder.Configuration.GetSection("CosmosDb"));

builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<IOptions<CosmosDbConfiguration>>().Value;
    var cosmosClientOptions = new CosmosClientOptions
    {
        AllowBulkExecution = settings.EnableBulkQuerying
    };

    return new CosmosClient(settings.Uri, settings.AuthKey, cosmosClientOptions);
});

builder.Services.AddScoped<IRepository<MyEntity>, GenericRepository<MyEntity>>();
// Optional decorators
builder.Services.Decorate<IRepository<MyEntity>, CachedRepository<MyEntity>>();
```

You can override the default database name, container name, or partition key by providing values to the repository constructor when registering the dependency.

## Modeling entities

```csharp
using System.Collections.Generic;

public class MyEntity : BaseAuditableEntity
{
    public string CustomerId { get; set; }
    public string DisplayName { get; set; }
    public List<string> Tags { get; set; } = new();
    public override string PartitionKey => CustomerId;
}
```

- `BaseStorableEntity` provides `Id`, `IsActive`, `ItemType`, `PartitionKey`, and a TTL field.
- `BaseAuditableEntity` extends `BaseStorableEntity` and automatically manages `CreatedDateTime`/`ModifiedDateTime` metadata.
- Override `PartitionKey` to supply the value stored in Cosmos DB when your partition key differs from the type name.

## Working with repositories

Below is an end-to-end example inside an application service. Every method is asynchronous and can be awaited from ASP.NET Core minimal APIs, controllers, or background services.

```csharp
public class MyService
{
    private readonly IRepository<MyEntity> _repository;

    public MyService(IRepository<MyEntity> repository)
    {
        _repository = repository;
    }

    public async Task<Guid> CreateAsync(MyEntity entity)
    {
        return await _repository.Create(entity);
    }

    public async Task<MyEntity> GetAsync(Guid id, string partitionKey)
    {
        return await _repository.Get(id, partitionKey);
    }

    public async Task<IReadOnlyCollection<MyEntity>> SearchAsync(string customerId)
    {
        var search = new SearchModel
        {
            PageSize = 20,
            PartitionKey = customerId,
            SortByField = nameof(MyEntity.DisplayName),
            SortAscending = true
        };

        var result = await _repository.Get(x => x.CustomerId == customerId, search);
        return result.Results.ToList();
    }

    public async Task UpdateAsync(MyEntity entity)
    {
        await _repository.Update(entity);
    }

    public async Task<Guid> UpsertAsync(MyEntity entity)
    {
        return await _repository.Upsert(entity);
    }

    public async Task<int> CountAsync(string customerId)
    {
        return await _repository.Count(x => x.CustomerId == customerId, customerId);
    }

    public async Task DeleteAsync(Guid id, string partitionKey, bool force = false)
    {
        await _repository.Delete(id, partitionKey, force);
    }
}
```

### Continuation tokens and paging

`SearchModel.ContinuationToken` accepts a base64 encoded token returned from a previous query. Set the token on subsequent requests to fetch the next page. You can also use `Offset` for small paged queries; Cosmos DB recommends continuation tokens for production workloads.

### Dynamic queries

If you need to build queries at runtime, call `Get(string query, ISearchModel model)` and pass a dynamic LINQ expression:

```csharp
var search = new SearchModel { PartitionKey = customerId, PageSize = 50 };
var response = await _repository.Get("DisplayName.StartsWith(\"A\")", search);
```

### Logical vs. physical delete

- `Delete(id, partitionKey, force: false)` (the default) sets the entity TTL to the configured `DeleteTTL`. Cosmos DB removes the document automatically after the interval, giving you a soft delete window.
- `Delete(id, partitionKey, force: true)` immediately removes the document. Use this when you are certain you no longer need the data.

### Patch updates

Use `CosmosPatchOperationCollection` to build partial updates. Audit metadata is updated automatically when targeting auditable entities.

```csharp
using SDDev.Net.GenericRepository.CosmosDB.Patch.Cosmos;

var operations = new CosmosPatchOperationCollection<MyEntity>();
operations.Set(x => x.DisplayName, "Contoso (updated)");
operations.Add(x => x.Tags, "priority");

await _repository.Patch(entityId, partitionKey: customerId, operations);
```

### Cached repositories

Wrap repositories with `CachedRepository<T>` to store point reads in `IDistributedCache` implementations such as Redis:

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

builder.Services.AddScoped<IRepository<MyEntity>, GenericRepository<MyEntity>>();
builder.Services.Decorate<IRepository<MyEntity>, CachedRepository<MyEntity>>();
```

- Cache entries default to a 60 second sliding window.
- Call `ICachedRepository<T>.Evict` or `Delete` to remove cache entries explicitly when executing cross-entity operations.

### Hierarchical partition keys

For containers that use composite partition keys, use `IHierarchicalPartitionRepository<T>` / `HierarchicalPartitionedRepository<T>` and pass the ordered key list:

```csharp
builder.Services.AddScoped<IHierarchicalPartitionRepository<MyEntity>>(sp =>
    new HierarchicalPartitionedRepository<MyEntity>(
        sp.GetRequiredService<CosmosClient>(),
        sp.GetRequiredService<ILogger<HierarchicalPartitionedRepository<MyEntity>>>(),
        sp.GetRequiredService<IOptions<CosmosDbConfiguration>>(),
        new List<string> { "CustomerId", "Region" },
        collectionName: "MyEntities"));

var entity = await hierarchicalRepo.Get(id, new List<string> { customerId, region });
```

The repository handles translating the key list into a `PartitionKey` compatible with the Cosmos SDK.

### Indexing integration

The `IndexedRepository<T>` adds Azure Cognitive Search support on top of the base repository. When you decorate a repository with indexing, patch and CRUD operations synchronize content with your search index. Consult the indexing tests for practical examples.

## Samples and reference material

- **Example application:** [`SDDev.Net.GenericRepository.Example`](SDDev.Net.GenericRepository.Example) – bootstrap project that you can expand to prototype your own usage.
- **Integration tests:** [`SDDev.Net.GenericRepository.Tests`](SDDev.Net.GenericRepository.Tests) – comprehensive test suite covering query composition, patch operations, hierarchical partitioning, caching, and more. Specific files worth reviewing include:
  - [`GenericRepositoryTests.cs`](SDDev.Net.GenericRepository.Tests/GenericRepositoryTests.cs) for CRUD, queries, deletes, and counts.
  - [`CachedRepositoryTests.cs`](SDDev.Net.GenericRepository.Tests/CachedRepositoryTests.cs) for cache behavior.
  - [`PatchOperationCollectionTests.cs`](SDDev.Net.GenericRepository.Tests/PatchOperationCollectionTests.cs) for patch examples.
  - [`HierarchicalPartitionRepositoryTests.cs`](SDDev.Net.GenericRepository.Tests/HierarchicalPartitionRepositoryTests.cs) for composite partition keys.
  - [`IndexedRepositoryTests.cs`](SDDev.Net.GenericRepository.Tests/IndexedRepositoryTests.cs) for search integration.

Feel free to copy these tests into your solution as living documentation—they demonstrate the majority of supported operations and edge cases.

## Troubleshooting

- **Cross-partition queries** – the repository warns when a search spans multiple partitions. Provide `SearchModel.PartitionKey` whenever possible to avoid RU spikes.
- **Index metrics** – set `CosmosDbConfiguration.PopulateIndexMetrics` to `true` while tuning indexes. The repository logs Cosmos index metrics for the first page of results to aid diagnostics.
- **Bulk workloads** – enable `CosmosDbConfiguration.EnableBulkQuerying` to turn on the Cosmos SDK bulk executor, reducing throttling for high-volume operations.

## Contributing

Issues and pull requests are welcome! Run the test suite from the repository root before submitting changes:

```bash
 dotnet test SDDev.Net.GenericRepository.sln
```

---

Happy coding!
