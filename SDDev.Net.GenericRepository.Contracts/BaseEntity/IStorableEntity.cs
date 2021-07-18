using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.Contracts.BaseEntity
{
    public interface IStorableEntity
    {
        /// <summary>
        /// The unique identifier for the object
        /// </summary>
        [JsonProperty("id")]
        Guid? Id { get; set; }

        /// <summary>
        /// The item type of the object
        /// </summary>
        IList<string> ItemType { get; }

        /// <summary>
        /// Logical Deletion flag
        /// </summary>
        bool IsActive { get; set; }

        /// <summary>
        /// The key that the collection this object is stored in is partitioned by
        /// </summary>
        string PartitionKey { get; }

        /// <summary>
        /// The amount of time until the document is deleted by cosmos
        /// </summary>
        [JsonProperty("ttl")]
        int TimeToLive { get; set; }
    }
}