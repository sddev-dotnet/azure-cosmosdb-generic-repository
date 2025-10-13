using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch.AzureSearch;
public class AzureSearchPatchOperation : BasePatchOperation<KeyValuePair<string, object>>
{
    public AzureSearchPatchOperation(string path, object value)
        : base(SDPatchOperationType.Set, path, value)
    { }

    protected override KeyValuePair<string, object> MakeOperation() => new(Path, Value);
}
