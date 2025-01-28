using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using SDDev.Net.GenericRepository.CosmosDB.Patch.Converters;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch.Cosmos;
public class CosmosPatchOperationCollection<TEntity> : BasePatchOperationCollection<TEntity, CosmosPatchOperation> where TEntity : IStorableEntity
{
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
