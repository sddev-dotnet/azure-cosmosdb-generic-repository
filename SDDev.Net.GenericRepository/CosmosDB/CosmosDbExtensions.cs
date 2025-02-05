using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using SDDev.Net.GenericRepository.Contracts.Repository;
using SDDev.Net.GenericRepository.CosmosDB.Migrations;
using SDDev.Net.GenericRepository.CosmosDB.Utilities;
using System;
using System.Collections.Generic;

namespace SDDev.Net.GenericRepository.CosmosDB
{
    public static class CosmosDbExtensions
    {
        public static void UseCosmosDbMigrations(this IApplicationBuilder builder)
        {
            var migrator = builder.ApplicationServices.GetService<IMigrator>();
            migrator.Migrate().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Add our Implementation of CosmosDB to the startup of an ASP.NET app.
        /// This method is intended to be used BEFORE the App is built, and before the Host is Run. (eg. builder.Services.UseCosmosDb)
        /// </summary>
        /// <remarks>
        /// Requires a config value for DocumentDBUrl and DocumentDBKey
        /// </remarks>
        /// <param name="services"></param>
        public static void UseCosmosDb(this IServiceCollection services, IConfiguration configuration)
        {
            RegisterServices(services, configuration);
            //Register the document client from the config
            services.AddSingleton(ctx =>
            {
                var serializerSettings = new JsonSerializerSettings()
                {
                    ContractResolver = new DefaultContractResolver(),
                    TypeNameHandling = TypeNameHandling.Objects,
                    NullValueHandling = NullValueHandling.Include,
                    Converters = new List<JsonConverter>() { new StringEnumConverter() }
                };

                //Serialize enums as string using default naming strategy (unchanged)
                serializerSettings.Converters.Add(new StringEnumConverter() { NamingStrategy = new DefaultNamingStrategy() });

                var serializer = new CosmosJsonDotNetSerializer(serializerSettings);

                var config = ctx.GetService<IOptionsMonitor<CosmosDbConfiguration>>();

                if (string.IsNullOrEmpty(config.CurrentValue.ConnectionString))
                    throw new InvalidOperationException("Missing required DocumentDB Configuration");

                return new CosmosClient(config.CurrentValue.ConnectionString, new CosmosClientOptions()
                {
                    Serializer = serializer,
                    AllowBulkExecution = config.CurrentValue.EnableBulkQuerying
                });

            });


        }

        /// <summary>
        /// This method is intended to be used AFTER the App is built, and before the Host is Run. (eg. app.AddCosmosDb)
        /// </summary>
        /// <param name="host"></param>
        public static void AddCosmosDb(this IHost host)
        {
            var logger = host.Services.GetRequiredService<ILogger>();
            var config = host.Services.GetRequiredService<IOptions<CosmosDbConfiguration>>();
            CosmosQueryExtensions.InitializeDependenices(logger, config);
        }

        private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient(typeof(IRepository<>), typeof(GenericRepository<>));
            services.Configure<CosmosDbConfiguration>(configuration.GetSection("CosmosDb"));
            services.AddTransient<IMigrator, CosmosDbMigrator>();
            services.AddScoped<IContainerClient, CosmosDbClient>();

            services.AddTransient<IRepository<MigrationResult>, GenericRepository<MigrationResult>>(x => {
                return new GenericRepository<MigrationResult>(x.GetService<IContainerClient>(), 
                    x.GetService<ILogger<GenericRepository<MigrationResult>>>(), 
                    x.GetService<IOptions<CosmosDbConfiguration>>(), 
                    "Migrations");
            });
        }


    }
}
