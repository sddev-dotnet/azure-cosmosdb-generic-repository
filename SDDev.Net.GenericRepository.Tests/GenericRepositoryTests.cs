using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.CosmosDB;
using SDDev.Net.GenericRepository.CosmosDB.Patch.Cosmos;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using SDDev.Net.GenericRepository.Tests.TestModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Tests
{
    [TestClass]
    public class GenericRepositoryTests
    {
        private static IConfiguration _config;
        private static IOptions<CosmosDbConfiguration> _cosmos;
        private static CosmosClient _client;
        private static ILogger<GenericRepository<TestObject>> _testLogger;
        private static ILogger<GenericRepository<TestAuditableObject>> _auditableLogger;
        private static ILoggerFactory _factory;

        private TestRepo _testRepo;
        private GenericRepository<TestAuditableObject> _auditableRepo;

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
            _testLogger = _factory.CreateLogger<GenericRepository<TestObject>>();
            _auditableLogger = _factory.CreateLogger<GenericRepository<TestAuditableObject>>();
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

            _testRepo = new TestRepo(_client, _testLogger, _cosmos, "Testing");
            _auditableRepo = new GenericRepository<TestAuditableObject>(_client, _auditableLogger, _cosmos, "Testing");
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
                },
                ExampleProperty = "Example from Conf."
            };


            //Act
            var result = await _testRepo.FindOne(x => x.ExampleProperty.Contains("Example"));


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
        public async Task WhenQueryingCount_ThenCountIsReturned_Test()
        {
            // Arrange
            var logger = _factory.CreateLogger<GenericRepository<TestObject>>();
            var repo = new GenericRepository<TestObject>(_client, logger, _cosmos, "Testing");

            var testObjectCount = Random.Shared.Next(1, 10);

            var key = Guid.NewGuid().ToString();
            var testObjects = Enumerable.Range(1, testObjectCount)
                .Select(x => new TestObject()
                {
                    Key = key,
                });

            foreach (var testObject in testObjects)
            {
                await repo.Upsert(testObject);
            }

            // Act
            var count = await repo.Count(x => true, key);

            // Assert
            count.Should().Be(testObjectCount);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenQueryCountWithExpression_ThenCountIsReturned_Test()
        {
            // Arrange
            var logger = _factory.CreateLogger<GenericRepository<TestObject>>();
            var repo = new GenericRepository<TestObject>(_client, logger, _cosmos, "Testing");

            var key = Guid.NewGuid().ToString();
            var expectedTestObject = new TestObject()
            {
                ExampleProperty = Guid.NewGuid().ToString(),
                Key = key,
            };
            var extraTestObject = new TestObject()
            {
                ExampleProperty = Guid.NewGuid().ToString(),
                Key = key,
            };

            await repo.Upsert(expectedTestObject);
            await repo.Upsert(extraTestObject);

            // Act
            var count = await repo.Count(
                x => x.ExampleProperty == expectedTestObject.ExampleProperty,
                partitionKey: key);

            // Assert
            count.Should().Be(1);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenUsingAnotherTestObject_ThenYouCanSearch_Test()
        {

            var logger = _factory.CreateLogger<GenericRepository<AnotherTestObject>>();
            var repo = new GenericRepository<AnotherTestObject>(_client, logger, _cosmos);

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
            var logger = _factory.CreateLogger<GenericRepository<BaseTestObject>>();
            var repo = new GenericRepository<BaseTestObject>(_client, logger, _cosmos, "BaseTest");

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
            var logger = _factory.CreateLogger<GenericRepository<BaseTestObject>>();
            var repo = new GenericRepository<BaseTestObject>(_client, logger, _cosmos, "BaseTest");

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
            var logger = _factory.CreateLogger<GenericRepository<BaseTestObject>>();
            var repo = new GenericRepository<BaseTestObject>(_client, logger, _cosmos, "BaseTest");
            var specificRepo = new GenericRepository<ChildObject1>(_client, _factory.CreateLogger<GenericRepository<ChildObject1>>(), _cosmos, "BaseTest");


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
            var partitionKey1 = Guid.NewGuid().ToString();
            var partitionKey2 = Guid.NewGuid().ToString();
            var anchorKey = Guid.NewGuid().ToString();
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
                    ChildObject = new TestObject
                    {
                        Number = random.Next(),
                        Prop1 = "ChildTestObject"
                    },
                    Prop1 = anchorKey,
                    Key = i % 2 == 0 ? partitionKey1 : partitionKey2,
                };

                await _testRepo.Upsert(item);
            }

            // Act
            var results = await _testRepo.Get(x => x.Prop1 == anchorKey, new SearchModel()
            {
                PartitionKey = partitionKey1,
            });
            results.TotalResults.Should().Be(50);

            var secondary = await _testRepo.Get(x => x.Prop1 == anchorKey, new SearchModel()
            {
                PartitionKey = partitionKey2
            });
            secondary.TotalResults.Should().Be(50);

            var combined = await _testRepo.Get(x => x.Prop1 == anchorKey, new SearchModel());
            combined.TotalResults.Should().Be(100);
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenPatchingTestObject_ThenIndividualPropertiesAreUpdated()
        {
            // Arrange
            var item = new TestObject
            {
                Number = 5,
                ChildObject = new TestObject
                {
                    Number = 8,
                },
                Key = Guid.NewGuid().ToString(),
            };

            var id = await _testRepo.Create(item);

            try
            {
                var original = await _testRepo.Get(id, item.PartitionKey);

                original.Number.Should().Be(5);
                original.ChildObject.Number.Should().Be(8);

                var operations = new CosmosPatchOperationCollection<TestObject>();
                operations.Replace(x => x.Number, 7);
                operations.Replace(x => x.ChildObject.Number, 12);

                // Act
                await _testRepo.Patch(id, item.PartitionKey, operations);

                // Assert
                var updated = await _testRepo.Get(id, item.PartitionKey);

                updated.Number.Should().Be(7);
                updated.ChildObject.Number.Should().Be(12);
            }
            finally
            {
                await _testRepo.Delete(id, item.Key, force: true);
            }
        }

        [TestMethod]
        [TestCategory("INTEGRATION")]
        public async Task WhenPatchingAuditableObject_ThenModifiedDateTimeIsUpdated()
        {
            // Arrange
            var item = new TestAuditableObject
            {
                ExampleProperty = "Test",
                ChildObject = new TestObject
                {
                    Prop1 = "w00t",
                },
                Collection = new List<string> { "Test1", "Test2" },
                Key = Guid.NewGuid().ToString(),
            };

            var id = await _auditableRepo.Create(item);

            try
            {
                var original = await _auditableRepo.Get(id, item.PartitionKey);

                original.ExampleProperty.Should().Be("Test");
                original.ChildObject.Prop1.Should().Be("w00t");
                original.Collection[0].Should().Be("Test1");
                original.Collection[1].Should().Be("Test2");

                var operations = new CosmosPatchOperationCollection<TestAuditableObject>();
                operations.Replace(x => x.ExampleProperty, "Hmmm");
                operations.Replace(x => x.ChildObject.Prop1, "Yaaas");
                operations.Remove(x => x.Collection[0]);
                operations.Add(x => x.Collection, "Test3");

                // Act
                await _auditableRepo.Patch(id, item.PartitionKey, operations);

                // Assert
                var updated = await _auditableRepo.Get(id, item.PartitionKey);

                updated.ExampleProperty.Should().Be("Hmmm");
                updated.ChildObject.Prop1.Should().Be("Yaaas");
                updated.Collection.Should().BeEquivalentTo(["Test2", "Test3"]);

                updated.AuditMetadata.ModifiedDateTime.Should().BeWithin(TimeSpan.FromSeconds(1)).Before(DateTime.UtcNow);
            }
            finally
            {
                await _testRepo.Delete(id, item.Key, force: true);
            }
        }

        [TestMethod]
        [DataRow(true, 2)]
        [DataRow(false, 0)]
        [DataRow(null, 2)]
        [TestCategory("INTEGRATION")]
        public async Task WhenSearchModelConfigurationIsProvided_ThenItOverridesConfigurationIncludeTotalResults(bool? includeTotalResults, int expectedTotalCount)
        {
            // ARRANGE
            var partitionKey = Guid.NewGuid().ToString();
            var commonProperty = Guid.NewGuid().ToString();
            var item1 = new TestObject()
            {
                Id = Guid.NewGuid(),
                ExampleProperty = commonProperty,
                Key = partitionKey,
            };
            var item2 = new TestObject()
            {
                Id = Guid.NewGuid(),
                ExampleProperty = commonProperty,
                Key = partitionKey,
            };

            await Task.WhenAll(
                _testRepo.Create(item1),
                _testRepo.Create(item2));

            var searchModel = new SearchModel()
            {
                IncludeTotalResults = includeTotalResults,
                PageSize = 1, // Use this so that we force a paginated response
                PartitionKey = partitionKey,
            };

            try
            {
                // ACT
                var results = await _testRepo.Get(x => x.ExampleProperty == commonProperty, searchModel);

                // ASSERT
                results.TotalResults.Should().Be(expectedTotalCount);
            }
            finally
            {
                await Task.WhenAll(
                    _testRepo.Delete(item1, force: true),
                    _testRepo.Delete(item2, force: true));
            }
        }
    }
}
