using LegalERP.Api.Common;
using LegalERP.Application.Clients;
using LegalERP.Application.Common;
using LegalERP.Application.Companies;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]   // All endpoints require authentication
public class ClientsController : ControllerBase
{
    private readonly IClientRepository _clientRepository;

    public ClientsController(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    // GET /api/clients
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default,
        [FromQuery] string? term = null)
    {
        if (!string.IsNullOrWhiteSpace(term))
        {
            return await Search(term, pageNumber, pageSize, ct);
        }

        if (pageNumber.HasValue || pageSize.HasValue)
        {
            var pagination = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 25 };
            var actionResult = await ExecutePagedResult(_clientRepository.GetPagedAsync(pagination, ct));
            return actionResult.Result!;
        }

        var clients = await _clientRepository.GetAllAsync(ct);
        var summaries = clients.Select(ToSummaryDto).ToList();
        return Ok(summaries);
    }

    // GET /api/clients/paged?pageNumber=1&pageSize=25
    [HttpGet("paged")]
    public Task<ActionResult<PagedResult<ClientSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var pagination = new PaginationParams { PageNumber = pageNumber, PageSize = pageSize };
        return ExecutePagedResult(_clientRepository.GetPagedAsync(pagination, ct));
    }

    // GET /api/clients/search?term=xxx
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? term = null,
        [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default)
    {
        if (pageNumber.HasValue || pageSize.HasValue)
        {
            var pagination = new PaginationParams { PageNumber = pageNumber ?? 1, PageSize = pageSize ?? 25 };
            var actionResult = await ExecutePagedResult(_clientRepository.SearchPagedAsync(term, pagination, ct));
            return actionResult.Result!;
        }

        var clients = await _clientRepository.SearchAsync(term, ct);
        var summaries = clients.Select(ToSummaryDto).ToList();
        return Ok(summaries);
    }

    // GET /api/clients/search/paged?term=xxx&pageNumber=1&pageSize=25
    [HttpGet("search/paged")]
    public Task<ActionResult<PagedResult<ClientSummaryDto>>> SearchPaged(
        [FromQuery] string? term,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var pagination = new PaginationParams { PageNumber = pageNumber, PageSize = pageSize };
        return ExecutePagedResult(_clientRepository.SearchPagedAsync(term, pagination, ct));
    }

    private async Task<ActionResult<PagedResult<ClientSummaryDto>>> ExecutePagedResult(
        Task<PagedResult<Client>> queryTask)
    {
        var paged = await queryTask;
        Response.SetPaginationHeaders(paged);

        var dtos = paged.Items.Select(ToSummaryDto).ToList();
        return Ok(new PagedResult<ClientSummaryDto>(dtos, paged.TotalCount, paged.PageNumber, paged.PageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var client = await _clientRepository.GetByIdAsync(id);
        if (client == null) return NotFound();

        return Ok(ToDto(client));
    }

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientDto dto)
    {
        var client = new Client
        {
            FileNumber = dto.FileNumber,
            FullName = dto.FullName,
            FullNameEn = dto.FullNameEn,
            NationalIdNumber = dto.NationalIdNumber,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            Address = dto.Address,
            Notes = dto.Notes,
            NationalIdDocumentId = dto.NationalIdDocumentId
        };

        await _clientRepository.AddAsync(client);
        await _clientRepository.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = client.Id }, ToSummaryDto(client));
    }

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientDto dto)
    {
        var client = await _clientRepository.GetByIdAsync(id);
        if (client == null) return NotFound();

        client.FileNumber = dto.FileNumber;
        client.FullName = dto.FullName;
        client.FullNameEn = dto.FullNameEn;
        client.NationalIdNumber = dto.NationalIdNumber;
        client.PhoneNumber = dto.PhoneNumber;
        client.Email = dto.Email;
        client.Address = dto.Address;
        client.Notes = dto.Notes;
        client.NationalIdDocumentId = dto.NationalIdDocumentId;

        _clientRepository.Update(client);
        await _clientRepository.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var client = await _clientRepository.GetByIdAsync(id);
        if (client == null) return NotFound();

        _clientRepository.SoftDelete(client);
        await _clientRepository.SaveChangesAsync();

        return NoContent();
    }

    private static ClientSummaryDto ToSummaryDto(Client client)
    {
        return new ClientSummaryDto(
            client.Id,
            client.FileNumber,
            client.FullName,
            client.NationalIdNumber,
            client.PhoneNumber
        );
    }

    private static ClientDto ToDto(Client client)
    {
        DocumentDto? nationalIdDoc = null;
        if (client.NationalIdDocument != null)
        {
            nationalIdDoc = new DocumentDto(
                client.NationalIdDocument.Id,
                client.NationalIdDocument.FileName,
                client.NationalIdDocument.StoredFileName,
                client.NationalIdDocument.ContentType,
                client.NationalIdDocument.FileSizeBytes
            );
        }

        var relatedCases = client.CaseParties.Select(party => new ClientCaseDto(
            party.CaseId,
            party.Case?.CaseNumber ?? "",
            party.Case?.Title ?? "",
            party.Case?.CaseType ?? default,
            party.Case?.Status ?? default,
            party.Case?.Outcome,
            party.Role,
            party.IsOurClient
        )).ToList();

        var relatedCompanies = client.CompanyPartnerships.Select(partner => new ClientCompanyDto(
            partner.CompanyId,
            partner.Company?.CompanyName ?? "",
            partner.OwnershipPercentage
        )).ToList();

        return new ClientDto(
            client.Id,
            client.FileNumber,
            client.FullName,
            client.FullNameEn,
            client.NationalIdNumber,
            client.PhoneNumber,
            client.Email,
            client.Address,
            client.Notes,
            nationalIdDoc,
            relatedCases,
            relatedCompanies
        );
    }
}
