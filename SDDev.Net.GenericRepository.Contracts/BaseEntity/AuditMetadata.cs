using System;

namespace SDDev.Net.GenericRepository.Contracts.BaseEntity
{
    public class AuditMetadata
    {
        public DateTime? CreatedDateTime { get; set; }

        public DateTime? ModifiedDateTime { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? ModifiedBy { get; set; }
    }
}