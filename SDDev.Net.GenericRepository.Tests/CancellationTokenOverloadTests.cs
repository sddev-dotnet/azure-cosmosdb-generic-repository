using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDDev.Net.GenericRepository.Caching;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.CosmosDB;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using SDDev.Net.GenericRepository.Tests.TestModels;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Tests;

[TestClass]
public class CancellationTokenOverloadTests
{
    /// <summary>
    /// A minimal external implementer that implements ONLY the tokenless members and none of the
    /// CancellationToken overloads. That it compiles at all proves the token overloads are default
    /// interface methods (i.e. adding them did not break existing implementers). The recorded flags
    /// prove the default implementations forward to the tokenless members.
    /// </summary>
    private sealed class TokenlessOnlyRepository : IRepository<TestObject>
    {
        public bool TokenlessGetByIdCalled;
        public bool TokenlessCreateCalled;

        public Task<TestObject> Get(Guid id, string partitionKey = null)
        {
            TokenlessGetByIdCalled = true;
            return Task.FromResult(new TestObject { Id = id });
        }

        public Task<Guid> Create(TestObject model)
        {
            TokenlessCreateCalled = true;
            return Task.FromResult(model.Id ?? Guid.NewGuid());
        }

        // Remaining members are not exercised by these tests.
        public Task<ISearchResult<TestObject>> Get(Expression<Func<TestObject, bool>> predicate, ISearchModel model) => throw new NotImplementedException();
        public Task<ISearchResult<TestObject>> Get(string query, ISearchModel model) => throw new NotImplementedException();
        public Task<ISearchResult<TestObject>> GetAll(Expression<Func<TestObject, bool>> predicate, ISearchModel model) => throw new NotImplementedException();
        public Task<TestObject> FindOne(Expression<Func<TestObject, bool>> predicate, string partitionKey = null, bool singleResult = false) => throw new NotImplementedException();
        public IQueryable<TestObject> Query(ISearchModel searchModel = null) => throw new NotImplementedException();
        public Task<int> Count(Expression<Func<TestObject, bool>> predicate, string partitionKey = null) => throw new NotImplementedException();
        public Task<Guid> Upsert(TestObject model) => throw new NotImplementedException();
        public Task<Guid> Update(TestObject model) => throw new NotImplementedException();
        public Task Patch(Guid id, string partitionKey, IPatchOperationCollection<TestObject> operationCollection) => throw new NotImplementedException();
        public Task Delete(Guid id, string partitionKey, bool force = false) => throw new NotImplementedException();
        public Task Delete(TestObject model, bool force = false) => throw new NotImplementedException();
    }

    [TestMethod]
    public async Task WhenExternalImplementerOmitsTokenOverloads_ThenTheyForwardToTheTokenlessMembers()
    {
        // ARRANGE
        IRepository<TestObject> repo = new TokenlessOnlyRepository();
        var id = Guid.NewGuid();

        // ACT — these CancellationToken overloads are default interface methods; the implementer
        // above provides no override for them.
        var entity = await repo.Get(id, "pk", CancellationToken.None);
        await repo.Create(new TestObject { Id = id }, CancellationToken.None);

        // ASSERT — the default implementations forwarded to the tokenless members.
        entity.Id.Should().Be(id);
        ((TokenlessOnlyRepository)repo).TokenlessGetByIdCalled.Should().BeTrue();
        ((TokenlessOnlyRepository)repo).TokenlessCreateCalled.Should().BeTrue();
    }

    /// <summary>
    /// Captures the CancellationToken passed to its token-accepting overloads so a test can assert
    /// that a decorator forwarded the caller's token (rather than dropping it / passing None).
    /// </summary>
    private sealed class TokenCapturingRepository : IRepository<TestObject>
    {
        public CancellationToken? CapturedCreateToken;
        public CancellationToken? CapturedGetToken;

        public Task<Guid> Create(TestObject model, CancellationToken cancellationToken)
        {
            CapturedCreateToken = cancellationToken;
            return Task.FromResult(model.Id ?? Guid.NewGuid());
        }

        public Task<TestObject> Get(Guid id, string partitionKey, CancellationToken cancellationToken)
        {
            CapturedGetToken = cancellationToken;
            return Task.FromResult<TestObject>(null);
        }

        // Tokenless members delegate to the capturing overloads; the rest are unused here.
        public Task<Guid> Create(TestObject model) => Create(model, CancellationToken.None);
        public Task<TestObject> Get(Guid id, string partitionKey = null) => Get(id, partitionKey, CancellationToken.None);
        public Task<ISearchResult<TestObject>> Get(Expression<Func<TestObject, bool>> predicate, ISearchModel model) => throw new NotImplementedException();
        public Task<ISearchResult<TestObject>> Get(string query, ISearchModel model) => throw new NotImplementedException();
        public Task<ISearchResult<TestObject>> GetAll(Expression<Func<TestObject, bool>> predicate, ISearchModel model) => throw new NotImplementedException();
        public Task<TestObject> FindOne(Expression<Func<TestObject, bool>> predicate, string partitionKey = null, bool singleResult = false) => throw new NotImplementedException();
        public IQueryable<TestObject> Query(ISearchModel searchModel = null) => throw new NotImplementedException();
        public Task<int> Count(Expression<Func<TestObject, bool>> predicate, string partitionKey = null) => throw new NotImplementedException();
        public Task<Guid> Upsert(TestObject model) => throw new NotImplementedException();
        public Task<Guid> Update(TestObject model) => throw new NotImplementedException();
        public Task Patch(Guid id, string partitionKey, IPatchOperationCollection<TestObject> operationCollection) => throw new NotImplementedException();
        public Task Delete(Guid id, string partitionKey, bool force = false) => throw new NotImplementedException();
        public Task Delete(TestObject model, bool force = false) => throw new NotImplementedException();
    }

    [TestMethod]
    public async Task WhenCallingThroughCachedRepository_ThenTheCallersTokenIsForwardedToTheInnerRepository()
    {
        // ARRANGE
        var inner = new TokenCapturingRepository();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cached = new CachedRepository<TestObject>(
            NullLogger<BaseRepository<TestObject>>.Instance,
            Options.Create(new CosmosDbConfiguration()),
            inner,
            cache);

        using var cts = new CancellationTokenSource();
        var model = new TestObject { Id = Guid.NewGuid() };

        // ACT
        await cached.Create(model, cts.Token);
        await cached.Get(Guid.NewGuid(), "pk", cts.Token);

        // ASSERT — the decorator threaded the caller's actual token, not CancellationToken.None.
        inner.CapturedCreateToken.Should().Be(cts.Token);
        inner.CapturedGetToken.Should().Be(cts.Token);
    }
}
