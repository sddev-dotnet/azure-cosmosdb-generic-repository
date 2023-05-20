using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.Indexing
{
    public class IndexSearchMetadata
    {
        public long TotalResults { get; set; }

        public Dictionary<string, IList<FacetValue>> Facets { get; set; } = new Dictionary<string, IList<FacetValue>>();
    }

    public class FacetValue
    {
        public string Value { get; set; }

        public long? Count { get; set; }
    }
}
