using ApiSign.AspNetCore.Abstractions;

using Microsoft.Extensions.Caching.Memory;

namespace ApiSign.AspNetCore.Core;

/// <summary>
/// Default in-memory nonce store.
/// </summary>
public sealed class DefaultNonceStore(IMemoryCache memoryCache) : INonceStore
{
    private readonly IMemoryCache _memoryCache = memoryCache;

    public Task<bool> ExistsAsync(string nonce)
        => Task.FromResult(_memoryCache.TryGetValue(nonce, out _));

    public Task SaveAsync(string nonce, TimeSpan expireTime)
    {
        _memoryCache.Set(nonce, true, expireTime);
        return Task.CompletedTask;
    }
}