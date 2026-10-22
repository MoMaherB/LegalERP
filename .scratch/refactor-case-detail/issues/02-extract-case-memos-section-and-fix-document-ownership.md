# 02: Extract CaseMemosSection and Fix Memo Document Ownership

**What to build:**
Extract the Legal Memos & Briefs (`مذكرات`) lifecycle out of `CaseDetail.razor` into a self-contained `CaseMemosSection` module and fix the broken document ownership sequence when attaching or replacing memo files:
- Extract `CaseMemosSection` encapsulating the ordered Legal Memos list, create/edit form (title, date, content, file attachment), `FileThumbnail` document previews, inline file replacement, delete confirmations, and RBAC checks (`CanEdit`, `CanDelete`).
- Fix the memo creation and replacement flow so attached `Document` records are always uploaded and stored with `OwnerType = "casememo"` and `OwnerId = CaseMemo.Id` (never a throwaway `Guid.NewGuid()`), and replacing a memo's file cleans up the superseded `Document`.
- Delegate Legal Memos rendering in `CaseDetail.razor` to `<CaseMemosSection>`.

**Blocked by:** 01: Extract CasePartiesSection and Fix Client Party Resolution

**Status:** resolved

- [x] `CaseMemosSection` encapsulates displaying, creating, editing, and deleting Legal Memos (`مذكرات`) with `CanEdit` and `CanDelete` authorization checks.
- [x] Creating a Legal Memo with a file attachment persists the `Document` with `OwnerType = "casememo"` and `OwnerId` equal to the created `CaseMemo.Id` (no random `Guid.NewGuid()` `OwnerId`).
- [x] Replacing an existing Legal Memo attachment updates the memo's `DocumentId`, deletes the superseded `Document`, and refreshes the UI.
- [x] `CaseDetail.razor` delegates Legal Memos rendering to `<CaseMemosSection>`.
- [x] Automated unit and bUnit tests verify memo creation, document ownership (`OwnerId == CaseMemo.Id`), inline file replacement, and RBAC visibility.
