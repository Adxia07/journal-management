using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace JournalApi.Services;

/// <summary>
/// Сервис для работы с Redis кэшем.
/// Обёртывает IDistributedCache и предоставляет удобные методы Get/Set/Remove.
/// </summary>
public class CacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;

    // TTL по умолчанию — 5 минут
    private static readonly DistributedCacheEntryOptions DefaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Получить значение из кэша по ключу.
    /// Возвращает null, если ключа нет или он истёк.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var bytes = await _cache.GetAsync(key);
            if (bytes == null) return default;

            _logger.LogInformation("Cache HIT: {Key}", key);
            return JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            // Если Redis недоступен, продолжаем без кэша
            _logger.LogWarning(ex, "Cache GET failed for key: {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Сохранить значение в кэш с заданным TTL.
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        try
        {
            var options = ttl.HasValue
                ? new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }
                : DefaultOptions;

            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await _cache.SetAsync(key, bytes, options);
            _logger.LogInformation("Cache SET: {Key} (TTL={TTL})", key, ttl ?? TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key: {Key}", key);
        }
    }

    /// <summary>
    /// Удалить ключ из кэша (инвалидация при изменении данных).
    /// </summary>
    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
            _logger.LogInformation("Cache REMOVE: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key: {Key}", key);
        }
    }

    /// <summary>
    /// Удалить все ключи с заданным префиксом (инвалидация группы).
    /// Примечание: в Redis Community Edition нет встроенного Pattern Delete через IDistributedCache,
    /// поэтому инвалидируем по конкретным ключам.
    /// </summary>
    public async Task RemoveManyAsync(params string[] keys)
    {
        foreach (var key in keys)
            await RemoveAsync(key);
    }
}
