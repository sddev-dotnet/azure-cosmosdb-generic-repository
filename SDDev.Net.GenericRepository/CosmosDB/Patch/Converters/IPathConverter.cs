using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using System.Linq.Expressions;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch.Converters;
public interface IPathConverter<TEntity>
    where TEntity : IStorableEntity
{
    string ConvertToPath(LambdaExpression expression);
}
