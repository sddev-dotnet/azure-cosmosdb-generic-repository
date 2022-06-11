using Azure.Search.Documents.Indexes;
using SDDev.Net.GenericRepository.Contracts.Indexing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Tests.TestModels
{
    public class BaseTestIndexModel : BaseIndexModel
    {
        [SearchableField]
        public string Name { get; set; }
    }
}
