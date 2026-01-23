# SDDev.Net Generic Repository for Azure Cosmos DB

The **SDDev.Net.GenericRepository** package implements the repository pattern on top of Azure Cosmos DB. It allows you to model your data as Plain Old C# Objects (POCOs) while the library handles container access, partition routing, query construction, and cross-cutting features such as caching, indexing, and patch updates. The goal is to make the common 90% of Cosmos DB development frictionless while still giving you escape hatches (for example, direct access to the `Container` client) for the remaining scenarios.

> **Prerequisite:** The repository targets **.NET 9**. Install the [.NET 9 SDK](https://dotnet.microsoft.com/download) before building the solution or referencing the package.

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

Add the following packages to your application (both target .NET 9):

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

Wrap repositories with `CachedRepository<T>` to store point reads in `IDistributedCache` implementations such as Redis.

#### Required Packages

Before using cached repositories, install the required packages:

```bash
# Required for Redis caching support
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis

# Required for Decorate extension method
dotnet add package Scrutor
```

#### Complete Setup Example

Here's a complete example showing all required steps:

```csharp
using Microsoft.Extensions.Caching.StackExchangeRedis;
using SDDev.Net.GenericRepository.Caching;
using Scrutor; // Required for Decorate extension method

// 1. Register Redis cache (registers IDistributedCache as Singleton by default)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// 2. Validate cache registration (optional but recommended - automatic validation also occurs at runtime)
builder.Services.ValidateDistributedCacheRegistration();

// 3. Register base repository
builder.Services.AddScoped<IRepository<MyEntity>, GenericRepository<MyEntity>>();

// 4. Decorate with caching (requires Scrutor package)
builder.Services.Decorate<IRepository<MyEntity>, CachedRepository<MyEntity>>();
```

#### Automatic Validation and Enforcement

**Automatic Runtime Validation**: `CachedRepository<T>` automatically validates that `IDistributedCache` is registered as Singleton when instantiated. If multiple cache instances are detected (indicating transient/scoped registration), an exception is thrown at startup, preventing connection pool exhaustion.

**Registration-Time Validation**: You can also validate at registration time using `ValidateDistributedCacheRegistration()`. This provides early feedback, but automatic runtime validation ensures the issue is caught even if you forget to call it.

#### Redis Connection Pool Management for Kubernetes

**Critical for Horizontal Scaling**: `IDistributedCache` must be registered as a **Singleton** to prevent connection pool exhaustion. When deployed in Kubernetes with horizontal scaling, each pod creates its own connection pool. If `IDistributedCache` is registered as transient or scoped, each `CachedRepository<T>` instance within a pod may create its own Redis connection pool, leading to pool exhaustion.

**Architecture**:
- Each pod = 1 process = 1 connection pool (optimal)
- Multiple pods = multiple connection pools (expected and correct)
- Multiple connection pools per pod = connection exhaustion (problematic - **automatically prevented**)

**⚠️ Do NOT register as Transient or Scoped**:

```csharp
// DON'T DO THIS - creates multiple connection pools per pod
// This will be detected and throw an exception at startup
builder.Services.AddTransient<IDistributedCache>(sp => 
    new StackExchangeRedisCache(...));
```

#### Azure Cache for Redis Connection Limits

When using Azure Cache for Redis, connection limits vary by tier. Understanding these limits is crucial for horizontal scaling:

**Connection Limits by Tier**:
- **Basic**: 20 connections (not suitable for production with multiple services)
- **Standard**: 1,000+ connections (suitable for most production scenarios)
- **Premium**: 2,000+ connections (for high-scale scenarios)

**How Connections Are Used**:
- Each pod creates **one** `ConnectionMultiplexer` (via singleton `IDistributedCache`)
- One `ConnectionMultiplexer` handles many concurrent operations efficiently through connection multiplexing
- Physical connections are multiplexed over fewer actual TCP connections

**Connection Calculation**:
- **Total connections** = Number of pods across all services
- Example: 30 services × 10 pods each = 300 connections (fits in Standard tier ✅)
- Example: 30 services × 50 pods each = 1,500 connections (needs Premium tier ✅)

**When You Might Hit Limits**:
- Using Basic tier with multiple services/pods
- Very high pod counts (100+ pods total)
- Need to upgrade Azure Redis tier or consider shared connection service

**Connection Multiplexing**:
StackExchange.Redis uses connection multiplexing - a single `ConnectionMultiplexer` can handle thousands of concurrent operations efficiently. The current implementation (1 connection pool per pod) is optimal because:
- Each `ConnectionMultiplexer` multiplexes operations over a small number of physical connections
- Multiple logical operations share the same physical connections
- Default settings are usually optimal for most scenarios

**Tier Selection Guidance**:
- **Basic (20 connections)**: Only for development/testing with single pod
- **Standard (1,000+ connections)**: Suitable for most production scenarios with multiple services
- **Premium (2,000+ connections)**: For high-scale deployments with many pods

**If You're Hitting Connection Limits**:
1. **Upgrade Azure Redis tier** (recommended for most cases)
2. **Consider shared connection service** (if Basic tier is required for cost reasons)
3. **Monitor connection usage** to understand actual requirements

#### Configuration Options

You can configure cache expiration behavior when registering `CachedRepository<T>`. However, since `Decorate` doesn't support constructor parameters directly, you have two options:

**Option 1: Use default configuration (60 seconds, no sliding expiration)**
```csharp
builder.Services.Decorate<IRepository<MyEntity>, CachedRepository<MyEntity>>();
```

**Option 2: Register with custom configuration using factory method**
```csharp
builder.Services.AddScoped<IRepository<MyEntity>>(sp =>
{
    var innerRepo = sp.GetRequiredService<IRepository<MyEntity>>();
    var cache = sp.GetRequiredService<IDistributedCache>();
    var logger = sp.GetRequiredService<ILogger<BaseRepository<MyEntity>>>();
    var config = sp.GetRequiredService<IOptions<CosmosDbConfiguration>>();
    
    // Custom configuration: 120 seconds cache, with sliding expiration
    return new CachedRepository<MyEntity>(logger, config, innerRepo, cache, cacheSeconds: 120, refreshCache: true);
});
```

**Configuration Parameters**:
- `cacheSeconds`: Cache expiration time in seconds (default: 60)
- `refreshCache`: Whether to use sliding expiration - resets timer on each access (default: false)

#### Multiple Entity Types

When using multiple `CachedRepository<T>` instances for different entity types, they all share the same `IDistributedCache` singleton instance. This is the correct behavior and ensures:

- **Single connection pool per pod**: All cached repositories share one Redis connection pool
- **Efficient resource usage**: Optimal for Kubernetes horizontal scaling
- **Automatic validation**: If any repository detects multiple cache instances, validation fails for all

Example with multiple entity types:
```csharp
// All repositories share the same IDistributedCache singleton
builder.Services.AddScoped<IRepository<Customer>, GenericRepository<Customer>>();
builder.Services.Decorate<IRepository<Customer>, CachedRepository<Customer>>();

builder.Services.AddScoped<IRepository<Order>, GenericRepository<Order>>();
builder.Services.Decorate<IRepository<Order>, CachedRepository<Order>>();

// Both CachedRepository<Customer> and CachedRepository<Order> use the same IDistributedCache instance
// This ensures only one connection pool per pod
```

#### Error Handling

`CachedRepository<T>` handles cache failures gracefully:
- Cache read failures: Logs warning and falls back to repository (returns null if not found)
- Cache write failures: Logs warning and continues with repository operation
- Cache delete failures: Logs warning and continues with repository operation
- Cache operations are best-effort: failures are logged but don't throw exceptions

This ensures that Redis connection issues, pool exhaustion, or timeouts don't crash your application.

#### Cache Key Strategy

Cache keys include the entity type name to ensure uniqueness: `"{EntityTypeName}:{entity.Id}"`. This means:
- Each entity is cached by its type name and unique ID (e.g., `"Customer:abc-123"`)
- Different entity types with the same ID will have separate cache entries, preventing collisions
- All `CachedRepository<T>` instances share the same underlying `IDistributedCache`, so type-prefixed keys are essential
- Cache keys are predictable and follow the format `"{TypeName}:{Guid}"`

#### Cache Operations

- **Automatic caching**: `Get(id)` operations are automatically cached after first retrieval
- **Automatic invalidation**: `Create`, `Update`, `Upsert`, and `Delete` operations automatically update/remove cache entries
- **Manual eviction**: Use `ICachedRepository<T>.Evict(key)` to manually remove cache entries
- **Custom caching**: Use `ICachedRepository<T>.Cache<TModel>(entity, key)` to cache arbitrary objects
- **Custom retrieval**: Use `ICachedRepository<T>.Retrieve<Model>(key)` to retrieve cached objects

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

The `IndexedRepository<T, TIndex>` adds Azure Cognitive Search support on top of the base repository. When you decorate a repository with indexing, patch and CRUD operations synchronize content with your search index. Consult the indexing tests for practical examples and the following tips when wiring up the decorator:

- **One-time setup** – call `Initialize(indexClientName, repository, options)` (or `SetRepository`/`SetIndexClientName`) after construction so the decorator knows which `IRepository<T>` instance and Azure client registrations to use.
- **Customize mapping** – subscribe to the `AfterMapping` or `AfterMappingAsync` events to enrich the index model with data that is not stored in Cosmos DB.
- **Manual index rebuilds** – use `CreateOrUpdateIndex()` to deploy your schema and `UpdateIndex(...)` overloads to repopulate the index. The overload that accepts an `id` now supports an optional `partitionKey` parameter; omit it when your entity uses the default key.
- **Parallel refresh** – `UpdateIndex(IList<T> entities, int maxDegreeOfParallelism = 1)` lets you batch updates efficiently. Increase the degree of parallelism when reindexing larger datasets.
- **Bring-your-own models** – call `Create(entity, indexModel)` or `Update(entity, indexModel)` if you want to control the mapping step entirely.

```csharp
var indexedRepository = serviceProvider.GetRequiredService<IIndexedRepository<MyEntity, MyEntityIndex>>();
indexedRepository.Initialize("SearchClient", innerRepository, new IndexRepositoryOptions
{
    IndexName = "my-entities",
    RemoveOnLogicalDelete = true
});

indexedRepository.AfterMapping += (indexModel, entity) =>
{
    indexModel.Region = ResolveRegion(entity.CustomerId);
};

await indexedRepository.UpdateIndex(id, partitionKey: null); // optional partition key parameter
```

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
