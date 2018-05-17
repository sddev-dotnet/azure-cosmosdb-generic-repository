using System;

namespace SDDev.Net.GenericRepository.Contracts.BaseEntity
{
    public interface IStorableEntity
    {
        /// <summary>
        /// The unique identifier for the object
        /// </summary>
        Guid? Id { get; set; }

        /// <summary>
        /// The item type of the object
        /// </summary>
        string[] ItemType { get; }
    }
}