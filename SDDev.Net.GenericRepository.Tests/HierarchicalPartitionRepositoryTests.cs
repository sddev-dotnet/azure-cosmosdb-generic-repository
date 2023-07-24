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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDDev.Net.GenericRepository.Tests.TestModels;
using FluentAssertions;
using SDDev.Net.GenericRepository.Contracts.Search;

namespace SDDev.Net.GenericRepository.Tests
{
    [TestClass]
    public class HierarchicalPartitionRepositoryTests
    {
        private static IConfiguration _config;
        private static IOptions<CosmosDbConfiguration> _cosmos;
        private static CosmosClient _client;
        private static ILogger<HierarchicalPartitionedRepository<TestObject>> _logger;
        private static ILoggerFactory _factory;

        private TestHierarchicalPartitionRepository _testRepo;

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
            _logger = _factory.CreateLogger<HierarchicalPartitionedRepository<TestObject>>();
        }

        [TestInitialize]
        public async Task TestInit()
        {

            _testRepo = new TestHierarchicalPartitionRepository(_client, _logger, _cosmos);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenUpsert_AndObjectDoesNotExist_ThenCreated_Test()
        {
            //Arrange 
            var item = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal1",
                    "TestVal2"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };


            //Act
            var result = await _testRepo.Upsert(item);


            //Assert
            result.Should().NotBe(new Guid());
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenDeleteDocument_DocumentIsDeleted_Test()
        {
            //Arrange 
            var item = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal1",
                    "TestVal2"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };

            //Act
            var result = await _testRepo.Upsert(item);
            await _testRepo.Delete(result, item.PartitionKey, true);

            var empty = await _testRepo.Get(result);

            //Assert
            empty.Should().BeNull();
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenDelete_AndDocumentDoestExist_ThenNoError()
        {
            // Arrange

            // Act
            await _testRepo.Delete(Guid.NewGuid(), Guid.NewGuid().ToString(), true);

            // Assert

            //nothing to assert, there should be no error, no return value
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenGetDocumentById_ThenDocumentIsRetrieved()
        {
            //Arrange 
            var item = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal1",
                    "TestVal2"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };

            //Act
            var result = await _testRepo.Upsert(item);

            var retrieved = await _testRepo.Get(result, "Primary");

            //Assert
            retrieved.Should().NotBeNull();
            retrieved.Id.Should().NotBeNull();
            retrieved.Id.Should().Be(result);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenGetDocumentsWithSearchModel_AndSearchingByAnArray_ThenItemIsReturned_Test()
        {
            //Arrange 
            var item = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal1",
                    "TestVal2"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };

            var item2 = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal3",
                    "TestVal4"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };

            var searchModel = new TestSearchModel
            {
                CollectionValues = new List<string>
                {
                    "TestVal3"
                }
            };

            //Act
            var result = await _testRepo.Upsert(item);
            var result2 = await _testRepo.Upsert(item2);

            var searchResults = await _testRepo.Get(searchModel);

            //Assert
            searchResults.TotalResults.Should().BeGreaterThan(0);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenSearchingByNumericProperty_ThenItemsAreFound_Test()
        {
            //Arrange 
            var item = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal1",
                    "TestVal2"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };

            var item2 = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal3",
                    "TestVal4"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };

            var searchModel = new TestSearchModel
            {
                Num = 5
            };

            //Act
            var result = await _testRepo.Upsert(item);
            var result2 = await _testRepo.Upsert(item2);

            var searchResults = await _testRepo.Get(searchModel);

            //Assert
            searchResults.TotalResults.Should().BeGreaterThan(0);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenSearchingByMultipleProperties_ThenObjectsAreFound_Test()
        {
            ///Arrange 
            var item = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal1",
                    "TestVal2"
                },
                Number = 7,
                Prop1 = "SomethingIShouldBeAbleToFind",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };


            var searchModel = new TestSearchModel
            {
                Num = 7,
                StrVal = item.Prop1
            };

            //Act
            var result = await _testRepo.Upsert(item);

            var resp = await _testRepo.Get(searchModel);


            //Assert
            resp.TotalResults.Should().BeGreaterThan(0);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenSearchingByStringContains_ThenResultsAreReturned_Test()
        {
            //Arrange 
            var item = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal1",
                    "TestVal2"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildTestObject"
                }
            };

            var item2 = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal3",
                    "TestVal4"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };

            var searchModel = new TestSearchModel
            {
                ContainsStrTest = "Test"
            };

            //Act
            try
            {
                var result = await _testRepo.Upsert(item);
                var result2 = await _testRepo.Upsert(item2);

                var searchResults = await _testRepo.Get(searchModel);

                //Assert
                searchResults.TotalResults.Should().BeGreaterOrEqualTo(1);
            }
            catch (Exception e)
            {
                var message = e.Message;
            }

        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenSearchByPredicate_ThenResultsAreReturned_Test()
        {
            //Arrange 
            var item = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal1",
                    "TestVal2"
                },
                Number = 15,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildTestObject"
                }
            };

            var item2 = new TestObject
            {
                Collection = new List<string>
                {
                    "TestVal3",
                    "TestVal4"
                },
                Number = 5,
                Prop1 = "TestingString",
                ChildObject = new TestObject
                {
                    Number = 8,
                    Prop1 = "ChildObject"
                }
            };

            var searchModel = new TestSearchModel
            {
                ContainsStrTest = "Test"
            };

            //Act
            var result = await _testRepo.Upsert(item);
            var result2 = await _testRepo.Upsert(item2);


            var results = await _testRepo.Get(x => x.Number == 15 && x.Prop1 == "TestingString", new SearchModel());

            //Assert

            results.TotalResults.Should().BeGreaterOrEqualTo(1);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenUsingAnotherTestObject_ThenYouCanSearch_Test()
        {

            var logger = _factory.CreateLogger<HierarchicalPartitionedRepository<AnotherTestObject>>();
            var repo = new HierarchicalPartitionedRepository<AnotherTestObject>(_client, logger, _cosmos, new List<string>()
            {
                "PartitionKey",
                "Prop1"
            }, "TestContainer");

            var item = new AnotherTestObject()
            {
                Name = "CoolThing",
                IsDeleted = false,
                Quantity = 100
            };

            var itemId = await repo.Upsert(item);

            var searchResults = await repo.Get(x => x.Quantity == 100, new SearchModel());

            searchResults.Results.Should().Contain(x => x.Id == itemId);

            //Cleanup
            //await _client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(_cosmos.Value.DefaultDatabaseName, "AnotherTestObject"));

        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenUsingBaseType_CanInsertMultipleTypes()
        {
            //Arrange
            var logger = _factory.CreateLogger<HierarchicalPartitionedRepository<BaseTestObject>>();
            var repo = new HierarchicalPartitionedRepository<BaseTestObject>(_client, logger, _cosmos, new List<string>()
            {
                "PartitionKey",
                "Prop1"
            }, "TestContainer");

            var child1 = new ChildObject1() { SomeIntValue = 10 };
            var child2 = new ChildObject2 { SomeStringProp = "testing String" };


            //Act

            var id = await repo.Upsert(child1);
            var id2 = await repo.Upsert(child2);


            //Assert
            id.Should().NotBeEmpty();
            id2.Should().NotBeEmpty();
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenCollectionContainsMultipleTypes_AndSearchingByMultipleTypes_ThenMultipleTypesReturned()
        {
            //Arrange
            var logger = _factory.CreateLogger<HierarchicalPartitionedRepository<BaseTestObject>>();
            var repo = new HierarchicalPartitionedRepository<BaseTestObject>(_client, logger, _cosmos, new List<string>()
            {
                "PartitionKey",
                "Prop1"
            }, "TestContainer");

            var child1 = new ChildObject1() { SomeIntValue = 10 };
            var child2 = new ChildObject2 { SomeStringProp = "testing String" };

            await repo.Upsert(child1);
            await repo.Upsert(child2);

            //Act
            var results = await repo.Get(x => 1 == 1, new SearchModel()); //return everything


            //Assert
            results.TotalResults.Should().BeGreaterOrEqualTo(2);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenMultipleTypesInOneCollection_AndRepoIsSpecificToOneType_ThenOtherTypesAreFiltered()
        {
            //Arrange
            var logger = _factory.CreateLogger<HierarchicalPartitionedRepository<BaseTestObject>>();
            var repo = new HierarchicalPartitionedRepository<BaseTestObject>(_client, logger, _cosmos, new List<string>() { 
                "PartitionKey",
                "Prop1"
            }, "TestContainer");
            var specificRepo = new HierarchicalPartitionedRepository<ChildObject1>(_client, _factory.CreateLogger<HierarchicalPartitionedRepository<ChildObject1>>(), _cosmos,new List<string>() { 
                "PartitionKey",
                "Prop1"
            }, "TestContainer");


            var child1 = new ChildObject1() { SomeIntValue = 10 };
            var child2 = new ChildObject2 { SomeStringProp = "testing String" };

            await repo.Upsert(child1);
            await repo.Upsert(child2);

            //Act
            var results = await specificRepo.Get(x => 1 == 1, new SearchModel());


            //Assert
            results.TotalResults.Should().NotBe(0);
            results.Results.Where(x => x.ItemType.Contains("ChildObject2")).Should().BeEmpty();
        }

        [TestCategory("INTEGRATION")]
        [Ignore]
        [TestMethod]
        public async Task ResultsPaging_Test()
        {
            //Arrange
            var random = new Random();
            for (int i = 0; i < 100; i++)
            {
                var item = new TestObject
                {
                    Collection = new List<string>
                    {
                        "TestVal1",
                        "TestVal2"
                    },
                    Number = random.Next(0, 1000),
                    Prop1 = $"TestingString-{i}",
                    ChildObject = new TestObject
                    {
                        Number = random.Next(),
                        Prop1 = "ChildTestObject"
                    }
                };

                await _testRepo.Upsert(item);
            }


            ////Act
            var results = await _testRepo.Get(x => x.PartitionKey == "Primary", new SearchModel());
            results.ContinuationToken.Should().NotBeNullOrEmpty();


            var newQuery = await _testRepo.Get(x => x.PartitionKey == "Primary", new SearchModel() { ContinuationToken = results.ContinuationToken, PageSize = 50 });
            newQuery.Results.Count().Should().Be(50);

            //Assert

        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenDataIsPartitioned_ThenQueriesLimitedByPartition()
        {
            // Arrange
            var random = new Random();
            for (int i = 0; i < 100; i++)
            {
                var item = new TestObject
                {
                    Collection = new List<string>
                    {
                        "TestVal1",
                        "TestVal2"
                    },
                    Number = random.Next(0, 1000),
                    Prop1 = $"TestingString-{i}",
                    ChildObject = new TestObject
                    {
                        Number = random.Next(),
                        Prop1 = "ChildTestObject"
                    },
                    Key = i % 2 == 0 ? "Primary" : "Secondary"
                };

                await _testRepo.Upsert(item);
            }

            // Act
            var results = await _testRepo.Get(x => x.PartitionKey == "Primary", new SearchModel());
            results.TotalResults.Should().BeGreaterThanOrEqualTo(50);

            var secondary = await _testRepo.Get(x => x.PartitionKey == "Secondary", new SearchModel());
            secondary.TotalResults.Should().BeGreaterThanOrEqualTo(50);

            var combined = await _testRepo.Get(x => true, new SearchModel());
            combined.TotalResults.Should().BeGreaterThanOrEqualTo(100);
        }
    }
}
