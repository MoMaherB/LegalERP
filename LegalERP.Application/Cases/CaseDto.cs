using System;
using System.Collections.Generic;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;

namespace LegalERP.Application.Cases;

public record CasePartyDto(
    Guid Id,
    Guid? ClientId,
    string FullName,
    PartyRole Role,
    bool IsOurClient,
    string? NationalIdNumber,
    string? PhoneNumber,
    string? Notes,
    Guid? DocumentId,
    DocumentDto? Document
);

public record CreateCasePartyDto(
    Guid? ClientId,
    string FullName,
    PartyRole Role,
    bool IsOurClient,
    string? NationalIdNumber,
    string? PhoneNumber,
    string? Notes,
    Guid? DocumentId
);

public record UpdateCasePartyDto(
    Guid? ClientId,
    string FullName,
    PartyRole Role,
    bool IsOurClient,
    string? NationalIdNumber,
    string? PhoneNumber,
    string? Notes,
    Guid? DocumentId
);

public record CaseMemoDto(
    Guid Id,
    string Title,
    string? Content,
    DateOnly MemoDate,
    Guid? DocumentId,
    DocumentDto? Document
);

public record CreateCaseMemoDto(
    string Title,
    string? Content,
    DateOnly? MemoDate,
    Guid? DocumentId
);

public record UpdateCaseMemoDto(
    string Title,
    string? Content,
    DateOnly? MemoDate,
    Guid? DocumentId
);

public record CaseHearingDto(
    Guid Id,
    DateOnly HearingDate,
    string? Purpose,
    string? JudgeDecision,
    string? PostponementReason
);

public record CreateCaseHearingDto(
    DateOnly HearingDate,
    string? Purpose,
    string? JudgeDecision,
    string? PostponementReason
);

public record UpdateCaseHearingDto(
    DateOnly HearingDate,
    string? Purpose,
    string? JudgeDecision,
    string? PostponementReason
);

public record CaseDto(
    Guid Id,
    string CaseNumber,
    string Title,
    CaseType CaseType,
    CaseStatus Status,
    CaseOutcome? Outcome,
    DateOnly? FilingDate,
    string? CourtName,
    string? JudgeName,
    string? Notes,
    List<CasePartyDto> Parties,
    List<CaseMemoDto> Memos,
    List<CaseHearingDto> Hearings
);

public record CreateCaseDto(
    string CaseNumber,
    string Title,
    CaseType CaseType,
    CaseStatus Status,
    CaseOutcome? Outcome,
    DateOnly? FilingDate,
    string? CourtName,
    string? JudgeName,
    string? Notes
);

public record UpdateCaseDto(
    string CaseNumber,
    string Title,
    CaseType CaseType,
    CaseStatus Status,
    CaseOutcome? Outcome,
    DateOnly? FilingDate,
    string? CourtName,
    string? JudgeName,
    string? Notes
);
