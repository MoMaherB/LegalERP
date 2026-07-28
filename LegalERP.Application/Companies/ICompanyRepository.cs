using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegalERP.Application.Common;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;

namespace LegalERP.Application.Companies;

public interface ICompanyRepository : IRepository<Company>
{
    Task<List<Company>> SearchAsync(string? searchTerm, CompanyCategory? category, CancellationToken ct = default);
    Task AddAmendmentAsync(CompanyAmendment amendment, CancellationToken ct = default);
    Task<CompanyAmendment?> GetAmendmentByIdAsync(Guid amendmentId, CancellationToken ct = default);
    void UpdateAmendment(CompanyAmendment amendment);
    void SoftDeleteAmendment(CompanyAmendment amendment);
    Task AddPartnerAsync(CompanyPartner partner, CancellationToken ct = default);
    Task<CompanyPartner?> GetPartnerByIdAsync(Guid partnerId, CancellationToken ct = default);
    void UpdatePartner(CompanyPartner partner);
    void SoftDeletePartner(CompanyPartner partner);
}