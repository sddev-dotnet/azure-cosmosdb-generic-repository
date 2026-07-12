using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using SDDev.Net.GenericRepository.Contracts.Search;
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
}
