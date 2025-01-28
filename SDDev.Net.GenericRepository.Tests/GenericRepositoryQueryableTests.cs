using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using SDDev.Net.GenericRepository.CosmosDB;
using SDDev.Net.GenericRepository.Tests.TestModels;
using System.IO;
using System.Threading.Tasks;
using System;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using SDDev.Net.GenericRepository.Contracts.Search;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq.Expressions;

namespace SDDev.Net.GenericRepository.Tests;

[TestClass]
public class GenericRepositoryQueryableTests
{
    private static IConfiguration _config;
    private static IOptions<CosmosDbConfiguration> _cosmos;
    private static CosmosClient _client;
    private static ILogger<GenericRepository<TestObject>> _testLogger;
    private static ILoggerFactory _factory;

    private GenericRepository<TestObject> _testRepo;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        var configBuilder = new ConfigurationBuilder();

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        _config = configBuilder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json", true, true)
            .AddEnvironmentVariables().Build();

        var section = _config.GetSection("CosmosDb");
        var cosmos = section.Get<CosmosDbConfiguration>();

        _cosmos = new OptionsWrapper<CosmosDbConfiguration>(cosmos);

        var serializer = new JsonSerializerSettings()
        {
            ContractResolver = new DefaultContractResolver(),
            TypeNameHandling = TypeNameHandling.Objects,
            Converters = { new StringEnumConverter() },
        };

        _client = new CosmosClient(cosmos.ConnectionString, new CosmosClientOptions()
        {
            Serializer = new CosmosJsonDotNetSerializer(serializer)
        });

        _factory = new LoggerFactory();
        _testLogger = _factory.CreateLogger<GenericRepository<TestObject>>();
        CosmosQueryExtensions.InitializeDependenices(_testLogger, _cosmos);
    }

    [TestInitialize]
    public async Task TestInit()
    {
        _testRepo = new GenericRepository<TestObject>(_client, _testLogger, _cosmos, "Testing");
    }

    [TestMethod]
    [TestCategory("INTEGRATION")]
    public async Task WhenUsingCountQueryable_ThenCountIsReturned()
    {
        // Arrange
        var testKey = Guid.NewGuid().ToString();
        var prop1 = Guid.NewGuid().ToString();
        try
        {
            var createTasks = new List<Task>();
            for (var i = 0; i < Random.Shared.Next(1, 5); i++)
            {
                createTasks.Add(
                    _testRepo.Create(new TestObject
                    {
                        Key = testKey,
                        ExampleProperty = "Test",
                        Collection = new List<string> { "Test1", "Test2" },
                        Prop1 = prop1,
                    }));
            }

            await Task.WhenAll(createTasks);

            // Act
            var count = (int)await _testRepo
                .Query(new SearchModel()
                {
                    PartitionKey = testKey,
                })
                .CountAsync();

            // Assert
            count.Should().Be(createTasks.Count);
        }
        finally
        {
            await Cleanup(x => x.PartitionKey == testKey);
        }
    }



    [TestMethod]
    [TestCategory("INTEGRATION")]
    public async Task WhenUsingPartitionKey_ThenPartitionedResultsAreReturned()
    {
        // Arrange
        var partitionKey = Guid.NewGuid().ToString();
        var testKey1 = Guid.NewGuid().ToString();
        var testKey2 = Guid.NewGuid().ToString();

        var testObjects = new List<TestObject>()
        {
            new TestObject
            {
                Key = partitionKey,
                ExampleProperty = testKey1,
            },
            new TestObject
            {
                Key = Guid.NewGuid().ToString(),
                ExampleProperty = testKey2,
            },
        };

        try
        {
            await Task.WhenAll(
                testObjects.Select(
                    _testRepo.Create));

            // Act
            var results = await _testRepo
                .Query(new SearchModel()
                {
                    PartitionKey = partitionKey,
                })
                .Where(x => x.ExampleProperty == testKey1
                    || x.ExampleProperty == testKey2)
                .ToListAsync();

            // Assert
            results.Count.Should().Be(1);
        }
        finally
        {
            await Cleanup(
                x => x.ExampleProperty == testKey1
                || x.ExampleProperty == testKey2);
        }
    }

    [TestMethod]
    [TestCategory("INTEGRATION")]
    public async Task WhenNotUsingPartitionKey_ThenAllResultsAreReturned()
    {
        // Arrange
        var testKey1 = Guid.NewGuid().ToString();
        var testKey2 = Guid.NewGuid().ToString();

        var testObjects = new List<TestObject>()
        {
            new TestObject
            {
                Key = Guid.NewGuid().ToString(),
                ExampleProperty = testKey1,
            },
            new TestObject
            {
                Key = Guid.NewGuid().ToString(),
                ExampleProperty = testKey2,
            },
        };

        try
        {
            await Task.WhenAll(
                testObjects.Select(
                    _testRepo.Create));

            // Act
            var results = await _testRepo
                .Query()
                .Where(x => x.ExampleProperty == testKey1
                    || x.ExampleProperty == testKey2)
                .ToListAsync();
            //_testRepo.Get(x => x.)
            // Assert
            results.Count.Should().Be(2);
        }
        finally
        {
            await Cleanup(
                x => x.ExampleProperty == testKey1
                || x.ExampleProperty == testKey2);
        }
    }

    [TestMethod]
    [TestCategory("INTEGRATION")]
    public async Task WhenProjectingTheDocument_ThenOnlySpecificResultsAreReturned()
    {
        // Arrange
        var testObject = new TestObject
        {
            Key = Guid.NewGuid().ToString(),
            ExampleProperty = Guid.NewGuid().ToString(),
        };

        await _testRepo.Create(testObject);

        try
        {
            // Act
            var result = await _testRepo
                .Query(new SearchModel()
                {
                    PartitionKey = testObject.PartitionKey,
                })
                .Select(x => new
                {
                    x.ExampleProperty,
                })
                .FirstOrDefaultAsync();

            // Assert
            result.ExampleProperty.Should().Be(testObject.ExampleProperty);
        }
        finally
        {
            await Cleanup(x => x.PartitionKey == testObject.PartitionKey);
        }
    }

    [TestMethod]
    public async Task WhenProjectingTheDocument_ThenQueryShouldOnlyRequestSpecificFieldsWithinTheItemType()
    {
        // Arrange
        // Act
        var query = _testRepo
            .Query()
            .Select(x => x.ExampleProperty)
            .ToString();

        // Assert
        query.Should().Be(@"{""query"":""SELECT VALUE root[\""ExampleProperty\""] FROM root WHERE ARRAY_CONTAINS(root[\""ItemType\""], \""TestObject\"")""}");
    }

    private async Task Cleanup(Expression<Func<TestObject, bool>> expression)
    {
        var existingObjects = await _testRepo
                .Query()
                .Where(expression)
                .ToListAsync();

        await Task.WhenAll(existingObjects.Select(x => _testRepo.Delete(x, force: true)));
    }
}
