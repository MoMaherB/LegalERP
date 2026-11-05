using System;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Financials;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace LegalERP.Infrastructure.Services;

public class FinancialSummaryCache : IFinancialSummaryCache
{
    public const string CacheKey = "FinancialSummary_Global";
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _slidingExpiration;

    public int SlidingExpirationSeconds { get; }

    public FinancialSummaryCache(IMemoryCache cache, IConfiguration configuration)
    {
        _cache = cache;
        var secondsValue = configuration["FinancialCaching:SlidingExpirationSeconds"];
        SlidingExpirationSeconds = int.TryParse(secondsValue, out var parsedSeconds) && parsedSeconds > 0
            ? parsedSeconds
            : 60;
        _slidingExpiration = TimeSpan.FromSeconds(Math.Max(5, SlidingExpirationSeconds));
    }

    public async Task<GlobalFinancialsDto> GetOrCreateSummaryAsync(
        Func<CancellationToken, Task<GlobalFinancialsDto>> factory,
        CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out GlobalFinancialsDto? cached) && cached is not null)
        {
            return cached;
        }

        var fresh = await factory(ct);

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = _slidingExpiration
        };

        _cache.Set(CacheKey, fresh, cacheEntryOptions);
        return fresh;
    }

    public void Invalidate()
    {
        _cache.Remove(CacheKey);
    }
}
