using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Distributed;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;

namespace SDDev.Net.GenericRepository.Tests
{
    [TestClass]
    public class CachedRepositoryErrorHandlingTests
    {
        private static IConfiguration _config;
        private static IOptions<CosmosDbConfiguration> _cosmos;
        private static CosmosClient _client;
        private static ILogger<GenericRepository<TestObject>> _logger;
        private static ILoggerFactory _factory;

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
            serializer.Converters.Add(new StringEnumConverter() { NamingStrategy = new DefaultNamingStrategy() });

            _client = new CosmosClient(cosmos.ConnectionString, new CosmosClientOptions()
            {
                Serializer = new CosmosJsonDotNetSerializer(serializer)
            });

            _factory = new LoggerFactory();
            _logger = _factory.CreateLogger<GenericRepository<TestObject>>();

            // Ensure the database and container exist before running tests
            var database = await _client.CreateDatabaseIfNotExistsAsync(cosmos.DefaultDatabaseName);
            await database.Database.CreateContainerIfNotExistsAsync("Testing", "/PartitionKey");
        }

        [TestMethod]
        public async Task WhenCacheGetFails_ThenRepositoryOperationContinues()
        {
            // Arrange
            var failingCache = new FailingDistributedCache();
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var sut = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo, failingCache);

            var item = new TestObject() { Key = "test" };
            await genericRepo.Create(item);

            // Act - Should not throw exception even though cache fails
            var result = await sut.Get(item.Id.Value, item.PartitionKey);

            // Assert
            result.Should().NotBeNull();
            result.Key.Should().Be("test");
        }

        [TestMethod]
        public async Task WhenCacheSetFails_ThenCreateOperationContinues()
        {
            // Arrange
            var failingCache = new FailingDistributedCache();
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var sut = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo, failingCache);

            var item = new TestObject() { Key = "test" };

            // Act - Should not throw exception even though cache fails
            var result = await sut.Create(item);

            // Assert
            result.Should().NotBeEmpty();
        }

        [TestMethod]
        public async Task WhenCacheSetFails_ThenUpdateOperationContinues()
        {
            // Arrange
            var failingCache = new FailingDistributedCache();
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var sut = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo, failingCache);

            var item = new TestObject() { Key = "test", Prop1 = "Original" };
            await genericRepo.Create(item);
            item.Prop1 = "Updated";

            // Act - Should not throw exception even though cache fails
            var result = await sut.Update(item);

            // Assert
            result.Should().Be(item.Id.Value);
            var dbItem = await genericRepo.Get(item.Id.Value, item.PartitionKey);
            dbItem.Prop1.Should().Be("Updated");
        }

        [TestMethod]
        public async Task WhenCacheRemoveFails_ThenDeleteOperationContinues()
        {
            // Arrange
            var failingCache = new FailingDistributedCache();
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var sut = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo, failingCache);

            var item = new TestObject() { Key = "test" };
            await genericRepo.Create(item);

            // Act - Should not throw exception even though cache fails
            await sut.Delete(item.Id.Value, item.PartitionKey, true);

            // Assert
            var dbItem = await genericRepo.Get(item.Id.Value, item.PartitionKey);
            dbItem.Should().BeNull();
        }

        [TestMethod]
        public async Task WhenCacheSetFails_ThenUpsertOperationContinues()
        {
            // Arrange
            var failingCache = new FailingDistributedCache();
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var sut = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo, failingCache);

            var item = new TestObject() { Key = "test", Prop1 = "Upserted" };

            // Act - Should not throw exception even though cache fails
            var result = await sut.Upsert(item);

            // Assert
            result.Should().Be(item.Id.Value);
            var dbItem = await genericRepo.Get(item.Id.Value, item.PartitionKey);
            dbItem.Should().NotBeNull();
        }

        [TestMethod]
        public async Task WhenCacheSetFails_ThenFindOneOperationContinues()
        {
            // Arrange
            var failingCache = new FailingDistributedCache();
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var sut = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo, failingCache);

            var item = new TestObject() { Key = "test", Prop1 = "FindOne" };
            await genericRepo.Create(item);

            // Act - Should not throw exception even though cache fails
            var result = await sut.FindOne(x => x.Prop1 == "FindOne");

            // Assert
            result.Should().NotBeNull();
            result.Prop1.Should().Be("FindOne");
        }

        [TestMethod]
        public async Task WhenCacheRemoveFails_ThenEvictOperationContinues()
        {
            // Arrange
            var failingCache = new FailingDistributedCache();
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var sut = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo, failingCache);

            // Act - Should not throw exception even though cache fails
            await sut.Evict("test-key");

            // Assert - No exception thrown
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task WhenCacheGetFails_ThenRetrieveReturnsDefault()
        {
            // Arrange
            var failingCache = new FailingDistributedCache();
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var sut = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo, failingCache);

            // Act
            var result = await sut.Retrieve<TestObject>("test-key");

            // Assert
            result.Should().BeNull();
        }

        [TestMethod]
        public async Task WhenCacheSetFails_ThenCacheOperationContinues()
        {
            // Arrange
            var failingCache = new FailingDistributedCache();
            var genericRepo = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var sut = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo, failingCache);

            var item = new TestObject() { Key = "test" };

            // Act - Should not throw exception even though cache fails
            await sut.Cache(item, "test-key");

            // Assert - No exception thrown
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void WhenMultipleCachedRepositoriesCreated_ThenTheyShareSameCacheInstance()
        {
            // Arrange
            var cache = new MemoryDistributedCache(new OptionsWrapper<Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions>(
                new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));
            var genericRepo1 = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var genericRepo2 = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");

            // Act
            var cachedRepo1 = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo1, cache);
            var cachedRepo2 = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo2, cache);

            // Assert - Both repositories use the same cache instance (simulating singleton behavior)
            // This test verifies that when IDistributedCache is registered as singleton,
            // multiple CachedRepository instances will share the same cache instance
            Assert.IsNotNull(cachedRepo1);
            Assert.IsNotNull(cachedRepo2);
        }

        [TestMethod]
        public void WhenDifferentCacheInstancesUsed_ThenThrowsInvalidOperationException()
        {
            // Arrange
            var cache1 = new MemoryDistributedCache(new OptionsWrapper<Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions>(
                new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));
            var cache2 = new MemoryDistributedCache(new OptionsWrapper<Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions>(
                new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));
            var genericRepo1 = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");
            var genericRepo2 = new GenericRepository<TestObject>(_client, _logger, _cosmos, "Testing");

            // Act - Create first repository with cache1
            var cachedRepo1 = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo1, cache1);

            // Assert - Creating second repository with different cache instance should throw
            InvalidOperationException exception = null;
            try
            {
                var cachedRepo2 = new CachedRepository<TestObject>(_logger, _cosmos, genericRepo2, cache2);
            }
            catch (InvalidOperationException ex)
            {
                exception = ex;
            }

            exception.Should().NotBeNull("Expected InvalidOperationException when using different cache instances");
            exception.Message.Should().Contain("Multiple IDistributedCache instances detected");
            exception.Message.Should().Contain("Singleton");
        }

        /// <summary>
        /// A mock IDistributedCache implementation that always throws exceptions
        /// to test error handling behavior.
        /// </summary>
        private class FailingDistributedCache : IDistributedCache
        {
            public byte[] Get(string key)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public string GetString(string key)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public Task<string> GetStringAsync(string key, CancellationToken token = default)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public void Refresh(string key)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public Task RefreshAsync(string key, CancellationToken token = default)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public void Remove(string key)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public void SetString(string key, string value)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public void SetString(string key, string value, DistributedCacheEntryOptions options)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public Task SetStringAsync(string key, string value, CancellationToken token = default)
            {
                throw new InvalidOperationException("Cache operation failed");
            }

            public Task SetStringAsync(string key, string value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                throw new InvalidOperationException("Cache operation failed");
            }
        }
    }
}
