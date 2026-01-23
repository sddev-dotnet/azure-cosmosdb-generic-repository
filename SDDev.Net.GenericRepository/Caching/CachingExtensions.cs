using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using SDDev.Net.GenericRepository.Contracts.BaseEntity;
using SDDev.Net.GenericRepository.Contracts.Repository;
using System;
using System.Linq;

namespace SDDev.Net.GenericRepository.Caching
{
    /// <summary>
    /// Extension methods for validating distributed cache registration and decorating repositories with caching
    /// to prevent connection pool exhaustion in Kubernetes deployments.
    /// </summary>
    public static class CachingExtensions
    {
        /// <summary>
        /// Validates that IDistributedCache is registered as singleton (or not registered at all).
        /// Throws an exception if it's registered as transient or scoped, which would cause connection pool exhaustion
        /// when scaling horizontally in Kubernetes.
        /// </summary>
        /// <param name="services">The service collection to validate.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="InvalidOperationException">Thrown if IDistributedCache is registered as transient or scoped.</exception>
        /// <remarks>
        /// Call this method after registering your IDistributedCache implementation (e.g., after AddStackExchangeRedisCache)
        /// to ensure proper singleton registration for Kubernetes deployments.
        /// </remarks>
        public static IServiceCollection ValidateDistributedCacheRegistration(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var distributedCacheDescriptor = services.FirstOrDefault(
                d => d.ServiceType == typeof(IDistributedCache));

            if (distributedCacheDescriptor != null)
            {
                if (distributedCacheDescriptor.Lifetime == ServiceLifetime.Transient)
                {
                    throw new InvalidOperationException(
                        "IDistributedCache is already registered as Transient. " +
                        "This will cause multiple connection pools per pod, leading to connection pool exhaustion. " +
                        "IDistributedCache must be registered as Singleton. " +
                        "Ensure AddStackExchangeRedisCache() or your cache registration method registers it as Singleton.");
                }

                if (distributedCacheDescriptor.Lifetime == ServiceLifetime.Scoped)
                {
                    throw new InvalidOperationException(
                        "IDistributedCache is already registered as Scoped. " +
                        "This will cause multiple connection pools per pod, leading to connection pool exhaustion. " +
                        "IDistributedCache must be registered as Singleton. " +
                        "Ensure AddStackExchangeRedisCache() or your cache registration method registers it as Singleton.");
                }

                // If it's already registered as Singleton, that's fine - validation passes
            }

            return services;
        }

        /// <summary>
        /// Validates IDistributedCache registration before decorating repositories with caching.
        /// Call this method before using Scrutor's Decorate method to ensure proper singleton registration.
        /// </summary>
        /// <param name="services">The service collection to validate.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="InvalidOperationException">Thrown if IDistributedCache is registered as transient or scoped.</exception>
        /// <remarks>
        /// This method validates that IDistributedCache is registered as Singleton before you decorate repositories.
        /// Use this before calling Scrutor's Decorate method to ensure proper configuration.
        /// 
        /// Example usage:
        /// <code>
        /// builder.Services.AddStackExchangeRedisCache(options => { ... });
        /// builder.Services.ValidateCacheRegistrationBeforeDecorating(); // Validates cache registration
        /// builder.Services.AddScoped&lt;IRepository&lt;MyEntity&gt;, GenericRepository&lt;MyEntity&gt;&gt;();
        /// builder.Services.Decorate&lt;IRepository&lt;MyEntity&gt;, CachedRepository&lt;MyEntity&gt;&gt;(); // Requires Scrutor
        /// </code>
        /// </remarks>
        public static IServiceCollection ValidateCacheRegistrationBeforeDecorating(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Validate cache registration before decoration
            return services.ValidateDistributedCacheRegistration();
        }

        /// <summary>
        /// Provides optimized configuration guidance for Azure Cache for Redis.
        /// This method validates that IDistributedCache will be registered correctly and provides
        /// guidance on optimal configuration for horizontal scaling scenarios.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// <para>
        /// <strong>Purpose:</strong> This helper validates your Redis cache configuration and provides
        /// guidance for optimal Azure Cache for Redis setup in Kubernetes environments.
        /// </para>
        /// 
        /// <para>
        /// <strong>Recommended Configuration:</strong>
        /// When using AddStackExchangeRedisCache(), the default configuration is usually optimal.
        /// StackExchange.Redis uses connection multiplexing where a single ConnectionMultiplexer
        /// handles many concurrent operations efficiently. Each pod creates one ConnectionMultiplexer
        /// (via singleton IDistributedCache), which is optimal for horizontal scaling.
        /// </para>
        /// 
        /// <para>
        /// <strong>Example with optimized defaults:</strong>
        /// <code>
        /// // Standard registration (already optimized by default)
        /// builder.Services.AddStackExchangeRedisCache(options =>
        /// {
        ///     options.Configuration = builder.Configuration.GetConnectionString("Redis");
        ///     // ConnectionMultiplexer defaults are optimal for most scenarios
        ///     // No additional configuration needed unless you have specific requirements
        /// });
        /// 
        /// // Validate the registration
        /// builder.Services.ValidateAzureRedisConfiguration();
        /// </code>
        /// </para>
        /// 
        /// <para>
        /// <strong>Connection Multiplexing:</strong>
        /// - One ConnectionMultiplexer per pod (via singleton IDistributedCache)
        /// - Each ConnectionMultiplexer multiplexes operations over a small number of physical connections
        /// - Default settings handle thousands of concurrent operations efficiently
        /// </para>
        /// 
        /// <para>
        /// <strong>Azure Redis Tier Selection:</strong>
        /// - Basic (20 connections): Not suitable for production with multiple services
        /// - Standard (1,000+ connections): Suitable for most production scenarios (30 services × 10 pods = 300 connections)
        /// - Premium (2,000+ connections): For high-scale deployments (30 services × 50 pods = 1,500 connections)
        /// </para>
        /// 
        /// <para>
        /// <strong>Connection Calculation:</strong>
        /// Total connections = Number of pods across all services.
        /// Each pod = 1 ConnectionMultiplexer = efficient connection usage.
        /// </para>
        /// </remarks>
        public static IServiceCollection ValidateAzureRedisConfiguration(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Validate that IDistributedCache will be registered as Singleton
            // This ensures optimal connection usage (1 pool per pod)
            return services.ValidateDistributedCacheRegistration();
        }
    }
}
