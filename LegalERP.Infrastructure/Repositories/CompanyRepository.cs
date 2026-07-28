using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LegalERP.Application.Companies;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LegalERP.Infrastructure.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly ApplicationDbContext _db;

    public CompanyRepository(ApplicationDbContext db) => _db = db;

    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Companies
            .Include(c => c.Amendments).ThenInclude(a => a.Document)
            .Include(c => c.Partners).ThenInclude(p => p.NationalIdDocument)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<Company>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Companies.OrderBy(c => c.CompanyName).ToListAsync(ct);

    public async Task<List<Company>> SearchAsync(string? searchTerm, CompanyCategory? category, CancellationToken ct = default)
    {
        var query = _db.Companies.AsQueryable();

        if (category is not null)
            query = query.Where(c => c.Category == category);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // Fuzzy search via pg_trgm (TR-7.1). Falls back to this simple
            // form now; we'll swap in EF.Functions trigram similarity once
            // the trigram extension/index is added in the migration step.
            query = query.Where(c =>
                EF.Functions.ILike(c.CompanyName, $"%{searchTerm}%") ||
                (c.CompanyNameEn != null && EF.Functions.ILike(c.CompanyNameEn, $"%{searchTerm}%")));
        }

        return await query.OrderBy(c => c.CompanyName).ToListAsync(ct);
    }

    public async Task AddAsync(Company entity, CancellationToken ct = default) =>
        await _db.Companies.AddAsync(entity, ct);

    public void Update(Company entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Companies.Update(entity);
    }

    public void SoftDelete(Company entity)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    public async Task AddAmendmentAsync(CompanyAmendment amendment, CancellationToken ct = default) =>
        await _db.CompanyAmendments.AddAsync(amendment, ct);

    public async Task<CompanyAmendment?> GetAmendmentByIdAsync(Guid amendmentId, CancellationToken ct = default) =>
        await _db.CompanyAmendments
            .Include(a => a.Document)
            .FirstOrDefaultAsync(a => a.Id == amendmentId, ct);

    public void UpdateAmendment(CompanyAmendment amendment)
    {
        amendment.UpdatedAt = DateTime.UtcNow;
        _db.CompanyAmendments.Update(amendment);
    }

    public void SoftDeleteAmendment(CompanyAmendment amendment)
    {
        amendment.IsDeleted = true;
        amendment.UpdatedAt = DateTime.UtcNow;
    }

    public async Task AddPartnerAsync(CompanyPartner partner, CancellationToken ct = default) =>
        await _db.CompanyPartners.AddAsync(partner, ct);

    public async Task<CompanyPartner?> GetPartnerByIdAsync(Guid partnerId, CancellationToken ct = default) =>
        await _db.CompanyPartners
            .Include(p => p.NationalIdDocument)
            .FirstOrDefaultAsync(p => p.Id == partnerId, ct);

    public void UpdatePartner(CompanyPartner partner)
    {
        partner.UpdatedAt = DateTime.UtcNow;
        _db.CompanyPartners.Update(partner);
    }

    public void SoftDeletePartner(CompanyPartner partner)
    {
        partner.IsDeleted = true;
        partner.UpdatedAt = DateTime.UtcNow;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);
}
