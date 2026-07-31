using System;
using System.Collections.Generic;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;

namespace LegalERP.Application.Clients;

public record ClientDto(
    Guid Id,
    string FullName,
    string? FullNameEn,
    string? NationalIdNumber,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? Notes,
    DocumentDto? NationalIdDocument,
    DocumentDto? AttorneyDocument,
    List<ClientCaseDto> RelatedCases,
    List<ClientCompanyDto> RelatedCompanies
);

public record ClientSummaryDto(
    Guid Id,
    string FullName,
    string? NationalIdNumber,
    string? PhoneNumber
);

public record CreateClientDto(
    string FullName,
    string? FullNameEn,
    string? NationalIdNumber,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? Notes,
    Guid? NationalIdDocumentId,
    Guid? AttorneyDocumentId
);

public record UpdateClientDto(
    string FullName,
    string? FullNameEn,
    string? NationalIdNumber,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? Notes,
    Guid? NationalIdDocumentId,
    Guid? AttorneyDocumentId
);

public record ClientCaseDto(
    Guid CaseId,
    string CaseNumber,
    string CaseTitle,
    CaseType CaseType,
    CaseStatus CaseStatus,
    CaseOutcome? CaseOutcome,
    PartyRole PartyRole,
    bool IsOurClient
);

public record ClientCompanyDto(
    Guid CompanyId,
    string CompanyName,
    decimal? OwnershipPercentage
);
