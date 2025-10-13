using Microsoft.Azure.Cosmos;
using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using System;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch.Cosmos;

public class CosmosPatchOperation : BasePatchOperation<PatchOperation>
{
    public CosmosPatchOperation(SDPatchOperationType operationType, string path, object value = default)
        : base(operationType, path, value)
    { }

    protected override PatchOperation MakeOperation()
    {
        return Type switch
        {
            SDPatchOperationType.Add => PatchOperation.Add(Path, Value),
            SDPatchOperationType.Remove => PatchOperation.Remove(Path),
            SDPatchOperationType.Replace => PatchOperation.Replace(Path, Value),
            SDPatchOperationType.Set => PatchOperation.Set(Path, Value),
            _ => throw new NotImplementedException()
        };
    }
};
