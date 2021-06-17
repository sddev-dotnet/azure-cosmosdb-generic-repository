using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB.Migrations
{
    public interface IMigration
    {
        bool AlwaysExecute { get; }
        string Name { get; }
        Task Up();

        Task Down();
    }
}
