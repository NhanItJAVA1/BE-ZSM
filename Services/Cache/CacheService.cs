using BE_ZSM.Services.Cache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        string? json;
        try
        {
            json = await _cache.GetStringAsync(key);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Redis cache read failed for key {CacheKey}", key);
            return default;
        }

        if (json == null)
            return default;

        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration)
    {
        var json = JsonSerializer.Serialize(value);

        try
        {
            await _cache.SetStringAsync(
                key,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration
                });
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Redis cache write failed for key {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (RedisException exception)
        {
            _logger.LogWarning(exception, "Redis cache removal failed for key {CacheKey}", key);
        }
    }
}
