using Azure;
using Azure.Search.Documents.Models;
using SDDev.Net.GenericRepository.Contracts.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.Indexing
{
    public class IndexSearchResult<T> 
    {
        public Response<SearchResults<T>> Metadata { get; set; }
        public List<Azure.Search.Documents.Models.SearchResult<T>> Results { get; set; } = new List<Azure.Search.Documents.Models.SearchResult<T>>();
    }
}
