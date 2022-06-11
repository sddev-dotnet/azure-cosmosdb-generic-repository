using Azure.Search.Documents.Indexes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.Indexing
{
    public abstract class BaseIndexModel : IBaseIndexModel
    {
        [SimpleField(IsKey = true, IsFilterable = true)]
        public string Id { get; set; }
    }
}
