using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Application.Financials;
using LegalERP.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LegalERP.Tests.Financials;

public class FinancialSummaryCacheTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly IConfiguration _configuration;
    private readonly FinancialSummaryCache _cacheService;

    public FinancialSummaryCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "FinancialCaching:SlidingExpirationSeconds", "60" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _cacheService = new FinancialSummaryCache(_memoryCache, _configuration);
    }

    [Fact]
    public async Task GetOrCreateSummaryAsync_WhenCacheEmpty_CallsFactoryAndCachesResult()
    {
        int factoryInvocationCount = 0;
        var expectedSummary = new GlobalFinancialsDto
        {
            TotalCaseAgreedFees = 10000m,
            TotalCaseCollected = 5000m
        };

        var result = await _cacheService.GetOrCreateSummaryAsync(ct =>
        {
            factoryInvocationCount++;
            return Task.FromResult(expectedSummary);
        });

        result.Should().BeSameAs(expectedSummary);
        factoryInvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateSummaryAsync_WhenCalledMultipleTimes_ServesFromCacheWithoutCallingFactoryAgain()
    {
        int factoryInvocationCount = 0;
        var initialSummary = new GlobalFinancialsDto
        {
            TotalCaseAgreedFees = 20000m,
            TotalCaseCollected = 8000m
        };

        var firstCall = await _cacheService.GetOrCreateSummaryAsync(ct =>
        {
            factoryInvocationCount++;
            return Task.FromResult(initialSummary);
        });

        var secondCall = await _cacheService.GetOrCreateSummaryAsync(ct =>
        {
            factoryInvocationCount++;
            return Task.FromResult(new GlobalFinancialsDto());
        });

        firstCall.Should().BeSameAs(initialSummary);
        secondCall.Should().BeSameAs(initialSummary);
        factoryInvocationCount.Should().Be(1, "factory should only be invoked once when cached");
    }

    [Fact]
    public async Task Invalidate_EvictsCachedSummary_SoNextCallInvokesFactory()
    {
        int factoryInvocationCount = 0;
        var summary1 = new GlobalFinancialsDto { TotalCaseAgreedFees = 1000m };
        var summary2 = new GlobalFinancialsDto { TotalCaseAgreedFees = 2000m };

        await _cacheService.GetOrCreateSummaryAsync(ct =>
        {
            factoryInvocationCount++;
            return Task.FromResult(summary1);
        });

        // Proactively invalidate
        _cacheService.Invalidate();

        var callAfterInvalidate = await _cacheService.GetOrCreateSummaryAsync(ct =>
        {
            factoryInvocationCount++;
            return Task.FromResult(summary2);
        });

        callAfterInvalidate.Should().BeSameAs(summary2);
        factoryInvocationCount.Should().Be(2, "factory should be invoked again after invalidation");
    }

    [Fact]
    public void SlidingExpirationSeconds_ReadsConfiguredValueOrDefaultsTo60()
    {
        _cacheService.SlidingExpirationSeconds.Should().Be(60);

        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "FinancialCaching:SlidingExpirationSeconds", "120" }
            })
            .Build();

        var customCache = new FinancialSummaryCache(_memoryCache, customConfig);
        customCache.SlidingExpirationSeconds.Should().Be(120);
    }

    [Fact]
    public void GlobalFinancialsDto_ComputeETag_GeneratesDeterministicHexHash()
    {
        var summary1 = new GlobalFinancialsDto
        {
            TotalCaseAgreedFees = 1000m,
            TotalCaseCollected = 500m
        };

        var summary2 = new GlobalFinancialsDto
        {
            TotalCaseAgreedFees = 1000m,
            TotalCaseCollected = 500m
        };

        var summary3 = new GlobalFinancialsDto
        {
            TotalCaseAgreedFees = 2000m,
            TotalCaseCollected = 500m
        };

        summary1.ComputeETag().Should().Be(summary2.ComputeETag());
        summary1.ComputeETag().Should().NotBe(summary3.ComputeETag());
        summary1.ComputeETag().Should().StartWith("\"").And.EndWith("\"");
    }
}
