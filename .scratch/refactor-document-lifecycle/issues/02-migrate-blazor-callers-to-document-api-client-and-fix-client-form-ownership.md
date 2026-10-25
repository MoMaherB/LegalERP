# 02: Migrate All Blazor Callers to DocumentApiClient and Fix ClientForm Document Ownership

**What to build:**
Complete the migration of all Blazor document upload, replace, and delete callers to `DocumentApiClient` and remove the misplaced/duplicated document methods from `CompanyApiClient` and `AttorneyApiClient`:
- Migrate `CompanyDetail.razor`, `CompanyAmendmentsSection.razor`, `CompanyPartnersSection.razor`, `CompanyForm.razor`, `CaseMemosSection.razor`, `ClientForm.razor`, `AttorneyDetail.razor`, and `AttorneyForm.razor` to inject and use `DocumentApiClient` and `DocumentOwnerTypes`.
- Use `DocumentApiClient.ReplaceDocumentAsync` for single-slot replacements (`IncorporationDocument` in `CompanyDetail.razor`, `PartnersIdsDocument` / `PartnersAttorneysDocument` in `CompanyPartnersSection.razor`, `Amendment.Document` in `CompanyAmendmentsSection.razor`, `Memo.Document` in `CaseMemosSection.razor` when a document already exists, and `Attorney.Document` in `AttorneyDetail.razor`) so callers no longer need multi-trip upload + parent `PUT` + old-document `DELETE` ceremonies when replacing an existing file.
- Fix `ClientForm.razor` so creating a new client with a National ID scan creates the client first (`ClientApi.CreateAsync`) and then uploads the document with `ownerId = created.Id` and updates the client, instead of uploading with a throwaway `Guid.NewGuid()`.
- Remove `GetDocumentsAsync`, `UploadDocumentAsync`, and `DeleteDocumentAsync` from `CompanyApiClient.cs` and `UploadDocumentAsync` from `AttorneyApiClient.cs`, and update all affected tests in `LegalERP.Tests`.

**Blocked by:** 01: Introduce Canonical DocumentOwnerTypes, Atomic Replace Endpoint, and DocumentApiClient

**Status:** resolved

- [x] All Blazor components and forms (`CompanyDetail`, `CompanyAmendmentsSection`, `CompanyPartnersSection`, `CompanyForm`, `CaseMemosSection`, `ClientForm`, `AttorneyDetail`, `AttorneyForm`) use `DocumentApiClient` and `DocumentOwnerTypes`.
- [x] Single-slot document replacements use `DocumentApiClient.ReplaceDocumentAsync` instead of creating orphaned `Document` rows or reconstructing full parent DTOs.
- [x] Creating a new client with a National ID scan in `ClientForm.razor` stores the document under the newly created `Client.Id` rather than `Guid.NewGuid()`.
- [x] Document methods are removed from `CompanyApiClient.cs` and `AttorneyApiClient.cs`, and all tests in `LegalERP.Tests` pass.
