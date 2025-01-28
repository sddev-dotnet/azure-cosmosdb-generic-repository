using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using SDDev.Net.GenericRepository.CosmosDB.Patch.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch;
public abstract class BasePatchOperationCollection<TEntity, TOperation> : IPatchOperationCollection<TEntity>
    where TEntity : IStorableEntity
    where TOperation : IPatchOperation
{
    protected readonly List<IPatchOperation> _operations = new();

    /// <inheritdoc/>
    public abstract void Add<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value);
    /// <inheritdoc/>
    public abstract void Add<TProperty>(Expression<Func<TEntity, IEnumerable<TProperty>>> expression, TProperty value);
    /// <inheritdoc/>
    public abstract void Remove<TProperty>(Expression<Func<TEntity, TProperty>> expression);
    /// <inheritdoc/>
    public abstract void Replace<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value);
    /// <inheritdoc/>
    public abstract void Set<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value);

    public IEnumerator<IPatchOperation> GetEnumerator() => _operations.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected internal void AddOperation<TPathConverter, TProperty>(
        LambdaExpression expression,
        Func<string, TOperation> makePatchOperation)
        where TPathConverter : IPathConverter<TEntity>, new()
    {
        var converter = new TPathConverter();
        var path = converter.ConvertToPath(expression);
        var operation = makePatchOperation(path);
        _operations.Add(operation);
    }
}
