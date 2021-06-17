namespace SDDev.Net.GenericRepository.Contracts.Search
{
    public interface ISearchModel
    {
        int PageSize { get; set; }

        int Offset { get; set; }

        bool SortAscending { get; set; }

        string SortByField { get; set; }

        string ContinuationToken { get; set; }

        string PartitionKey { get; set; }
    }
}