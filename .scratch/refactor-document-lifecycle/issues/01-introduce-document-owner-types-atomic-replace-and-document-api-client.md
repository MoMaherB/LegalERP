# 01: Introduce Canonical DocumentOwnerTypes, Atomic Replace Endpoint, and DocumentApiClient

**What to build:**
Establish a single polymorphic Document management seam across `LegalERP.Application`, `LegalERP.Api`, and `LegalERP.Web`:
- Create `DocumentOwnerTypes` in `LegalERP.Application/Common/DocumentOwnerTypes.cs` defining the canonical owner type constants (`Company`, `CompanyDoc`, `Amendment`, `PartnerIds`, `PartnerAttorneys`, `Client`, `Case`, `CaseMemo`, `CaseParty`, `Attorney`, `Users`).
- Add `PUT api/documents/{id:guid}/replace` (`[Authorize(Roles = "SuperAdmin,Admin,Editor")]`) to `LegalERP.Api/Controllers/DocumentsController.cs` to validate the file, save the new file via `IFileStorageService`, delete the old file via `IFileStorageService`, and update `Document` (`FileName`, `StoredFileName`, `ContentType`, `FileSizeBytes`, `UpdatedAt`) in-place without changing `Document.Id`.
- Tighten `DELETE api/documents/{id:guid}` in `DocumentsController.cs` from `[Authorize(Roles = "SuperAdmin,Admin,Editor")]` to `[Authorize(Roles = "SuperAdmin,Admin")]` per `AGENTS.md` TR-3.2.
- Create `DocumentApiClient` in `LegalERP.Web/Services/DocumentApiClient.cs` (`GetDocumentsAsync`, `UploadDocumentAsync`, `ReplaceDocumentAsync`, `DeleteDocumentAsync`), register it in `LegalERP.Web/Program.cs`, and migrate `EntityDocumentsSection.razor` to use `DocumentApiClient` (including `ReplaceDocumentAsync` for in-place document replacement).

**Blocked by:** None

**Status:** resolved

- [x] `DocumentOwnerTypes` constants exist in `LegalERP.Application/Common/DocumentOwnerTypes.cs`.
- [x] `DocumentsController` exposes `PUT api/documents/{id:guid}/replace` (`SuperAdmin,Admin,Editor`) that updates the `Document` row in-place and removes the superseded file via `IFileStorageService`, and restricts `DELETE api/documents/{id:guid}` to `SuperAdmin,Admin`.
- [x] `DocumentApiClient` is registered in `LegalERP.Web/Program.cs` and `EntityDocumentsSection.razor` uses `DocumentApiClient` (with `ReplaceDocumentAsync` instead of upload + delete).
- [x] Automated tests in `LegalERP.Tests` cover `DocumentsController` (upload, replace, delete RBAC) and `EntityDocumentsSection.razor`.
