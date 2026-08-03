using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LegalERP.Application.Common;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;

namespace LegalERP.Application.Cases;

public interface ICaseRepository : IRepository<Case>
{
    Task<List<Case>> SearchAsync(string? searchTerm, CaseType? type, CaseStatus? status, CancellationToken ct = default);

    Task AddPartyAsync(CaseParty party, CancellationToken ct = default);
    Task<CaseParty?> GetPartyByIdAsync(Guid partyId, CancellationToken ct = default);
    void UpdateParty(CaseParty party);
    void SoftDeleteParty(CaseParty party);

    Task AddMemoAsync(CaseMemo memo, CancellationToken ct = default);
    Task<CaseMemo?> GetMemoByIdAsync(Guid memoId, CancellationToken ct = default);
    void UpdateMemo(CaseMemo memo);
    void SoftDeleteMemo(CaseMemo memo);

    Task AddHearingAsync(CaseHearing hearing, CancellationToken ct = default);
    Task<CaseHearing?> GetHearingByIdAsync(Guid hearingId, CancellationToken ct = default);
    void UpdateHearing(CaseHearing hearing);
    void SoftDeleteHearing(CaseHearing hearing);
}
