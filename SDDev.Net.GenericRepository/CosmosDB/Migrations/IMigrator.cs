using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB.Migrations
{
    public interface IMigrator
    {
        Task Migrate();

    }
}
