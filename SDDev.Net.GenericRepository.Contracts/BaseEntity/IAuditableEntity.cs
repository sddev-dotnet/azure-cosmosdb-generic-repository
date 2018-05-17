namespace SDDev.Net.GenericRepository.Contracts.BaseEntity
{
    public interface IAuditableEntity : IStorableEntity
    {
        AuditMetadata AuditMetadata { get; set; }
    }
}