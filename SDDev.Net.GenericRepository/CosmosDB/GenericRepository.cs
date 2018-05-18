using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Logging;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Search;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB
{
    /// <summary>
	/// Class that allows for most CRUD operations on an Azure DocumentDB
	/// </summary>
	/// <typeparam name="TModel"></typeparam>
	public class GenericRepository<TModel> : BaseRepository<TModel> where TModel : class, IStorableEntity
    {

        public override async Task<TModel> Get(Guid id)
        {
            try
            {
                var resp = await Client.ReadDocumentAsync(UriFactory.CreateDocumentUri(DatabaseName, CollectionName, id.ToString()));
                var item = (TModel)(dynamic)resp.Resource;
                return item;
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                    return null;

                throw;
            }
            catch (Exception e)
            {
                Log.LogError($"There was a problem with the GET operation for object {typeof(TModel).Name}", e);
                throw;
            }
        }

        public override async Task<ISearchResult<TModel>> Get(Expression<Func<TModel, bool>> predicate, ISearchModel model)
        {
            var feedoptions = new FeedOptions() { MaxItemCount = model.PageSize };
            var response = new SearchResult<TModel>() { PageSize = model.PageSize };
            var result = Client.CreateDocumentQuery<TModel>(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName), feedoptions)
                .Where(predicate)
                .AsEnumerable<TModel>()
                .ToList();


            response.Results = result;
            response.TotalResults = result.Count();


            return response;

        }

        public async override Task<ISearchResult<TModel>> Get(string query, ISearchModel model)
        {
            var feedoptions = new FeedOptions() { MaxItemCount = model.PageSize };
            var response = new SearchResult<TModel>() { PageSize = model.PageSize };
            var result = Client.CreateDocumentQuery<TModel>(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName), feedoptions)
                .Where(query)
                .AsEnumerable<TModel>();

            response.Results = result.ToList();
            response.TotalResults = result.Count();


            return response;
        }

        public override async Task<TModel> Upsert(TModel model)
        {
            if (!model.Id.HasValue || model.Id == Guid.Empty)
                model.Id = Guid.NewGuid();
            try
            {
                await Client.ReadDocumentAsync(UriFactory.CreateDocumentUri(DatabaseName, CollectionName, model.Id.ToString()));

                //made it this far, we need to replace not insert
                await Client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(DatabaseName, CollectionName, model.Id.ToString()), model);

                return model;
            }
            catch (DocumentClientException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    Log.LogDebug($"Creating Object type {typeof(TModel).Name} with ID {model.Id}");
                    try
                    {
                        await Client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName), model);
                    }
                    catch (DocumentClientException ex)
                    {
                        Log.LogError("Problem creating document.", ex);
                    }
                    return model;
                }
                else
                {
                    throw;
                }
            }
        }

        public override async Task Delete(Guid id)
        {
            Log.LogInformation($"Deleting document {id} from Database {DatabaseName} Collection {CollectionName}");
            await Client.DeleteDocumentAsync(UriFactory.CreateDocumentUri(DatabaseName, CollectionName, id.ToString()));
        }

        public GenericRepository(IDocumentClient client, ILogger log) : base(client, log)
        {
        }

        public GenericRepository(IDocumentClient client, ILogger log, string database, string collection) : base(client, log, database, collection)
        {

        }


    }
}
