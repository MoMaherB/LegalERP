using LegalERP.Application.Attorneys;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LegalERP.Infrastructure.Repositories;

public class AttorneyRepository : IAttorneyRepository
{
    private readonly ApplicationDbContext _db;

    public AttorneyRepository(ApplicationDbContext db) => _db = db;

    public async Task<List<Attorney>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Attorneys
            .Include(a => a.Document)
            .Include(a => a.AttorneyClients.Where(ac => !ac.IsDeleted))
                .ThenInclude(ac => ac.Client)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Attorney?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Attorneys
            .Include(a => a.Document)
            .Include(a => a.AttorneyClients.Where(ac => !ac.IsDeleted))
                .ThenInclude(ac => ac.Client)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task AddAsync(Attorney entity, CancellationToken ct = default)
    {
        await _db.Attorneys.AddAsync(entity, ct);
    }

    public void Update(Attorney entity)
    {
        _db.Attorneys.Update(entity);
    }

    public void SoftDelete(Attorney entity)
    {
        entity.IsDeleted = true;
        _db.Attorneys.Update(entity);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<Attorney>> SearchAsync(string? searchTerm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllAsync(ct);

        var pattern = $"%{searchTerm.Trim()}%";
        return await _db.Attorneys
            .Include(a => a.Document)
            .Include(a => a.AttorneyClients.Where(ac => !ac.IsDeleted))
                .ThenInclude(ac => ac.Client)
            .Where(a => EF.Functions.ILike(a.AttorneyNumber, pattern))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddClientAsync(AttorneyClient link, CancellationToken ct = default)
    {
        await _db.AttorneyClients.AddAsync(link, ct);
    }

    public async Task<AttorneyClient?> GetAttorneyClientByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.AttorneyClients
            .Include(ac => ac.Client)
            .Include(ac => ac.Attorney)
            .FirstOrDefaultAsync(ac => ac.Id == id, ct);
    }

    public void SoftDeleteAttorneyClient(AttorneyClient link)
    {
        link.IsDeleted = true;
        _db.AttorneyClients.Update(link);
    }

    public async Task<List<Attorney>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default)
    {
        return await _db.Attorneys
            .Include(a => a.Document)
            .Include(a => a.AttorneyClients.Where(ac => !ac.IsDeleted))
                .ThenInclude(ac => ac.Client)
            .Where(a => a.AttorneyClients.Any(ac => ac.ClientId == clientId && !ac.IsDeleted))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }
}
