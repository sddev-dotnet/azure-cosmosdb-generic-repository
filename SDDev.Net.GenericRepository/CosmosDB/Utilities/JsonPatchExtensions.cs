using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB.Utilities
{
    public static class JsonPatchExtensions
    {
    //    public static IReadOnlyList<PatchOperation> ToCosmosPatchOperations(this Microsoft.AspNetCore.JsonPatch.JsonPatchDocument jsonPatchOperations)
    //    {

    //        List<PatchOperation> cosmosPatchOperations = new List<PatchOperation>(jsonPatchOperations.Operations.Count);
    //        foreach (Operation jsonPatchOperation in jsonPatchOperations.Operations)
    //        {
    //            switch (jsonPatchOperation.OperationType)
    //            {
    //                case OperationType.Add:
    //                    cosmosPatchOperations.Add(PatchOperation.Add(jsonPatchOperation.path, jsonPatchOperation.value));
    //                    break;
    //                case OperationType.Remove:
    //                    cosmosPatchOperations.Add(PatchOperation.Remove(jsonPatchOperation.path));
    //                    break;
    //                case OperationType.Replace:
    //                    cosmosPatchOperations.Add(PatchOperation.Replace(jsonPatchOperation.path, jsonPatchOperation.value));
    //                    break;
                    
    //            }
    //        }

    //        return cosmosPatchOperations;
    //    }
    }
}
