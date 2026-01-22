namespace SDDev.Net.GenericRepository.Caching
{
    /// <summary>
    /// Configuration options for CachedRepository behavior.
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// Gets or sets the cache expiration time in seconds.
        /// Default is 60 seconds.
        /// </summary>
        public int CacheSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets whether to use sliding expiration.
        /// When true, cache expiration is reset on each access.
        /// When false, cache entries expire after CacheSeconds regardless of access.
        /// Default is false.
        /// </summary>
        public bool RefreshCache { get; set; } = false;
    }
}
