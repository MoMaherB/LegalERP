using LegalERP.Application.Attorneys;
using LegalERP.Application.Companies;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttorneysController : ControllerBase
{
    private readonly IAttorneyRepository _attorneyRepository;

    public AttorneysController(IAttorneyRepository attorneyRepository)
    {
        _attorneyRepository = attorneyRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var attorneys = await _attorneyRepository.GetAllAsync();
        var dtos = attorneys.Select(ToSummaryDto);
        return Ok(dtos);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string term)
    {
        var attorneys = await _attorneyRepository.SearchAsync(term);
        var dtos = attorneys.Select(ToSummaryDto);
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var attorney = await _attorneyRepository.GetByIdAsync(id);
        if (attorney == null) return NotFound();
        return Ok(ToDto(attorney));
    }

    // Get all attorneys linked to a specific client (for client detail page)
    [HttpGet("by-client/{clientId:guid}")]
    public async Task<IActionResult> GetByClientId(Guid clientId)
    {
        var attorneys = await _attorneyRepository.GetByClientIdAsync(clientId);
        var dtos = attorneys.Select(a =>
        {
            DocumentDto? doc = null;
            if (a.Document != null)
            {
                doc = new DocumentDto(
                    a.Document.Id,
                    a.Document.FileName,
                    a.Document.StoredFileName,
                    a.Document.ContentType,
                    a.Document.FileSizeBytes
                );
            }
            return new ClientAttorneyDto(a.Id, a.AttorneyNumber, doc);
        });
        return Ok(dtos);
    }

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAttorneyDto dto)
    {
        var attorney = new Attorney
        {
            AttorneyNumber = dto.AttorneyNumber,
            Notes = dto.Notes,
            DocumentId = dto.DocumentId
        };

        await _attorneyRepository.AddAsync(attorney);
        await _attorneyRepository.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = attorney.Id }, ToSummaryDto(attorney));
    }

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAttorneyDto dto)
    {
        var attorney = await _attorneyRepository.GetByIdAsync(id);
        if (attorney == null) return NotFound();

        attorney.AttorneyNumber = dto.AttorneyNumber;
        attorney.Notes = dto.Notes;
        attorney.DocumentId = dto.DocumentId;
        attorney.UpdatedAt = DateTime.UtcNow;

        _attorneyRepository.Update(attorney);
        await _attorneyRepository.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var attorney = await _attorneyRepository.GetByIdAsync(id);
        if (attorney == null) return NotFound();

        _attorneyRepository.SoftDelete(attorney);
        await _attorneyRepository.SaveChangesAsync();

        return NoContent();
    }

    // ── Client linking ────────────────────────────────────────────────

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
    [HttpPost("{id:guid}/clients")]
    public async Task<IActionResult> AddClient(Guid id, [FromBody] AddAttorneyClientDto dto)
    {
        var attorney = await _attorneyRepository.GetByIdAsync(id);
        if (attorney == null) return NotFound();

        // Check if this client is already linked
        if (attorney.AttorneyClients.Any(ac => ac.ClientId == dto.ClientId))
            return Conflict(new { message = "Client is already linked to this attorney." });

        var link = new AttorneyClient
        {
            AttorneyId = id,
            ClientId = dto.ClientId
        };

        await _attorneyRepository.AddClientAsync(link);
        await _attorneyRepository.SaveChangesAsync();

        return Ok(new { id = link.Id });
    }

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
    [HttpDelete("{id:guid}/clients/{linkId:guid}")]
    public async Task<IActionResult> RemoveClient(Guid id, Guid linkId)
    {
        var link = await _attorneyRepository.GetAttorneyClientByIdAsync(linkId);
        if (link == null || link.AttorneyId != id) return NotFound();

        _attorneyRepository.SoftDeleteAttorneyClient(link);
        await _attorneyRepository.SaveChangesAsync();

        return NoContent();
    }

    // ── Mappers ───────────────────────────────────────────────────────

    private static AttorneySummaryDto ToSummaryDto(Attorney attorney)
    {
        return new AttorneySummaryDto(
            attorney.Id,
            attorney.AttorneyNumber,
            attorney.AttorneyClients?.Count ?? 0
        );
    }

    private static AttorneyDto ToDto(Attorney attorney)
    {
        DocumentDto? doc = null;
        if (attorney.Document != null)
        {
            doc = new DocumentDto(
                attorney.Document.Id,
                attorney.Document.FileName,
                attorney.Document.StoredFileName,
                attorney.Document.ContentType,
                attorney.Document.FileSizeBytes
            );
        }

        var clients = attorney.AttorneyClients?.Select(ac => new AttorneyClientDto(
            ac.Id,
            ac.ClientId,
            ac.Client?.FullName ?? "",
            ac.Client?.NationalIdNumber,
            ac.Client?.FileNumber
        )).ToList() ?? new();

        return new AttorneyDto(
            attorney.Id,
            attorney.AttorneyNumber,
            attorney.Notes,
            doc,
            clients
        );
    }
}
