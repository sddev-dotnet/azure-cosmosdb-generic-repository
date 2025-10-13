using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using SDDev.Net.GenericRepository.Tests.TestModels;
using SDDev.Net.GenericRepository.Tests.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Tests;

[TestClass]
public class CosmosQueryExtensionTests
{
    [TestMethod]
    public async Task WhenUnitTestingMethodsThatUseCosmosQueryExtensions_ThenYouCanMockTheFeedIteratorToGetMoreRealisticResults()
    {
        // ARRANGE
        CosmosQueryExtensions.FeedIteratorProvider = new MockFeedIteratorProvider(1);

        var partitionKey = Guid.NewGuid().ToString();
        var testObject1 = new TestObject
        {
            Id = Guid.NewGuid(),
            Key = partitionKey,
            Number = 1,
        };
        var testObject2 = new TestObject
        {
            Id = Guid.NewGuid(),
            Key = partitionKey,
            Number = 2,
        };
        var queryable = new[] { testObject1, testObject2 }.AsQueryable();

        // ACT
        var asyncEnumerable = queryable.ToAsyncEnumerable();
        var list = await queryable.ToListAsync();
        var firstOrDefault = await queryable
            .OrderByDescending(x => x.Number)
            .FirstOrDefaultAsync();
        var singleOrDefault = await queryable
            .Where(x => x.Number == 1)
            .SingleOrDefaultAsync();

        // ASSERT
        var count = 0;
        await foreach (var result in asyncEnumerable)
        {
            result.Key.Should().Be(partitionKey);
            count++;
        }
        count.Should().Be(queryable.Count());

        list.Count.Should().Be(2);
        firstOrDefault.Id.Should().Be(testObject2.Id);
        singleOrDefault.Id.Should().Be(testObject1.Id);
    }
}
