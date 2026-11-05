using System;
using System.Threading;
using System.Threading.Tasks;

namespace LegalERP.Application.Financials;

public interface IFinancialSummaryCache
{
    int SlidingExpirationSeconds { get; }

    Task<GlobalFinancialsDto> GetOrCreateSummaryAsync(
        Func<CancellationToken, Task<GlobalFinancialsDto>> factory,
        CancellationToken ct = default);

    void Invalidate();
}
