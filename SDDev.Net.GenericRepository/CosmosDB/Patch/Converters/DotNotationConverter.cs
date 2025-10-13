using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using System;
using System.Linq.Expressions;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch.Converters;
public class DotNotationConverter<TEntity> : BaseNotationConverter, IPathConverter<TEntity>
    where TEntity : IStorableEntity
{
    public string ConvertToPath(LambdaExpression expression)
    {
        // Handle conversion expressions (e.g., Convert nodes for value types)
        if (expression.Body is UnaryExpression unaryExpression
            && unaryExpression.NodeType == ExpressionType.Convert)
        {
            return BuildPath(unaryExpression.Operand);
        }

        return BuildPath(expression.Body);
    }

    private string BuildPath(Expression expression)
    {
        switch (expression)
        {
            case MemberExpression memberExpression:
            {
                // Recursively resolve parent expressions to build dot notation
                var parentPath = BuildPath(memberExpression.Expression);

                // Get the property name, falling back to the member name if no attribute is found
                var propertyName = GetJsonPropertyName(memberExpression.Member);

                return string.IsNullOrEmpty(parentPath)
                    ? propertyName
                    : $"{parentPath}.{propertyName}";
            }

            case ParameterExpression:
                // Root parameter, return an empty string
                return string.Empty;
            default:
                throw new InvalidOperationException($"Unsupported expression type: {expression.GetType().Name}");
        }
    }
}
