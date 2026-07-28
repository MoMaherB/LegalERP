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
    string FullName,
    string? NationalIdNumber,
    decimal? OwnershipPercentage,
    Guid? NationalIdDocumentId
);

public record UpdateCompanyPartnerDto(
    string FullName,
    string? NationalIdNumber,
    decimal? OwnershipPercentage,
    Guid? NationalIdDocumentId
);