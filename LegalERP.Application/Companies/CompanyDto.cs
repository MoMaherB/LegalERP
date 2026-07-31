using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LegalERP.Domain.Enums;

namespace LegalERP.Application.Companies;

public record DocumentDto(
    Guid Id,
    string FileName,
    string StoredFileName,
    string ContentType,
    long FileSizeBytes
);

public record CompanyDto(
    Guid Id,
    CompanyCategory Category,
    string CompanyName,
    string? CompanyNameEn,
    string? TradeName,
    DateOnly? EstablishmentDate,
    string? RegistrationNumber,
    string? Address,
    Guid? IncorporationDocumentId,
    DocumentDto? IncorporationDocument,
    List<CompanyAmendmentDto> Amendments,
    List<CompanyPartnerDto> Partners
);

public record CompanyAmendmentDto(
    Guid Id,
    int SequenceNumber,
    string? Title,
    DateOnly? AmendmentDate,
    Guid? DocumentId,
    DocumentDto? Document
);

public record CompanyPartnerDto(
    Guid Id,
    Guid? ClientId,
    string FullName,
    string? NationalIdNumber,
    decimal? OwnershipPercentage,
    Guid? NationalIdDocumentId,
    DocumentDto? NationalIdDocument
);

public record CreateCompanyDto(
    CompanyCategory Category,
    string CompanyName,
    string? CompanyNameEn,
    string? TradeName,
    DateOnly? EstablishmentDate,
    string? RegistrationNumber,
    string? Address,
    Guid? IncorporationDocumentId
);

public record CreateCompanyAmendmentDto(
    string? Title,
    DateOnly? AmendmentDate,
    Guid? DocumentId
);

public record UpdateCompanyDto(
    CompanyCategory Category,
    string CompanyName,
    string? CompanyNameEn,
    string? TradeName,
    DateOnly? EstablishmentDate,
    string? RegistrationNumber,
    string? Address,
    Guid? IncorporationDocumentId
);

public record UpdateCompanyAmendmentDto(
    string? Title,
    DateOnly? AmendmentDate,
    Guid? DocumentId
);

public record CreateCompanyPartnerDto(
    Guid ClientId,
    decimal? OwnershipPercentage,
    string? FullName = null,
    string? NationalIdNumber = null,
    Guid? NationalIdDocumentId = null
);

public record UpdateCompanyPartnerDto(
    Guid ClientId,
    decimal? OwnershipPercentage,
    string? FullName = null,
    string? NationalIdNumber = null,
    Guid? NationalIdDocumentId = null
);