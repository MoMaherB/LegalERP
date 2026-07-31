using LegalERP.Application.Clients;
using LegalERP.Application.Companies;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IClientRepository _clientRepository;

    public ClientsController(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var clients = await _clientRepository.GetAllAsync();
        var dtos = clients.Select(ToSummaryDto);
        return Ok(dtos);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string term)
    {
        var clients = await _clientRepository.SearchAsync(term);
        var dtos = clients.Select(ToSummaryDto);
        return Ok(dtos);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var client = await _clientRepository.GetByIdAsync(id);
        if (client == null) return NotFound();

        return Ok(ToDto(client));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientDto dto)
    {
        var client = new Client
        {
            FullName = dto.FullName,
            FullNameEn = dto.FullNameEn,
            NationalIdNumber = dto.NationalIdNumber,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            Address = dto.Address,
            Notes = dto.Notes,
            NationalIdDocumentId = dto.NationalIdDocumentId,
            AttorneyDocumentId = dto.AttorneyDocumentId
        };

        await _clientRepository.AddAsync(client);
        await _clientRepository.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = client.Id }, ToSummaryDto(client));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientDto dto)
    {
        var client = await _clientRepository.GetByIdAsync(id);
        if (client == null) return NotFound();

        client.FullName = dto.FullName;
        client.FullNameEn = dto.FullNameEn;
        client.NationalIdNumber = dto.NationalIdNumber;
        client.PhoneNumber = dto.PhoneNumber;
        client.Email = dto.Email;
        client.Address = dto.Address;
        client.Notes = dto.Notes;
        client.NationalIdDocumentId = dto.NationalIdDocumentId;
        client.AttorneyDocumentId = dto.AttorneyDocumentId;

        _clientRepository.Update(client);
        await _clientRepository.SaveChangesAsync();

        return NoContent();
    }

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

        DocumentDto? attorneyDoc = null;
        if (client.AttorneyDocument != null)
        {
            attorneyDoc = new DocumentDto(
                client.AttorneyDocument.Id,
                client.AttorneyDocument.FileName,
                client.AttorneyDocument.StoredFileName,
                client.AttorneyDocument.ContentType,
                client.AttorneyDocument.FileSizeBytes
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
            client.FullName,
            client.FullNameEn,
            client.NationalIdNumber,
            client.PhoneNumber,
            client.Email,
            client.Address,
            client.Notes,
            nationalIdDoc,
            attorneyDoc,
            relatedCases,
            relatedCompanies
        );
    }
}
