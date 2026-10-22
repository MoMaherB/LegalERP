# 03: Extract CaseHearingsSection, Shared EntityDocumentsSection, and Polish CaseDetail Orchestrator

**What to build:**
Complete the deepening of `CaseDetail.razor` by extracting the Hearings & Postponements (`الحكم و التأجيل`) chain into `CaseHearingsSection`, generalizing general document management into a shared `EntityDocumentsSection` module reused by both Companies and Cases, and polishing `CaseDetail.razor` into a concise orchestrator:
- Extract `CaseHearingsSection` encapsulating the chronological Hearings & Postponements chain (`موعد الجلسة`, `نوع الجلسة`, `حكم`, `سبب التأجيل`), add/edit/delete actions, and RBAC checks (`CanEdit`, `CanDelete`).
- Generalize general entity document upload, preview, replacement, and deletion into a shared `EntityDocumentsSection` (absorbing the 10-line `DocumentFormattingHelper`) so both `CompanyDetail` (`"companydoc"`) and `CaseDetail` (`"case"`) use the same module seam.
- Polish `CaseDetail.razor` into a clean ~190-line orchestrator that loads the Case, resolves RBAC flags once, and delegates to `<CasePartiesSection>`, `<CaseMemosSection>`, `<CaseHearingsSection>`, `<EntityDocumentsSection>`, and `<SharedFinancials>`.

**Blocked by:** 01: Extract CasePartiesSection and Fix Client Party Resolution, 02: Extract CaseMemosSection and Fix Memo Document Ownership

**Status:** resolved

- [x] `CaseHearingsSection` encapsulates rendering, adding, editing, and deleting Case Hearings (`الحكم و التأجيل`) with `CanEdit` and `CanDelete` checks.
- [x] General document upload, size formatting, inline replacement, and deletion are unified so both `CompanyDetail` and `CaseDetail` delegate to a shared documents section module without duplicating markup or shallow formatting helpers.
- [x] `CaseDetail.razor` is reduced to a clean orchestrator (~190 lines) with no leftover unused state fields or dead handler methods.
- [x] Automated bUnit tests verify `CaseHearingsSection`, case documents management, and the full `CaseDetail` orchestrator composition, with the entire solution building and passing all tests.
