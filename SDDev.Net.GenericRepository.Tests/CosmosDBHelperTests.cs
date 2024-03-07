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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDDev.Net.GenericRepository.Utilities;

namespace SDDev.Net.GenericRepository.Tests
{
    [TestClass]
    public class CosmosDBHelperTests
    {
        private static IConfiguration _config;
        private static IOptions<CosmosDbConfiguration> _cosmos;
        private static CosmosClient _client;
        private static ILogger<GenericRepository<TestObject>> _logger;
        private static ILoggerFactory _factory;

        private TestRepo _testRepo;

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

        //[ClassCleanup]
        //public static async Task Cleanup()
        //{
        //    await _client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(_cosmos.Value.DefaultDatabaseName, "TestObject"));
        //    _client.Dispose();
        //}


        [TestInitialize]
        public async Task TestInit()
        {

            _testRepo = new TestRepo(_client, _logger, _cosmos, "Testing");
        }

        [TestMethod]
        public async Task GetCosmosResults()
        {
            var results = await CosmosDBHelpers.GetCosmosResults("SELECT * FROM c", _testRepo.Client);
            Assert.IsNotNull(results);
        }

        [TestMethod]
        public async Task TestProjectingOutput()
        {
            var results = await CosmosDBHelpers.GetCosmosResults("SELECT c.id, c.Name FROM c", _testRepo.Client);
            Assert.IsNotNull(results);
        }

        [TestMethod]
        public async Task TestAggregatingData()
        {
            var results = await CosmosDBHelpers.GetCosmosResults("SELECT COUNT(1) total, c.Prop1 FROM c GROUP BY c.Prop1", _testRepo.Client);
            Assert.IsNotNull(results);
        }

        [TestMethod]
        public async Task TestScalarValue()
        {
            var results = await CosmosDBHelpers.GetScalarValue("SELECT VALUE COUNT(c.id) FROM c", _testRepo.Client);
            Assert.IsTrue(results > 0);
        }
    }
}
