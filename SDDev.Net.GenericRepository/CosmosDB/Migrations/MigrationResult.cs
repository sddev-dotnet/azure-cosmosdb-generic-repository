using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using System;

namespace SDDev.Net.GenericRepository.CosmosDB.Migrations
{
    public class MigrationResult : BaseStorableEntity
    {
        public string Name { get; set; }

        public DateTime ExecutedDateTime { get; set; } = DateTime.UtcNow;

        public bool IsSuccessful { get; set; }


    }
}
