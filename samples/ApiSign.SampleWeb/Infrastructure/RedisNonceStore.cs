using ApiSign.AspNetCore.Abstractions;

using Microsoft.Extensions.Caching.Distributed;

namespace ApiSign.SampleWeb.Infrastructure;

public sealed class RedisNonceStore(IDistributedCache cache) : INonceStore
{
    private readonly IDistributedCache _cache = cache;

    public async Task<bool> ExistsAsync(string nonce)
    {
        var value = await _cache.GetStringAsync(nonce);
        return value is not null;
    }

    public Task SaveAsync(string nonce, TimeSpan expireTime)
        => _cache.SetStringAsync(
            nonce,
            bool.TrueString,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expireTime,
            });
}