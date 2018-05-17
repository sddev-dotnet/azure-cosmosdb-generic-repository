namespace SDDev.Net.GenericRepository.Contracts.Search
{
    public class SearchModel : ISearchModel
    {
        public int PageSize { get; set; }
        public int Offset { get; set; }
        public bool SortAscending { get; set; }
        public string SortByField { get; set; }
        public string ContinuationToken { get; set; }
    }
}