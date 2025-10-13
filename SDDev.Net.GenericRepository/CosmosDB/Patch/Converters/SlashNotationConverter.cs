using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using System.Linq.Expressions;
using System.Text;
using System;
using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch.Converters;
public class SlashNotationConverter<TEntity> : BaseNotationConverter, IPathConverter<TEntity>
    where TEntity : IStorableEntity
{
    public string ConvertToPath(LambdaExpression expression)
    {
        var path = new StringBuilder();
        var node = expression.Body;
        while (node != null)
        {
            switch (node)
            {
                case MemberExpression memberExpression:
                    if (IsCollectionType(memberExpression.Type) && path.Length == 0)
                    {
                        // Append "/-" for collections with no index access
                        path.Insert(0, "/-");
                        break;
                    }
                    path.Insert(0, $"/{GetJsonPropertyName(memberExpression.Member)}");
                    node = memberExpression.Expression;
                    break;

                case MethodCallExpression methodCallExpression:
                    if (methodCallExpression.Method.Name == "get_Item")
                    {
                        var indexExpression = methodCallExpression.Arguments[0];
                        var indexValue = Expression.Lambda(indexExpression).Compile().DynamicInvoke();
                        path.Insert(0, $"/{indexValue}");
                        node = methodCallExpression.Object;
                    }
                    else
                    {
                        throw new NotSupportedException($"Unsupported method: {methodCallExpression.Method.Name}");
                    }
                    break;

                default:
                    node = null;
                    break;
            }
        }

        return path.ToString();
    }

    private static bool IsCollectionType(Type type)
    {
        return typeof(IEnumerable<object>).IsAssignableFrom(type)
               || type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition());
    }
}
