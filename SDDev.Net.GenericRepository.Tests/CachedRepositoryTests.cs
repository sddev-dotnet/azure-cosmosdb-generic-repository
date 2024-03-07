using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using SDDev.Net.GenericRepository.Caching;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using SDDev.Net.GenericRepository.CosmosDB;
using SDDev.Net.GenericRepository.Tests.TestModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using static Azure.Core.HttpHeader;
using Microsoft.Extensions.Caching.Memory;
using FluentAssertions;

namespace SDDev.Net.GenericRepository.Tests
{
    [TestClass]
    public class CachedRepositoryTests
    {
        private static IConfiguration _config;
        private static IOptions<CosmosDbConfiguration> _cosmos;
        private static CosmosClient _client;
        private static ILogger<GenericRepository<TestObject>> _logger;
        private static ILoggerFactory _factory;
        private CachedRepository<TestObject> _sut;
        private IDistributedCache _cache;

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
                Converters = new List<JsonConverter>() { new StringEnumConverter() }
            };
            //Serialize enums as string using default naming strategy (unchanged)
            serializer.Converters.Add(new StringEnumConverter() { NamingStrategy = new DefaultNamingStrategy() });

            _client = new CosmosClient(cosmos.ConnectionString, new CosmosClientOptions()
            {
                Serializer = new CosmosJsonDotNetSerializer(serializer)
            });

            _factory = new LoggerFactory();
            _logger = _factory.CreateLogger<GenericRepository<TestObject>>();
        }

        [TestInitialize]
        public async Task TestInit()
        {
            var opts = new MemoryDistributedCacheOptions();
            var options = new OptionsWrapper<MemoryDistributedCacheOptions>(opts);
            _cache = new MemoryDistributedCache(options);
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            _sut = new CachedRepository<TestObject>( _logger, _cosmos, genericRepo, _cache);
        }

        [TestMethod]
        public async Task WhenCreateItem_ThenCached()
        {
            // Arrange



            // Act
            var item = await _sut.Create(new TestObject() { Key = "test" });

            // Assert
            var cacheItem = await _cache.GetAsync(item.ToString());
            cacheItem.Should().NotBeNull();
        }

        [TestMethod]
        public async Task WhenGetItemFromCache_ThenItemReturnedFromCache()
        {
            // Arrange
            var item = new TestObject() { Key = "test" };
            await _sut.Create(item);

            // Act
            var result = await _sut.Get(item.Id.Value, item.PartitionKey);

            // Assert
            result.Should().NotBeNull();
        }

        [TestMethod]
        public async Task WhenItemDoesNotExistInCache_ThenRetrievedFromDB()
        {
            // Arrange
            var item = new TestObject() { Key = "test" };
            await _sut.Create(item);
            await _cache.RemoveAsync(item.Id.ToString());

            // Act
            var result = await _sut.Get(item.Id.Value, item.PartitionKey);

            // Assert
            result.Should().NotBeNull();
        }

        [TestMethod]
        public async Task WhenItemUpdated_ThenCacheUpdated()
        {
            // Arrange
            var item = new TestObject() { Key = "test", Prop1 = "Testing" };
            await _sut.Create(item);
            item.Prop1 = "Test Changed";

            // Act
            await _sut.Update(item);

            // Assert
            var result = await _cache.GetStringAsync(item.Id.ToString());
            var testResult = JsonConvert.DeserializeObject<TestObject>(result);


            testResult.Prop1.Should().Be("Test Changed");
        }

        // Create a test method that tests the ICachedRepository CacheItem method
        [TestMethod]
        public async Task WhenCacheItem_ThenItemCached()
        {
            // Arrange
            var item = new TestObject() { Key = "test", Prop1 = "Testing" };
            var cacheItem = Guid.NewGuid();

            // Act
            await _sut.Cache(item, cacheItem.ToString());

            // Assert
            var result = await _cache.GetStringAsync(cacheItem.ToString());
            var testResult = JsonConvert.DeserializeObject<TestObject>(result);

            testResult.Prop1.Should().Be("Testing");
        }

        // Create a test method that retrieves an item from the cache using the Retrieve function
        [TestMethod]
        public async Task WhenRetrieveItem_ThenItemRetrieved()
        {
            // Arrange
            var item = new TestObject() { Key = "test", Prop1 = "Testing" };
            var cacheItem = Guid.NewGuid();
            await _sut.Cache(item, cacheItem.ToString());

            // Act
            var result = await _sut.Retrieve<TestObject>(cacheItem.ToString());

            // Assert
            result.Prop1.Should().Be("Testing");
        }

        // Create a test method that removes an item from the cache using the Evict function
        [TestMethod]
        public async Task WhenEvictItem_ThenItemRemoved()
        {
            // Arrange
            var item = new TestObject() { Key = "test", Prop1 = "Testing" };
            var cacheItem = Guid.NewGuid();
            await _sut.Cache(item, cacheItem.ToString());

            // Act
            await _sut.Evict(cacheItem.ToString());

            // Assert
            var result = await _cache.GetStringAsync(cacheItem.ToString());
            result.Should().BeNull();
        }
    }
}
