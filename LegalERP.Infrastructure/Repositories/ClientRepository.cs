using LegalERP.Application.Clients;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LegalERP.Infrastructure.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly ApplicationDbContext _db;

    public ClientRepository(ApplicationDbContext db) => _db = db;

    public async Task<List<Client>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Clients
            .Include(c => c.NationalIdDocument)
            .Include(c => c.AttorneyDocument)
            .OrderBy(c => c.FullName)
            .ToListAsync(ct);
    }

    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Clients
            .Include(c => c.NationalIdDocument)
            .Include(c => c.AttorneyDocument)
            .Include(c => c.CaseParties)
                .ThenInclude(p => p.Case)
            .Include(c => c.CompanyPartnerships)
                .ThenInclude(p => p.Company)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task AddAsync(Client entity, CancellationToken ct = default)
    {
        await _db.Clients.AddAsync(entity, ct);
    }

    public void Update(Client entity)
    {
        _db.Clients.Update(entity);
    }

    public void SoftDelete(Client entity)
    {
        entity.IsDeleted = true;
        _db.Clients.Update(entity);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<Client>> SearchAsync(string? searchTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllAsync(ct);

        var words = searchTerm.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var query = _db.Clients
            .Include(c => c.NationalIdDocument)
            .Include(c => c.AttorneyDocument)
            .AsQueryable();

        foreach (var word in words)
        {
            var pattern = $"%{word}%";
            query = query.Where(c => EF.Functions.ILike(c.FullName, pattern) ||
                                     (c.FullNameEn != null && EF.Functions.ILike(c.FullNameEn, pattern)) ||
                                     (c.NationalIdNumber != null && EF.Functions.ILike(c.NationalIdNumber, pattern)));
        }

        return await query
            .OrderBy(c => c.FullName)
            .ToListAsync(ct);
    }
}
