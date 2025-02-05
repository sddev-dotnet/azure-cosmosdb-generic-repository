using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.JsonPatch;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using SDDev.Net.GenericRepository.CosmosDB.Patch.Converters;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch.Cosmos;
public class CosmosPatchOperationCollection<TEntity> : BasePatchOperationCollection<TEntity, CosmosPatchOperation> where TEntity : class, IStorableEntity
{
    public static CosmosPatchOperationCollection<TEntity> CreateFromJsonPatchDocument(JsonPatchDocument<TEntity> jsonPatchDocument)
    {
        var collection = new CosmosPatchOperationCollection<TEntity>();

        foreach (var operation in jsonPatchDocument.Operations)
        {
            var cosmosOperation = operation.OperationType switch
            {
                OperationType.Add => new CosmosPatchOperation(SDPatchOperationType.Add, operation.path, operation.value),
                OperationType.Remove => new CosmosPatchOperation(SDPatchOperationType.Remove, operation.path),
                OperationType.Replace => new CosmosPatchOperation(SDPatchOperationType.Replace, operation.path, operation.value),
                _ => new CosmosPatchOperation(SDPatchOperationType.Set, operation.path, operation.value),
            };

            collection.AppendOperation(cosmosOperation);
        }

        return collection;
    }

    /// <inheritdoc/>
    public override void Set<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value) =>
        AddOperation<SlashNotationConverter<TEntity>, TProperty>(
            expression,
            path => new CosmosPatchOperation(SDPatchOperationType.Set, path, value));

    /// <inheritdoc/>
    public override void Add<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value) =>
        AddOperation<SlashNotationConverter<TEntity>, TProperty>(
            expression,
            path => new CosmosPatchOperation(SDPatchOperationType.Add, path, value));

    /// <inheritdoc/>
    public override void Add<TProperty>(Expression<Func<TEntity, IEnumerable<TProperty>>> expression, TProperty value) =>
        AddOperation<SlashNotationConverter<TEntity>, TProperty>(
            expression,
            path => new CosmosPatchOperation(SDPatchOperationType.Add, path, value));

    /// <inheritdoc/>
    public override void Replace<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value) =>
        AddOperation<SlashNotationConverter<TEntity>, TProperty>(
            expression,
            path => new CosmosPatchOperation(SDPatchOperationType.Replace, path, value));

    /// <inheritdoc/>
    public override void Remove<TProperty>(Expression<Func<TEntity, TProperty>> expression) =>
        AddOperation<SlashNotationConverter<TEntity>, TProperty>(
            expression,
            path => new CosmosPatchOperation(SDPatchOperationType.Remove, path));
}
