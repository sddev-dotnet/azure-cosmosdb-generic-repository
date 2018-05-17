using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.Contracts.Search
{
    public interface ISearchResult<T>
    {
        int PageSize { get; set; }

        int TotalResults { get; set; }

        IList<T> Results { get; set; }

        string ContinuationToken { get; set; }
    }
}