using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LegalERP.Domain.Common;

namespace LegalERP.Application.Common;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void SoftDelete(T entity);   // sets IsDeleted = true — TR-1.3, never a hard delete
    Task SaveChangesAsync(CancellationToken ct = default);
}