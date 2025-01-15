namespace SDDev.Net.GenericRepository.Contracts.Repository.Patch;

public interface IPatchOperation
{
    public SDPatchOperationType Type { get; }
    public string Path { get; }
    public object Value { get; }

    public TExternalOperation ToOperation<TExternalOperation>();
}
