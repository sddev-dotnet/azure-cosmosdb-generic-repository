using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SDDev.Net.GenericRepository.Contracts.Search;
using SDDev.Net.GenericRepository.CosmosDB;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Tests.TestModels
{
    public class TestRepo : GenericRepository<TestObject>
    {


        public Task<ISearchResult<TestObject>> Get(ISearchModel model)
        {
            var search = (TestSearchModel)model;
            var query = new StringBuilder("id != null");

            if (search.CollectionValues.Any())
            {
                foreach (var searchCollectionValue in search.CollectionValues)
                {
                    query.Append($" and Collection.Contains(\"{searchCollectionValue}\")");
                }
            }

            if (!string.IsNullOrEmpty(search.StrVal))
            {
                query.Append($" and Prop1=\"{search.StrVal}\"");
            }

            if (!string.IsNullOrEmpty(search.ContainsStrTest))
            {
                query.Append($" and ChildObject.Prop1.Contains(\"{search.ContainsStrTest}\")");
            }

            if (search.Num > 0)
            {
                query.Append($" and Number={search.Num}");
            }

            if (search.UUID.HasValue)
            {
                query.Append($" and UUID=\"{search.UUID}\"");
            }


            var queryResult = query.ToString();

            return Get(queryResult, model);
        }

        public TestRepo(CosmosClient client, ILogger<BaseRepository<TestObject>> log, IOptions<CosmosDbConfiguration> config, string collectionName = null, string partitionKey = null) : base(client, log, config, collectionName, partitionKey)
        {
        }
    }
}