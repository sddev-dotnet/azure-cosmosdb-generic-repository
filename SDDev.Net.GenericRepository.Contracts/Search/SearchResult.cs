using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.Contracts.Search
{
    public class SearchResult<T> : ISearchResult<T>
    {
        public int PageSize { get; set; }
        public int TotalResults { get; set; }
        public IList<T> Results { get; set; }
        public string ContinuationToken { get; set; }
    }
}