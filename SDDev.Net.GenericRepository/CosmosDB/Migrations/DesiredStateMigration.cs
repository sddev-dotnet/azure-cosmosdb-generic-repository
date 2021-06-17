using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB.Migrations
{
    public abstract class DesiredStateMigration : BaseMigration
    {
        public override bool AlwaysExecute => true;

        public abstract Task Execute();

        public override Task Up()
        {
            return Execute();
        }

        public override Task Down()
        {
            return Execute();
        }
    }
}
