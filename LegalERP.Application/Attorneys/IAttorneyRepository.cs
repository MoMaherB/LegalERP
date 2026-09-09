using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Common;
using LegalERP.Domain.Entities;

namespace LegalERP.Application.Attorneys;

public interface IAttorneyRepository : IRepository<Attorney>
{
    Task<List<Attorney>> SearchAsync(string? searchTerm, CancellationToken ct = default);
    Task AddClientAsync(AttorneyClient link, CancellationToken ct = default);
    Task<AttorneyClient?> GetAttorneyClientByIdAsync(Guid id, CancellationToken ct = default);
    void SoftDeleteAttorneyClient(AttorneyClient link);
    Task<List<Attorney>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
}
