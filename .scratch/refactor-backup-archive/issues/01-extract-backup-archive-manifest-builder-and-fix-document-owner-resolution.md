---
id: 01
title: Extract BackupArchiveManifestBuilder and fix silent backup data loss for Amendments, Case Memos, and Bulk Partner Documents
status: resolved
depends_on: []
---

# Extract `BackupArchiveManifestBuilder` & Fix Silent Backup Data Loss

## Goal

Complete the final candidate (Candidate 5) from the Architecture Review by:
1. Fixing silent backup omissions where Company Amendment contracts (`عقود التعديل`), Case Legal Memos (`المذكرات القانونية`), and Company Bulk Partner documents (`بطاقات الشركاء` / `توكيلات الشركاء`) were missing from exported archives.
2. Extracting a pure, in-process `BackupArchiveManifestBuilder` in `LegalERP.Application/Backups/` so the Arabic folder hierarchy, filename sanitization (`SanitizeName`), duplicate filename numbering (`(2)`, `(3)`), and `Document` source resolution (`doc.OwnerType` + `doc.OwnerId` + `doc.StoredFileName`) are testable in memory without touching the filesystem.

## Root Cause of Missing Backup Files

1. **Company Amendments (`BackupArchiveService.cs:124`):** Uploaded with `OwnerType = "amendment"` and `OwnerId = amend.Id`, but `BackupArchiveService` looked in `uploads/company/{comp.Id}/`.
2. **Case Legal Memos (`BackupArchiveService.cs:214`):** Uploaded with `OwnerType = "casememo"` and `OwnerId = memo.Id`, but `BackupArchiveService` looked in `uploads/memo/{memo.Id}/`.
3. **Bulk Partner Documents (`HasManyPartners`):** `Company.PartnersIdsDocument` (`partnerids`) and `Company.PartnersAttorneysDocument` (`partnerattorneys`) were not `.Include()`d or exported in `BackupArchiveService`.

## Scope & Requirements

1. **`BackupArchiveManifestBuilder` (`LegalERP.Application/Backups/BackupArchiveManifestBuilder.cs`):**
   - Define `BackupArchiveEntry(Document SourceDocument, string RelativeDirectory, string FileName)` and `BackupArchiveManifest(IReadOnlyList<string> Directories, IReadOnlyList<BackupArchiveEntry> Entries)`.
   - Implement `BackupArchiveManifestBuilder.SanitizeName(string? name)` (preserving existing sanitization rules: replacing `/ \ : * ? " < > |` with `-`, collapsing spaces/hyphens, defaulting blank names to `"بدون اسم"` or `"ملف"`).
   - Implement `BackupArchiveManifestBuilder.Build(...)` taking companies, general `companydoc` documents, clients, cases, general `case` documents, and attorneys:
     - **Companies (`companies/{FileNumber - CompanyName}`):**
       - Incorporation contract: `عقد التأسيس_{FileNumber ?? CompanyName}{ext}`
       - Bulk Partner IDs (`PartnersIdsDocument`): `بطاقات الشركاء_{FileNumber ?? CompanyName}{ext}`
       - Bulk Partner Attorneys (`PartnersAttorneysDocument`): `توكيلات الشركاء_{FileNumber ?? CompanyName}{ext}`
       - Amendments (`التعديلات/`): ordered by `SequenceNumber`, named `عقد التعديل رقم {SequenceNumber}{ext}`, referencing `amend.Document` directly (preserving `OwnerType = "amendment"`, `OwnerId = amend.Id`).
       - General Company Docs (`سجلات وتراخيص الشركة/`): referencing each `gdoc` (`OwnerType = "companydoc"`, `OwnerId = comp.Id`).
     - **Clients (`clients/{FileNumber - FullName}`):**
       - National ID scan: `صورة الهوية_{FileNumber ?? FullName}{ext}`, referencing `client.NationalIdDocument`.
     - **Cases (`cases/قضية {CaseNumber} - {CourtName}`):**
       - Legal Memos (`المذكرات القانونية/`): named `{Title or "مذكرة دفاع"}{ext}`, referencing `memo.Document` directly (preserving `OwnerType = "casememo"`, `OwnerId = memo.Id`).
       - General Case Docs (`المستندات والمستخرجات/`): referencing each `doc` (`OwnerType = "case"`, `OwnerId = case.Id`).
     - **Attorneys (`attornies/`):**
       - Power of Attorney scan: `توكيل_{AttorneyNumber}{ext}`, referencing `att.Document`.

2. **`BackupArchiveService.cs` Refactor:**
   - Include `PartnersIdsDocument` and `PartnersAttorneysDocument` when querying `_db.Companies`.
   - Delegate manifest construction to `BackupArchiveManifestBuilder.Build(...)`.
   - Replace `LinkOrCopyFile(string ownerType, Guid ownerId, string storedFileName, ...)` with `LinkOrCopyDocument(Document doc, string targetDir, string desiredFileName, out long fileSize)` that reads `doc.OwnerType`, `doc.OwnerId`, and `doc.StoredFileName` directly from the `Document` entity (with fallback if `doc.OwnerType` is empty).

## Acceptance Criteria

- [ ] `BackupArchiveManifestBuilder` builds the complete archive directory and file manifest in memory and resolves `SourceDocument.OwnerType` and `SourceDocument.OwnerId` directly from each `Document`.
- [ ] Company Amendments (`amendment`), Company Bulk Partner Documents (`partnerids`, `partnerattorneys`), and Case Memos (`casememo`) are included in the manifest and copied/hardlinked from their true storage paths on disk.
- [ ] Unit and integration tests in `LegalERP.Tests/Backups/` verify both pure manifest generation (`BackupArchiveManifestBuilderTests`) and end-to-end physical archive generation (`BackupArchiveServiceTests`) in a temp directory.
- [ ] Full test suite (`dotnet test LegalERP.Tests/LegalERP.Tests.csproj`) passes with 0 failures.
