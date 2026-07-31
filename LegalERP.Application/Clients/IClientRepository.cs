using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Common;
using LegalERP.Domain.Entities;

namespace LegalERP.Application.Clients;

public interface IClientRepository : IRepository<Client>
{
    Task<List<Client>> SearchAsync(string? searchTerm, CancellationToken ct = default);
}
