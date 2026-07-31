using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LegalERP.Application.Cases;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LegalERP.Infrastructure.Repositories;

public class CaseRepository : ICaseRepository
{
    private readonly ApplicationDbContext _db;

    public CaseRepository(ApplicationDbContext db) => _db = db;

    public async Task<Case?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Cases
            .Include(c => c.Parties).ThenInclude(p => p.Document)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<Case>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Cases.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

    public async Task<List<Case>> SearchAsync(string? searchTerm, CaseType? type, CaseStatus? status, CancellationToken ct = default)
    {
        var query = _db.Cases.AsQueryable();

        if (type.HasValue)
            query = query.Where(c => c.CaseType == type.Value);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.CaseNumber, pattern) ||
                EF.Functions.ILike(c.Title, pattern) ||
                (c.CourtName != null && EF.Functions.ILike(c.CourtName, pattern)) ||
                EF.Functions.TrigramsAreSimilar(c.CaseNumber, searchTerm) ||
                EF.Functions.TrigramsAreSimilar(c.Title, searchTerm));
        }

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
    }

    public async Task AddAsync(Case entity, CancellationToken ct = default) =>
        await _db.Cases.AddAsync(entity, ct);

    public void Update(Case entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Cases.Update(entity);
    }

    public void SoftDelete(Case entity)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Cases.Update(entity);
    }

    public async Task AddPartyAsync(CaseParty party, CancellationToken ct = default) =>
        await _db.CaseParties.AddAsync(party, ct);

    public async Task<CaseParty?> GetPartyByIdAsync(Guid partyId, CancellationToken ct = default) =>
        await _db.CaseParties
            .Include(p => p.Document)
            .FirstOrDefaultAsync(p => p.Id == partyId, ct);

    public void UpdateParty(CaseParty party)
    {
        party.UpdatedAt = DateTime.UtcNow;
        _db.CaseParties.Update(party);
    }

    public void SoftDeleteParty(CaseParty party)
    {
        party.IsDeleted = true;
        party.UpdatedAt = DateTime.UtcNow;
        _db.CaseParties.Update(party);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);
}
