using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB.Migrations
{
    public abstract class BaseMigration : IMigration
    {
        public virtual string Name => GetType().FullName;

        public virtual bool AlwaysExecute { get; set; } = false;

        public abstract Task Down();
        public abstract Task Up();
    }
}
