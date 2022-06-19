using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.Indexing
{
    public class IndexRepositoryOptions
    {
        /// <summary>
        /// When true, the document will be removed from the index during a logical delete operation
        /// otherwise, an update is executed (assuming that an IsActive flag is flipped);
        /// </summary>
        public bool RemoveOnLogicalDelete { get; set; } = false;

        public bool CreateOrUpdateIndex { get; set; } = true;

        /// <summary>
        /// The name of the index to create
        /// </summary>
        public string IndexName { get; set; }

        /// <summary>
        /// This is the name of the registered admin client
        /// </summary>
        public string AdminClientName { get; set; }
    }
}
