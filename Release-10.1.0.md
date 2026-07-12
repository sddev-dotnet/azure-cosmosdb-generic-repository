# Release 10.1.0 (in progress)

Additive, backward-compatible minor release. Work is landing incrementally; this file tracks what has
merged so far.

## Packaging change (please note)

- The main library (`SDDev.Net.GenericRepository`) now references `SDDev.Net.GenericRepository.Contracts`
  via **ProjectReference** instead of a pinned NuGet `PackageReference`. The two packages are now
  versioned and published **in lockstep** (a single `Directory.Build.props` `<Version>` is the source
  of truth; the release workflow overrides it from the git tag). This was required so that changes to
  the Contracts public surface (e.g. the new `CancellationToken` overloads) compile and ship together.
- Consequence for consumers: install matching versions of both packages. The main package now declares
  a `Contracts >= <this version>` dependency, so a normal `dotnet add package` restores the right pair.

## Features

### CancellationToken support

`IRepository<T>` now exposes a `CancellationToken` overload for every asynchronous IO method:

```csharp
await repository.Get(id, partitionKey, cancellationToken);
await repository.Create(entity, cancellationToken);
await repository.Update(entity, cancellationToken);
await repository.Delete(id, partitionKey, force: false, cancellationToken);
// ...Upsert, Patch, Count, Get(predicate,...), Get(query,...), GetAll, FindOne
```

Design notes:

- The overloads are **default interface methods**, so existing external implementers of
  `IRepository<T>` continue to compile without changes (verified by a regression test).
- The `CancellationToken` parameter is **required** (no default value) on the new overloads, so they
  never collide with the existing tokenless members during overload resolution — existing call sites
  are unaffected.
- The token is threaded into the Cosmos SDK across **all** repositories — `GenericRepository<T>`
  (point operations *and* query methods: `Get(predicate)`, `Get(query)`, `GetAll`, `FindOne`) and
  `HierarchicalPartitionedRepository<T>` — as well as into the Azure Search and `IDistributedCache`
  calls of the `IndexedRepository` and `CachedRepository` decorators. A test verifies that a decorator
  forwards the caller's actual token to the inner repository.

## Still planned for 10.1.0

Request-options passthrough, `Query(QueryDefinition)` (vector/full-text/parameterized SQL), conditional
patch, change-feed processor, bulk/batch write helpers, diagnostics hook, nullable reference types on
Contracts, XML docs + SourceLink/symbols, formalized `Upsert` deprecation, the cache-expiration default
(deferred from 10.0.1), and the documentation overhaul.
