using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SDDev.Net.GenericRepository.CosmosDB;
using SDDev.Net.GenericRepository.Tests.TestModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;
using SDDev.Net.GenericRepository.Contracts.Search;

namespace SDDev.Net.GenericRepository.Tests;

[TestClass]
public class CosmosQueryExtensionNullDependenciesTests
{
    private static CosmosClient _client;
    private static ILogger<GenericRepository<TestObject>> _logger;
    private static IOptions<CosmosDbConfiguration> _cosmos;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        var configBuilder = new ConfigurationBuilder();

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var config = configBuilder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json", true, true)
            .AddEnvironmentVariables().Build();

        var section = config.GetSection("CosmosDb");
        _cosmos = new OptionsWrapper<CosmosDbConfiguration>(section.Get<CosmosDbConfiguration>());

        _logger = new LoggerFactory().CreateLogger<GenericRepository<TestObject>>();

        var serializer = new JsonSerializerSettings()
        {
            ContractResolver = new DefaultContractResolver(),
            TypeNameHandling = TypeNameHandling.Objects,
            Converters = { new StringEnumConverter() },
        };

        _client = new CosmosClient(_cosmos.Value.ConnectionString, new CosmosClientOptions()
        {
            Serializer = new CosmosJsonDotNetSerializer(serializer)
        });
    }

    private GenericRepository<TestObject> _testRepo;
    private Guid[] _ids;
    private string _partitionKey;
    private IQueryable<TestObject> _queryable;

    [TestInitialize]
    public async Task TestInitialize()
    {
        _testRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");

        // ARRANGE
        _partitionKey = Guid.NewGuid().ToString();
        _ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        await Task.WhenAll(
            _testRepo.Create(new() { Id = _ids[0], Key = _partitionKey }),
            _testRepo.Create(new() { Id = _ids[1], Key = _partitionKey }),
            _testRepo.Create(new() { Id = _ids[2], Key = _partitionKey }));

        _queryable = _testRepo.Query(new SearchModel()
        {
            PartitionKey = _partitionKey,
        });
    }

    [TestMethod]
    [TestCategory("INTEGRATION")]
    public async Task WhenCallingToListAsyncWithNoDependencies_ThenExtensionsDontFail()
    {

        // ACT
        var toListResult = await _queryable.ToListAsync();

        // ASSERT
        AssertConfigurationAndLoggerAreNull();
        toListResult.Select(x => x.Id).Should().BeEquivalentTo(_ids);
    }

    [TestMethod]
    [TestCategory("INTEGRATION")]
    public async Task WhenCallingFirstOrDefaultAsyncWithNoDependencies_ThenExtensionsDontFail()
    {

        // ACT
        var firstOrDefaultResult = await _queryable
            .Where(x => _ids.Contains(x.Id.Value))
            .FirstOrDefaultAsync();

        // ASSERT
        AssertConfigurationAndLoggerAreNull();
        _ids.Should().Contain(firstOrDefaultResult.Id.Value);
    }

    [TestMethod]
    [TestCategory("INTEGRATION")]
    public async Task WhenCallingSingleOrDefaultAsyncWithNoDependencies_ThenExtensionsDontFail()
    {

        // ACT
        var singleOrDefaultResult = await _queryable
            .Where(x => x.Id == _ids[1])
            .SingleOrDefaultAsync();

        // ASSERT
        AssertConfigurationAndLoggerAreNull();
        singleOrDefaultResult.Id.Should().Be(_ids[1]);
    }

    [TestMethod]
    [TestCategory("INTEGRATION")]
    public async Task WhenCallingToAsyncEnumerableWithNoDependencies_ThenExtensionsDontFail()
    {

        // ACT
        var asyncEnumerableResult = new List<TestObject>();
        await foreach (var result in _queryable.ToAsyncEnumerable())
        {
            asyncEnumerableResult.Add(result);
            // ASSERT
            _ids.Should().Contain(result.Id.Value);
        }
        AssertConfigurationAndLoggerAreNull();
    }

    private void AssertConfigurationAndLoggerAreNull()
    {
        var configField = typeof(CosmosQueryExtensions)
            .GetField("_configuration", BindingFlags.NonPublic | BindingFlags.Static);
        var loggerField = typeof(CosmosQueryExtensions)
            .GetField("_logger", BindingFlags.NonPublic | BindingFlags.Static);

        // Since the fields are static we can pass "null" as the object reference to GetValue
        configField.GetValue(null).Should().BeNull();
        loggerField.GetValue(null).Should().BeNull();
    }
}
