using System;
using System.Collections.Generic;
using LegalERP.Application.Companies;

namespace LegalERP.Application.Attorneys;

// Full attorney with its clients
public record AttorneyDto(
    Guid Id,
    string AttorneyNumber,
    string? Notes,
    DocumentDto? Document,
    List<AttorneyClientDto> Clients
);

// Client info within an attorney
public record AttorneyClientDto(
    Guid Id,          // AttorneyClient join ID
    Guid ClientId,
    string ClientFullName,
    string? ClientNationalId,
    string? ClientFileNumber
);

// For list/search results
public record AttorneySummaryDto(
    Guid Id,
    string AttorneyNumber,
    int ClientCount
);

public record CreateAttorneyDto(
    string AttorneyNumber,
    string? Notes,
    Guid? DocumentId
);

public record UpdateAttorneyDto(
    string AttorneyNumber,
    string? Notes,
    Guid? DocumentId
);

// For adding a client to an attorney
public record AddAttorneyClientDto(Guid ClientId);

// For showing attorneys on the Client detail page (read-only reflection)
public record ClientAttorneyDto(
    Guid AttorneyId,
    string AttorneyNumber,
    DocumentDto? Document
);
