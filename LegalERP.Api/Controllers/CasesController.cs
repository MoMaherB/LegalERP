using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Cases;
using LegalERP.Application.Companies;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CasesController : ControllerBase
{
    private readonly ICaseRepository _repository;

    public CasesController(ICaseRepository repository) => _repository = repository;

    // GET /api/cases
    [HttpGet]
    public async Task<ActionResult<List<CaseDto>>> GetAll(CancellationToken ct)
    {
        var cases = await _repository.GetAllAsync(ct);
        return Ok(cases.Select(ToDto));
    }

    // GET /api/cases/search?term=xxx&type=Criminal&status=Active
    [HttpGet("search")]
    public async Task<ActionResult<List<CaseDto>>> Search(
        [FromQuery] string? term, [FromQuery] CaseType? type, [FromQuery] CaseStatus? status, CancellationToken ct)
    {
        var cases = await _repository.SearchAsync(term, type, status, ct);
        return Ok(cases.Select(ToDto));
    }

    // GET /api/cases/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CaseDto>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null) return NotFound();

        return Ok(ToDto(entity));
    }

    // POST /api/cases
    [HttpPost]
    public async Task<ActionResult<CaseDto>> Create(CreateCaseDto dto, CancellationToken ct)
    {
        var entity = new Case
        {
            CaseNumber = dto.CaseNumber,
            Title = dto.Title,
            CaseType = dto.CaseType,
            Status = dto.Status,
            Outcome = dto.Status == CaseStatus.Closed ? dto.Outcome : null,
            FilingDate = dto.FilingDate,
            CourtName = dto.CourtName,
            JudgeName = dto.JudgeName,
            Notes = dto.Notes
        };

        await _repository.AddAsync(entity, ct);
        await _repository.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
    }

    // PUT /api/cases/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, UpdateCaseDto dto, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null) return NotFound();

        entity.CaseNumber = dto.CaseNumber;
        entity.Title = dto.Title;
        entity.CaseType = dto.CaseType;
        entity.Status = dto.Status;
        entity.Outcome = dto.Status == CaseStatus.Closed ? dto.Outcome : null;
        entity.FilingDate = dto.FilingDate;
        entity.CourtName = dto.CourtName;
        entity.JudgeName = dto.JudgeName;
        entity.Notes = dto.Notes;

        _repository.Update(entity);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // DELETE /api/cases/{id}
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(id, ct);
        if (entity is null) return NotFound();

        _repository.SoftDelete(entity);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // --- Case Parties endpoints ---

    // POST /api/cases/{caseId}/parties
    [HttpPost("{caseId:guid}/parties")]
    public async Task<ActionResult<CasePartyDto>> AddParty(Guid caseId, CreateCasePartyDto dto, CancellationToken ct)
    {
        var c = await _repository.GetByIdAsync(caseId, ct);
        if (c is null) return NotFound();

        var party = new CaseParty
        {
            CaseId = caseId,
            ClientId = dto.ClientId,
            FullName = dto.FullName,
            Role = dto.Role,
            IsOurClient = dto.IsOurClient,
            NationalIdNumber = dto.NationalIdNumber,
            PhoneNumber = dto.PhoneNumber,
            Notes = dto.Notes,
            DocumentId = dto.DocumentId
        };

        await _repository.AddPartyAsync(party, ct);
        await _repository.SaveChangesAsync(ct);

        var partyWithDoc = await _repository.GetPartyByIdAsync(party.Id, ct);
        return Ok(ToPartyDto(partyWithDoc ?? party));
    }

    // PUT /api/cases/{caseId}/parties/{partyId}
    [HttpPut("{caseId:guid}/parties/{partyId:guid}")]
    public async Task<ActionResult> UpdateParty(Guid caseId, Guid partyId, UpdateCasePartyDto dto, CancellationToken ct)
    {
        var party = await _repository.GetPartyByIdAsync(partyId, ct);
        if (party is null || party.CaseId != caseId) return NotFound();

        party.ClientId = dto.ClientId;
        party.FullName = dto.FullName;
        party.Role = dto.Role;
        party.IsOurClient = dto.IsOurClient;
        party.NationalIdNumber = dto.NationalIdNumber;
        party.PhoneNumber = dto.PhoneNumber;
        party.Notes = dto.Notes;
        party.DocumentId = dto.DocumentId;

        _repository.UpdateParty(party);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // DELETE /api/cases/{caseId}/parties/{partyId}
    [HttpDelete("{caseId:guid}/parties/{partyId:guid}")]
    public async Task<ActionResult> DeleteParty(Guid caseId, Guid partyId, CancellationToken ct)
    {
        var party = await _repository.GetPartyByIdAsync(partyId, ct);
        if (party is null || party.CaseId != caseId) return NotFound();

        _repository.SoftDeleteParty(party);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    private static CaseDto ToDto(Case c) => new(
        c.Id,
        c.CaseNumber,
        c.Title,
        c.CaseType,
        c.Status,
        c.Outcome,
        c.FilingDate,
        c.CourtName,
        c.JudgeName,
        c.Notes,
        c.Parties.Select(ToPartyDto).ToList()
    );

    private static CasePartyDto ToPartyDto(CaseParty p) => new(
        p.Id,
        p.ClientId,
        p.FullName,
        p.Role,
        p.IsOurClient,
        p.NationalIdNumber,
        p.PhoneNumber,
        p.Notes,
        p.DocumentId,
        p.Document == null ? null : new DocumentDto(p.Document.Id, p.Document.FileName, p.Document.StoredFileName, p.Document.ContentType, p.Document.FileSizeBytes)
    );
}
