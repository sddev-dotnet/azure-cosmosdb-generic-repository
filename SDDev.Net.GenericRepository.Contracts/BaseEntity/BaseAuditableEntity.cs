using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.Contracts.BaseEntity
{
    public class BaseAuditableEntity : BaseStorableEntity, IAuditableEntity
    {
        public AuditMetadata AuditMetadata { get; set; } = new AuditMetadata();
    }
}
