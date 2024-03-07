using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Utilities
{
    public static class CosmosDBHelpers
    {
        /// <summary>
        /// Returns a dynamic object with the results of the query. Its good for content projections or aggregating data.
        /// </summary>
        /// <param name="queryString"></param>
        /// <param name="container"></param>
        /// <param name="maxItemCount"></param>
        /// <returns></returns>
        public static async Task<List<ExpandoObject>> GetCosmosResults(string queryString, Container container, int maxItemCount = 100)
        {
            var query = container.GetItemQueryIterator<ExpandoObject>(
                queryText: queryString,
                requestOptions: new QueryRequestOptions() { MaxItemCount = maxItemCount }
            );

            var items = new List<ExpandoObject>();
            try
            {
                while (query.HasMoreResults)
                {
                    var results = await query.ReadNextAsync();
                    foreach (var result in results)
                    {
                        items.Add(result);
                    }
                }
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // if no results were found, return an empty list
                    return items;
                }
                throw;
            }

            return items;
        }

        /// <summary>
        /// This is good for returning a single numeric value from a query. Good for single aggregates like counts or sums.
        /// Make sure you use the VALUE keyword in your query to return a single value.
        /// 
        /// i.e. SELECT VALUE COUNT(1) FROM c
        /// </summary>
        /// <param name="queryString"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        public static async Task<long> GetScalarValue(string queryString, Container container)
        {
            var query = container.GetItemQueryIterator<long>(
                queryText: queryString,
                requestOptions: new QueryRequestOptions() { MaxItemCount = 100 }
            );

            while (query.HasMoreResults)
            {
                var resp = await query.ReadNextAsync();
                var total = resp.SingleOrDefault();
                return total;
            }

            return 0;
        }

    }
}
