using Azure.Search.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.Indexing
{
    public class SearchRequest
    {
        public string SearchText { get; set; }

        public SearchOptions Options { get; set; }
    }
}
