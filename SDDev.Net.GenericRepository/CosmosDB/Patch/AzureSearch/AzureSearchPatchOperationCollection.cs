using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository.Patch;
using SDDev.Net.GenericRepository.CosmosDB.Patch.Converters;
using SDDev.Net.GenericRepository.CosmosDB.Patch.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch.AzureSearch;
public class AzureSearchPatchOperationCollection<TEntity> : BasePatchOperationCollection<TEntity, IPatchOperation> where TEntity : class, IStorableEntity
{
    public override void Set<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value)
    {
        ThrowIfArrayAccess(expression);

        AddOperation<DotNotationConverter<TEntity>, TProperty>(expression, path =>
        {
            if (_operations.Any(x => x.Path == path))
            {
                throw new InvalidOperationException($"The path '{path}' has already been set in the patch operation collection.");
            }

            return new AzureSearchPatchOperation(path, value);
        });

        AddOperation<SlashNotationConverter<TEntity>, TProperty>(expression, path =>
            new CosmosPatchOperation(SDPatchOperationType.Set, path, value));
    }

    public override void Add<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value) =>
        Set(expression, value);

    public override void Add<TProperty>(Expression<Func<TEntity, IEnumerable<TProperty>>> expression, TProperty value) =>
        ThrowIfArrayAccess(expression);

    public override void Replace<TProperty>(Expression<Func<TEntity, TProperty>> expression, TProperty value) =>
        Set(expression, value);

    public override void Remove<TProperty>(Expression<Func<TEntity, TProperty>> expression) =>
        Set(expression, default);

    private void ThrowIfArrayAccess(Expression expression)
    {
        if (IsArrayAccess(expression))
        {
            throw new InvalidOperationException("Manipulating an array is not supported in Azure Search. You must set the entire property with the new array value.");
        }
    }

    private bool IsArrayAccess(Expression expression)
    {
        if (expression is IndexExpression indexExpression)
        {
            return indexExpression.Object?.Type.IsArray == true;
        }

        if (expression is MemberExpression memberExpression)
        {
            return memberExpression.Expression?.Type.IsArray == true;
        }

        if (expression is UnaryExpression unaryExpression && unaryExpression.NodeType == ExpressionType.Convert)
        {
            // Recursively check parent expressions
            return IsArrayAccess(unaryExpression.Operand);
        }

        return false;
    }
}
