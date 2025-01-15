namespace SDDev.Net.GenericRepository.Contracts.Search
{
    public class SearchModel : ISearchModel
    {
        /// <summary>
        /// The max number of results to return. Defaults to 10
        /// </summary>
        public int PageSize { get; set; } = 10;

        /// <summary>
        /// The number of records to skip when paging
        /// we should be using the continuation token instead of this property in most cases
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Whether the sort should be ascending or descending
        /// </summary>
        public bool SortAscending { get; set; }

        /// <summary>
        /// Which field to order the results by
        /// </summary>
        public string SortByField { get; set; }

        /// <summary>
        /// Allows you to continue a previous search and get the next page of results
        /// </summary>
        public string ContinuationToken { get; set; }

        /// <summary>
        /// The partition key to search, if null, a cross partition query is executed
        /// </summary>
        public string PartitionKey { get; set; }

        /// <summary>
        /// Determines whether is or not an active record
        /// </summary>
        public bool? Active { get; set; }

        /// <inheritdoc/>
        public bool? IncludeTotalResults { get; set; }
    }
}