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
        private static IDistributedCache _sharedCache;
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

            // Initialize a single shared cache instance (simulate Singleton registration)
            var opts = new MemoryDistributedCacheOptions();
            var options = new OptionsWrapper<MemoryDistributedCacheOptions>(opts);
            _sharedCache = new MemoryDistributedCache(options);
        }

        [TestInitialize]
        public async Task TestInit()
        {
            // Reuse the shared cache instance across tests
            _cache = _sharedCache;
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
            var cacheKey = $"TestObject:{item}";
            var cacheItem = await _cache.GetAsync(cacheKey);
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
            var cacheKey = $"TestObject:{item.Id}";
            await _cache.RemoveAsync(cacheKey);

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
            var cacheKey = $"TestObject:{item.Id}";
            var result = await _cache.GetStringAsync(cacheKey);
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

        [TestMethod]
        public async Task WhenDifferentEntityTypesWithSameId_ThenCacheKeysDoNotCollide()
        {
            // Arrange
            var sharedId = Guid.NewGuid();
            var testObject = new TestObject() { Key = "test", Prop1 = "TestObjectValue" };
            testObject.Id = sharedId;

            var anotherTestObject = new AnotherTestObject() { Name = "AnotherTestObjectValue", Prop1 = "AnotherProp1" };
            anotherTestObject.Id = sharedId;

            // Use the same shared cache instance to satisfy singleton validation
            var sharedCache = _sharedCache;

            var testObjectLogger = _factory.CreateLogger<GenericRepository<TestObject>>();
            var anotherTestObjectLogger = _factory.CreateLogger<GenericRepository<AnotherTestObject>>();
            var testObjectRepo = new GenericRepository<TestObject>(_client, testObjectLogger, _cosmos, "Testing");
            var anotherTestObjectRepo = new GenericRepository<AnotherTestObject>(_client, anotherTestObjectLogger, _cosmos, "Testing");

            var cachedTestObjectRepo = new CachedRepository<TestObject>(testObjectLogger, _cosmos, testObjectRepo, sharedCache);
            var cachedAnotherTestObjectRepo = new CachedRepository<AnotherTestObject>(anotherTestObjectLogger, _cosmos, anotherTestObjectRepo, sharedCache);

            // Act - Create both entities with the same ID
            await cachedTestObjectRepo.Create(testObject);
            await cachedAnotherTestObjectRepo.Create(anotherTestObject);

            // Assert - Verify both can be retrieved independently with correct values
            var retrievedTestObject = await cachedTestObjectRepo.Get(sharedId, testObject.PartitionKey);
            var retrievedAnotherTestObject = await cachedAnotherTestObjectRepo.Get(sharedId, anotherTestObject.PartitionKey);

            retrievedTestObject.Should().NotBeNull();
            retrievedTestObject.Prop1.Should().Be("TestObjectValue");

            retrievedAnotherTestObject.Should().NotBeNull();
            retrievedAnotherTestObject.Name.Should().Be("AnotherTestObjectValue");
            retrievedAnotherTestObject.Prop1.Should().Be("AnotherProp1");

            // Verify cache keys are prefixed with type names
            var testObjectCacheKey = $"TestObject:{sharedId}";
            var anotherTestObjectCacheKey = $"AnotherTestObject:{sharedId}";

            var testObjectCacheValue = await sharedCache.GetStringAsync(testObjectCacheKey);
            var anotherTestObjectCacheValue = await sharedCache.GetStringAsync(anotherTestObjectCacheKey);

            testObjectCacheValue.Should().NotBeNull("TestObject should be cached with type-prefixed key");
            anotherTestObjectCacheValue.Should().NotBeNull("AnotherTestObject should be cached with type-prefixed key");

            // Verify the cache entries are different
            testObjectCacheValue.Should().NotBe(anotherTestObjectCacheValue, "Different entity types should have different cache entries");
        }
    }
}
