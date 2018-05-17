using System;
using Newtonsoft.Json;

namespace SDDev.Net.GenericRepository.Contracts.BaseEntity
{
    public abstract class BaseStorableEntity : IStorableEntity
    {
        [JsonProperty("id")]
        public Guid? Id { get; set; }

        public virtual string[] ItemType => new string[] { this.GetType().Name };
    }
}