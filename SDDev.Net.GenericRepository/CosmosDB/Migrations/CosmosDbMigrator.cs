using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.Contracts.Search;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SDDev.Net.GenericRepository.CosmosDB.Migrations
{
    public class CosmosDbMigrator : IMigrator
    {
        private ILogger<CosmosDbMigrator> _logger;
        private IRepository<MigrationResult> _migrationRepository;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// Retrieves any migrations that have been registered in the Services Collection and executes them
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public async Task Migrate()
        {

            //Find all of the migrations in the assembly specified
            var migrations = _serviceProvider.GetServices<IMigration>().OrderBy(x => x.Name);
            _logger.LogInformation($"Found {migrations.Count()} migrations.");
            foreach (var m in migrations)
            {
                _logger.LogDebug($"Found Migration Type {m.Name}.");
            }



            //Find the last migration that was executed
            // by querying for MigrationResult objects
            var results = await _migrationRepository.GetAll(x => x.IsSuccessful, new SearchModel() { PageSize = 500, SortByField = "ExecutedDateTime", SortAscending = false });

            _logger.LogInformation($"Found {results.TotalResults} executed migrations to skip.");

            //filter migrations, skipping ones that have been completed
            var migrationsToExecute = migrations.Where(m => m.AlwaysExecute || results.Results.All(x => x.Name != m.Name));

            _logger.LogInformation($"Preparing to execute {migrationsToExecute.Count()} migrations.");

            foreach (var migration in migrationsToExecute)
            {

                _logger.LogInformation($"Executing migration {migration.Name}.");

                try
                {
                    await migration.Up();
                    var result = new MigrationResult()
                    {
                        ExecutedDateTime = DateTime.UtcNow,
                        IsSuccessful = true,
                        Name = migration.Name
                    };
                    await _migrationRepository.Upsert(result);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Migration failed. Performing Down.");

                    try
                    {
                        await migration.Down();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex, "Exception during down migration. Data is in an unknown state.");
                    }


                    throw;
                }

            }

        }


        public CosmosDbMigrator(ILogger<CosmosDbMigrator> logger, IRepository<MigrationResult> migrationRepository, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _migrationRepository = migrationRepository;
            _serviceProvider = serviceProvider;
        }

    }
}
