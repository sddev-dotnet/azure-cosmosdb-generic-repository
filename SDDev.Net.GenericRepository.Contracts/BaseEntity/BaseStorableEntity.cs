using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.Contracts.BaseEntity
{
    public abstract class BaseStorableEntity : IStorableEntity
    {
        /// <summary>
        /// The unique ID for the entity. 
        /// </summary>
        [JsonProperty("id")]
        public Guid? Id { get; set; }


        /// <summary>
        /// Logical Deletion flag
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// A type name collection that is used to facilitate searching
        /// </summary>
        public virtual IList<string> ItemType => new List<string>() { this.GetType().Name };

        /// <summary>
        /// The Partition Key value 
        /// </summary>
        public virtual string PartitionKey => this.GetType().Name;

        [JsonProperty("ttl")]
        public virtual int TimeToLive { get; set; } = -1;
    }
}