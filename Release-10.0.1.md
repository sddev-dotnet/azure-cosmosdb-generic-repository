# Release 10.0.1

A backward-compatible patch release: correctness fixes, dependency/security hygiene, and a repaired
CI/release pipeline. **No public interface signatures changed** and **no serialized document shapes
changed.** A few fixes alter observable *behavior* where the previous behavior was a defect; those are
called out under **Behavioral changes** below.

## Security & dependencies

- **AutoMapper** bumped to **16.2.0** across all projects, clearing the high-severity advisory
  **NU1903 (GHSA-rvv3-g6hj-g44x)** and the `NU1605` downgrade that prevented a clean restore.
- Removed vestigial `System.Linq` (4.3.0) and `Microsoft.CSharp` (4.7.0) package references — both are
  in-box on .NET 10 and were flagged `NU1510`.
- Removed the unused `Microsoft.AspNetCore.JsonPatch` reference and the empty `JsonPatchExtensions`
  class (only commented-out code remained).

## Correctness fixes

- **`Get(string query, ISearchModel)`**: sorting (`SortByField`) and offset paging (`Offset`) were
  silently ignored — the `OrderBy`/`Skip`/`Take` results were discarded (and paging was applied to the
  query string rather than the query). Both now take effect.
- **`FindOne`**: no longer drains every page of the result set before returning; it stops as soon as it
  has enough items (1 for `FirstOrDefault`, 2 for the `singleResult` uniqueness check).
- **`CosmosQueryExtensions.FirstOrDefaultAsync` / `SingleOrDefaultAsync`**: now iterate across pages
  instead of reading only the first page, so an empty leading page no longer returns `default`, and a
  second match on a later page is correctly detected by `SingleOrDefaultAsync`.
- **`CachedRepository`**:
  - `Update`/`Upsert` now write to the database **before** updating the cache, so a failed write can no
    longer leave stale data in the cache.
  - `Patch` now **evicts** the cached entity after a successful patch (previously the cache kept serving
    a stale copy).
  - `Create` now serializes with the polymorphic serializer settings, matching how cached entities are
    read back.
- **`IndexedRepository`**:
  - `UpdateIndex(IList<...>, ...)` batch overloads now use `Parallel.ForEachAsync` and are genuinely
    awaited — batch upload exceptions are observed and propagated instead of being lost.
  - `UpdateIndex(Guid, string)` no longer uses `ContinueWith(async …)`; the returned task now completes
    only after the index update actually runs.
  - `UpdateIndex(IList<T>, …)` now honors its `groupSize` parameter (and validates it, like the sibling
    overload) instead of hardcoding 900.
  - `Delete(Guid, …)` guards against a not-found entity instead of throwing a `NullReferenceException`.
  - Custom mapping now runs **both** the synchronous `AfterMapping` and the `AfterMappingAsync` hooks
    when both are subscribed (previously only the synchronous one ran).
- **`GenericRepository`**:
  - `Upsert`/`Update` no longer throw a `NullReferenceException` when an `IAuditableEntity` has a null
    `AuditMetadata` (matching `Create`'s existing guard).
  - `Delete(TModel, …)` no longer dereferences a null `model` inside its own null check.
  - Exceptions are now attached to `ILogger.LogError` calls (they were previously passed as a message
    argument and dropped by structured logging).
  - Removed a dead `Activator.CreateInstance<TModel>()` call from the constructors that imposed a hidden
    public-parameterless-constructor requirement on entity types.
- **`HierarchicalPartitionedRepository`**:
  - `Get(id, partitionKey)` now builds its partition-key filter with a **parameterized** Dynamic LINQ
    expression, so partition-key values containing quotes no longer break the query.
  - Continuation-token handling now matches `GenericRepository` (whitespace-guarded) and returns `null`
    (not `""`) when there are no further pages, so `while (token != null)` pagination loops terminate.
  - Removed a redundant `Client` property that shadowed the base repository's `Client`.
- **`CosmosQueryExtensions`**: added a correctly-spelled `InitializeDependencies` method; the previous
  misspelling (`InitializeDependenices`) is retained and forwards to it for backward compatibility.

## Behavioral changes (please review)

- **Sorting/paging on the string `Get` overload now work.** If you set `SortByField` on this overload,
  the sort is now applied — which may require an appropriate Cosmos index on the sort field. Queries
  that previously returned unsorted results without that index could now error until the index exists.
- **`IndexedRepository.UpdateIndex` batches now surface exceptions.** Calls that appeared to succeed
  instantly may now take longer (they actually await all uploads) and may throw exceptions that were
  previously swallowed.
- **`SingleOrDefaultAsync` may now throw** on result sets whose duplicates were split across pages
  (previously it silently returned the first item).

## Build / CI / release pipeline

- Added a `global.json` pinning the .NET 10 SDK band (`rollForward: latestFeature`).
- CI (`ci.yml`) now installs the correct SDK (via `global.json`), uses `actions/checkout@v4` /
  `actions/setup-dotnet@v4`, and **runs the unit tests** (`TestCategory!=INTEGRATION`).
- Live-Cosmos/Search test classes are now tagged `[TestCategory("INTEGRATION")]` so unit tests can run
  in CI without external dependencies.
- Release (`release.yml`) now:
  - accepts both `v1.2.3` and `1.2.3` tags,
  - derives the published **package version from the tag** (previously packages shipped as `1.0.0`),
  - checks out full history so the change-detection step works (previously a shallow checkout made it
    publish nothing),
  - pushes with `--skip-duplicate`.

## Deferred to 10.1.0

- Cached-entry expiration default (currently unbounded unless `refreshCache: true`) — deferred because
  it is a caching-semantics behavior change better introduced with a configuration gate.
- `Microsoft.AspNetCore.Http.Abstractions` → `FrameworkReference` — deferred because it introduces an
  ASP.NET Core shared-framework requirement for consumers.
