using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using System;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch;
public abstract class BasePatchOperation<TOperation> : IPatchOperation
{
    public BasePatchOperation(SDPatchOperationType type, string path, object value)
    {
        Path = path;
        Value = value;
        Type = type;
    }

    public string Path { get; }

    public object Value { get; }

    public SDPatchOperationType Type { get; init; }

    protected abstract TOperation MakeOperation();

    public TExternalOperation ToOperation<TExternalOperation>()
    {
        // This is the cleanest way I could find to enforce the type in a backwards compatible way.
        // An additional generic type parameter on the GenericRepository would be a breaking change to allow compile-time type checking for the PatchOperation that has to be produced for the implementation.
        // Everything else is strictly enforced, this is the only semi-leak, and it would be obvious to the developer if they ran their code even a single time.
        if (typeof(TExternalOperation) != typeof(TOperation))
        {
            throw new InvalidCastException($"Cannot cast {typeof(TOperation).Name} to {typeof(TExternalOperation).Name}");
        }

        var operation = MakeOperation();
        return (TExternalOperation)(object)operation;
    }
}
