using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using SDDev.Net.GenericRepository.CosmosDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using SDDev.Net.GenericRepository.Tests.TestModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Azure;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Indexing;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.Indexing;
using FluentAssertions;

namespace SDDev.Net.GenericRepository.Tests
{
    [TestClass]
    public class IndexedRepositoryTests
    {
        private static IConfiguration _config;
        private static IOptions<CosmosDbConfiguration> _cosmos;
        private static CosmosClient _client;
        private static ILogger<GenericRepository<TestObject>> _logger;
        private static ILoggerFactory _factory;
        private static IServiceCollection _services;

        private IServiceProvider _provider;
        private IIndexedRepository<BaseTestObject, BaseTestIndexModel> _sut;

        [ClassInitialize]
        public static void Init(TestContext context)
        {
            var configBuilder = new ConfigurationBuilder();
            _services = new ServiceCollection();

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
            //Serialize enums as string using default naming strategy (unchanged)
            
            
            _services.AddLogging((x) =>
            {
                x.ClearProviders();
                x.SetMinimumLevel(LogLevel.Debug);
            });
            _services.AddSingleton<IConfiguration>(_config);
            _services.AddAzureClients(builder =>
            {

                builder.AddSearchIndexClient(
                    new System.Uri(_config.GetValue<string>("Search:Uri")),
                    new AzureKeyCredential(_config.GetValue<string>("Search:Key"))
                ).WithName("test");

                builder.AddSearchClient(
                    new System.Uri(_config.GetValue<string>("Search:Uri")),
                    "test",
                    new AzureKeyCredential(_config.GetValue<string>("Search:Key"))
                ).WithName("testing");             
            });
            _services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies().Where(x => x.FullName.StartsWith("SDDev.Net")));

            _client = new CosmosClient(cosmos.ConnectionString, new CosmosClientOptions()
            {
                Serializer = new CosmosJsonDotNetSerializer(serializer)
            });

            _factory = new LoggerFactory();
            _logger = _factory.CreateLogger<GenericRepository<TestObject>>();

            _services.AddSingleton(_cosmos);
            _services.AddSingleton(_client);
            _services.AddScoped<IRepository<BaseTestObject>, CosmosDB.GenericRepository<BaseTestObject>>(x =>
            {
                var client = x.GetService<CosmosClient>();
                var logger = x.GetService<ILogger<GenericRepository<BaseTestObject>>>();
                var options = x.GetService<IOptions<CosmosDbConfiguration>>();

                return new GenericRepository<BaseTestObject>(client, logger, options, "Testing", "kinectify");
            });
            _services.AddScoped<IIndexedRepository<BaseTestObject, BaseTestIndexModel>, IndexedRepository<BaseTestObject, BaseTestIndexModel>>();
        }

        [TestInitialize]
        public async Task TestInit()
        {
            
            _provider = _services.BuildServiceProvider();
            _sut = _provider.GetService<IIndexedRepository<BaseTestObject, BaseTestIndexModel>>();
            _sut.Initialize("testing", _provider.GetService<IRepository<BaseTestObject>>(), new IndexRepositoryOptions()
            {
                CreateOrUpdateIndex = true,
                IndexName = "test",
                RemoveOnLogicalDelete = false
            });

            _sut.AfterMappingAsync += async (BaseTestIndexModel, BaseTestObject) =>
            {
                await Task.FromResult(true);
            };
        }

        [TestMethod]
        public async Task WhenCreatingIndex_ThenIndexCreated()
        {
            // Arrange
            

            // Act
            await _sut.CreateOrUpdateIndex();

            // Assert

        }

        [TestMethod]
        public async Task WhenObjectCreated_ThenIndexed()
        {
            // Arrange
            var test = new BaseTestObject()
            {
                Name = "Create Test"
            };

            // act
            var result = await _sut.Create(test);

            // Assert
        }

        [TestMethod]
        public async Task WhenUpdatingObject_ThenUpdated()
        {
            // Arrange
            var test = new BaseTestObject()
            {
                Name = "Create Test"
            };

            var result = await _sut.Create(test);
            test.Name = "Modified 2";

            // Act
            var resp = await _sut.Update(test);

            // Assert
        }

        [TestMethod]
        public async Task WhenRequestingFacets_ThenFacetCountsReturned()
        {
            // Arrange
            var req = new SearchRequest()
            {
                Options = new Azure.Search.Documents.SearchOptions()

            };
            req.Options.Facets.Add("Name");

            // Act
            var result = await _sut.Search(req);

            // Assert
            result.Metadata.Facets.Count().Should().BeGreaterThan(0);
        }

        [TestMethod]
        public async Task WhenDeletingWithOutForce_ThenRemoved()
        {
            // Arrange
            var test = new BaseTestObject()
            {
                Name = "Create Test 3"
            };

            var result = await _sut.Create(test);

            // Act
            await _sut.Delete(test.Id.Value, "BaseTestObject", false);

            // Assert
        }

        [TestMethod]
        public async Task WhenDeletingWithForce_ThenRemoved()
        {
            // Arrange
            var test = new BaseTestObject()
            {
                Name = "Logical Delete Keep"
            };

            var result = await _sut.Create(test);

            // Act
            await _sut.Delete(test.Id.Value, "BaseTestObject", false);

            // Assert
        }

        [TestMethod]
        public async Task WhenDeletingForce_ThenRemoved()
        {
            // Arrange
            var test = new BaseTestObject()
            {
                Name = "Force Delete Gone"
            };

            var result = await _sut.Create(test);

            // Act
            await _sut.Delete(test.Id.Value, "BaseTestObject", true);

            // Assert
        }
    }
}
