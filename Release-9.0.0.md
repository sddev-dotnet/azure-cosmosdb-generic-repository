# Release 9.0.0

## Overview
Version 9.0.0 is our biggest update yet. The entire solution now targets .NET 9.0, bringing the latest runtime performance work to every project in the repo. Alongside the platform jump we unlocked faster data access paths in Cosmos DB, tuned indexing for Azure Search, and introduced lightweight patch operations for write-heavy scenarios.

## Breaking Changes & Upgrade Notes
- All projects (`SDDev.Net.GenericRepository`, `.Contracts`, `.Tests`, `.Example`) target `net9.0`. Update your consumer applications and CI pipelines to use the .NET 9 SDK (`global.json` or build agents).
- Rebuild local containers or emulator images if they were pinned to earlier frameworks; binding redirects are no longer applied for 8.x assemblies.

## Feature Highlights
### LINQ `Query()` with Projection Support
`Query()` now exposes the underlying LINQ provider so you can shape documents before materializing them. This reduces RU consumption and payload size for read-heavy workloads.

```csharp
var shaped = await repository
    .Query(new SearchModel { PartitionKey = orderKey })
    .Select(x => new { x.ExampleProperty })
    .FirstOrDefaultAsync();
```
Example usage lives in `GenericRepositoryQueryableTests` and demonstrates both filtered counts and projection-only queries.

### Pre-Mapped Index Batching
`IndexedRepository.UpdateIndex(IList<TIndex> models, ...)` accepts pre-mapped models, enabling you to reuse existing DTOs and skip AutoMapper on hot paths.

```csharp
var mapped = sourceItems
    .Select(mapper.Map<BaseTestIndexModel>)
    .ToList();

await indexedRepository.UpdateIndex(mapped, maxDegreeOfParallelism: 4, groupSize: 500);
```
Combine this with `AfterMappingAsync` for per-item enrichment without sacrificing batching throughput.

### Targeted Patch Operations
New Cosmos and Azure Search patch collections let you issue focused updates instead of full document writes.

```csharp
var patch = new CosmosPatchOperationCollection<TestAuditableObject>();
patch.Replace(x => x.Collection[1], "Test4");
patch.Set(x => x.ChildObject.ExampleProperty, "Updated");

await repository.Patch(id, partitionKey, patch);
```
Azure Search patches mirror the same builders (`AzureSearchPatchOperationCollection`) and enable in-place field updates after calling `_sut.UpdateIndex(...)` in integration tests.

## Additional Improvements
- Repository logging now surfaces RU charges for expensive queries to help right-size partition throughput.
- Index batching enforces Azure Search limits (`groupSize < 1000`) and supports configurable parallelism for large migrations.
- Added samples in `SDDev.Net.GenericRepository.Tests` covering patch orchestration and query projections to guide adoption.

## Upgrade Checklist
1. Install the .NET 9 SDK and retarget downstream projects.
2. Replace conservative read patterns with projected `Query()` calls and migrate batch index jobs to the new overloads for optimal RU usage.
