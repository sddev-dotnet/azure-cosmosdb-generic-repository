using Azure.Search.Documents.Indexes;
using SDDev.Net.GenericRepository.Contracts.Indexing;

namespace SDDev.Net.GenericRepository.Tests.TestModels
{
    public class BaseTestIndexModel : BaseIndexModel
    {
        [SearchableField(IsFacetable = true, IsFilterable = true, IsSortable = true)]
        public string Name { get; set; }
    }
}
