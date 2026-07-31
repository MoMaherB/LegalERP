using LegalERP.Application.Companies;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyRepository _repository;

    public CompaniesController(ICompanyRepository repository) => _repository = repository;

    // GET /api/companies
    [HttpGet]
    public async Task<ActionResult<List<CompanyDto>>> GetAll(CancellationToken ct)
    {
        var companies = await _repository.GetAllAsync(ct);
        return Ok(companies.Select(ToDto));
    }

    // GET /api/companies/search?term=xxx&category=CapitalCompany
    [HttpGet("search")]
    public async Task<ActionResult<List<CompanyDto>>> Search(
        [FromQuery] string? term, [FromQuery] CompanyCategory? category, CancellationToken ct)
    {
        var companies = await _repository.SearchAsync(term, category, ct);
        return Ok(companies.Select(ToDto));
    }

    // POST /api/companies
    [HttpPost]
    public async Task<ActionResult<CompanyDto>> Create(CreateCompanyDto dto, CancellationToken ct)
    {
        var company = new Company
        {
            Category = dto.Category,
            CompanyName = dto.CompanyName,
            CompanyNameEn = dto.CompanyNameEn,
            TradeName = dto.TradeName,
            EstablishmentDate = dto.EstablishmentDate,
            RegistrationNumber = dto.RegistrationNumber,
            Address = dto.Address,
            IncorporationDocumentId = dto.IncorporationDocumentId
        };

        await _repository.AddAsync(company, ct);
        await _repository.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAll), new { id = company.Id }, ToDto(company));
    }

    private static CompanyDto ToDto(Company c) => new(
        c.Id,
        c.Category,
        c.CompanyName,
        c.CompanyNameEn,
        c.TradeName,
        c.EstablishmentDate,
        c.RegistrationNumber,
        c.Address,
        c.IncorporationDocumentId,
        c.IncorporationDocument == null ? null : new DocumentDto(c.IncorporationDocument.Id, c.IncorporationDocument.FileName, c.IncorporationDocument.StoredFileName, c.IncorporationDocument.ContentType, c.IncorporationDocument.FileSizeBytes),
        c.Amendments.Select(a => new CompanyAmendmentDto(
            a.Id, 
            a.SequenceNumber, 
            a.Title, 
            a.AmendmentDate, 
            a.DocumentId,
            a.Document == null ? null : new DocumentDto(a.Document.Id, a.Document.FileName, a.Document.StoredFileName, a.Document.ContentType, a.Document.FileSizeBytes)
        )).ToList(),
        c.Partners.Select(p => {
            var fullName = p.Client?.FullName ?? p.FullName;
            var nationalId = p.Client?.NationalIdNumber ?? p.NationalIdNumber;
            var doc = p.Client?.NationalIdDocument ?? p.NationalIdDocument;
            var docDto = doc == null ? null : new DocumentDto(doc.Id, doc.FileName, doc.StoredFileName, doc.ContentType, doc.FileSizeBytes);
            return new CompanyPartnerDto(
                p.Id, 
                p.ClientId,
                fullName, 
                nationalId, 
                p.OwnershipPercentage, 
                doc?.Id ?? p.NationalIdDocumentId,
                docDto
            );
        }).ToList()
    );

    // GET /api/companies/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyDto>> GetById(Guid id, CancellationToken ct)
    {
        var company = await _repository.GetByIdAsync(id, ct);
        if (company is null) return NotFound();

        return Ok(ToDto(company));
    }

    // PUT /api/companies/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, UpdateCompanyDto dto, CancellationToken ct)
    {
        var company = await _repository.GetByIdAsync(id, ct);
        if (company is null) return NotFound();

        company.Category = dto.Category;
        company.CompanyName = dto.CompanyName;
        company.CompanyNameEn = dto.CompanyNameEn;
        company.TradeName = dto.TradeName;
        company.EstablishmentDate = dto.EstablishmentDate;
        company.RegistrationNumber = dto.RegistrationNumber;
        company.Address = dto.Address;
        company.IncorporationDocumentId = dto.IncorporationDocumentId;

        _repository.Update(company);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // DELETE /api/companies/{id}
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var company = await _repository.GetByIdAsync(id, ct);
        if (company is null) return NotFound();

        _repository.SoftDelete(company);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // GET /api/companies/{companyId}/amendments
    [HttpGet("{companyId:guid}/amendments")]
    public async Task<ActionResult<List<CompanyAmendmentDto>>> GetAmendments(Guid companyId, CancellationToken ct)
    {
        var company = await _repository.GetByIdAsync(companyId, ct);
        if (company is null) return NotFound();

        var amendments = company.Amendments
            .OrderBy(a => a.SequenceNumber)
            .Select(a => new CompanyAmendmentDto(
                a.Id, 
                a.SequenceNumber, 
                a.Title, 
                a.AmendmentDate, 
                a.DocumentId, 
                a.Document == null ? null : new DocumentDto(a.Document.Id, a.Document.FileName, a.Document.StoredFileName, a.Document.ContentType, a.Document.FileSizeBytes)
            ));

        return Ok(amendments);
    }

    // POST /api/companies/{companyId}/amendments
    [HttpPost("{companyId:guid}/amendments")]
    public async Task<ActionResult<CompanyAmendmentDto>> AddAmendment(
        Guid companyId, CreateCompanyAmendmentDto dto, CancellationToken ct)
    {
        var company = await _repository.GetByIdAsync(companyId, ct);
        if (company is null) return NotFound();

        // Auto-incrementing sequence number based on active amendments
        // Matches BR-1.3, the amendment list is ordered and dynamic (1st, 2nd, 3rd...), never hardcoded.
        var nextSequence = company.Amendments.Count == 0
            ? 1
            : company.Amendments.Max(a => a.SequenceNumber) + 1;

        var title = dto.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            var ordinals = new[] { "الأول", "الثاني", "الثالث", "الرابع", "الخامس", "السادس", "السابع", "الثامن", "التاسع", "العاشر" };
            var ordinalStr = nextSequence <= 10 ? ordinals[nextSequence - 1] : nextSequence.ToString();
            title = $"عقد التعديل {ordinalStr}";
        }

        var amendment = new CompanyAmendment
        {
            CompanyId = companyId,
            SequenceNumber = nextSequence,
            Title = title,
            AmendmentDate = dto.AmendmentDate,
            DocumentId = dto.DocumentId
        };

        // Add directly to the DbContext rather than through the Company's
        // navigation collection. Adding via company.Amendments.Add() marks
        // the Company entity itself as Modified, which can trigger a phantom
        // concurrency check (DbUpdateConcurrencyException) if EF still
        // tracks stale concurrency metadata from a prior migration.
        await _repository.AddAmendmentAsync(amendment, ct);
        await _repository.SaveChangesAsync(ct);

        return Ok(new CompanyAmendmentDto(amendment.Id, amendment.SequenceNumber, amendment.Title, amendment.AmendmentDate, amendment.DocumentId, null));
    }

    // PUT /api/companies/{companyId}/amendments/{amendmentId}
    [HttpPut("{companyId:guid}/amendments/{amendmentId:guid}")]
    public async Task<ActionResult> UpdateAmendment(
        Guid companyId, Guid amendmentId, UpdateCompanyAmendmentDto dto, CancellationToken ct)
    {
        var amendment = await _repository.GetAmendmentByIdAsync(amendmentId, ct);
        if (amendment is null || amendment.CompanyId != companyId) return NotFound();

        amendment.Title = dto.Title;
        amendment.AmendmentDate = dto.AmendmentDate;
        amendment.DocumentId = dto.DocumentId;

        _repository.UpdateAmendment(amendment);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // DELETE /api/companies/{companyId}/amendments/{amendmentId}
    [HttpDelete("{companyId:guid}/amendments/{amendmentId:guid}")]
    public async Task<ActionResult> DeleteAmendment(
        Guid companyId, Guid amendmentId, CancellationToken ct)
    {
        var amendment = await _repository.GetAmendmentByIdAsync(amendmentId, ct);
        if (amendment is null || amendment.CompanyId != companyId) return NotFound();

        _repository.SoftDeleteAmendment(amendment);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // --- Partners ---

    // POST /api/companies/{companyId}/partners
    [HttpPost("{companyId:guid}/partners")]
    public async Task<ActionResult<CompanyPartnerDto>> AddPartner(
        Guid companyId, CreateCompanyPartnerDto dto, CancellationToken ct)
    {
        var company = await _repository.GetByIdAsync(companyId, ct);
        if (company is null) return NotFound();

        var partner = new CompanyPartner
        {
            CompanyId = companyId,
            ClientId = dto.ClientId,
            FullName = dto.FullName ?? "",
            NationalIdNumber = dto.NationalIdNumber,
            OwnershipPercentage = dto.OwnershipPercentage,
            NationalIdDocumentId = dto.NationalIdDocumentId
        };

        await _repository.AddPartnerAsync(partner, ct);
        await _repository.SaveChangesAsync(ct);

        var updatedCompany = await _repository.GetByIdAsync(companyId, ct);
        var createdPartner = updatedCompany?.Partners.FirstOrDefault(p => p.Id == partner.Id);

        if (createdPartner != null)
        {
            var fullName = createdPartner.Client?.FullName ?? createdPartner.FullName;
            var nationalId = createdPartner.Client?.NationalIdNumber ?? createdPartner.NationalIdNumber;
            var doc = createdPartner.Client?.NationalIdDocument ?? createdPartner.NationalIdDocument;
            var docDto = doc == null ? null : new DocumentDto(doc.Id, doc.FileName, doc.StoredFileName, doc.ContentType, doc.FileSizeBytes);

            return Ok(new CompanyPartnerDto(
                createdPartner.Id,
                createdPartner.ClientId,
                fullName,
                nationalId,
                createdPartner.OwnershipPercentage,
                doc?.Id ?? createdPartner.NationalIdDocumentId,
                docDto
            ));
        }

        return Ok(new CompanyPartnerDto(
            partner.Id, partner.ClientId, partner.FullName, partner.NationalIdNumber, partner.OwnershipPercentage, partner.NationalIdDocumentId, null));
    }

    // PUT /api/companies/{companyId}/partners/{partnerId}
    [HttpPut("{companyId:guid}/partners/{partnerId:guid}")]
    public async Task<ActionResult> UpdatePartner(
        Guid companyId, Guid partnerId, UpdateCompanyPartnerDto dto, CancellationToken ct)
    {
        var partner = await _repository.GetPartnerByIdAsync(partnerId, ct);
        if (partner is null || partner.CompanyId != companyId) return NotFound();

        partner.FullName = dto.FullName;
        partner.NationalIdNumber = dto.NationalIdNumber;
        partner.OwnershipPercentage = dto.OwnershipPercentage;
        partner.NationalIdDocumentId = dto.NationalIdDocumentId;

        _repository.UpdatePartner(partner);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // DELETE /api/companies/{companyId}/partners/{partnerId}
    [HttpDelete("{companyId:guid}/partners/{partnerId:guid}")]
    public async Task<ActionResult> DeletePartner(
        Guid companyId, Guid partnerId, CancellationToken ct)
    {
        var partner = await _repository.GetPartnerByIdAsync(partnerId, ct);
        if (partner is null || partner.CompanyId != companyId) return NotFound();

        _repository.SoftDeletePartner(partner);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }
}