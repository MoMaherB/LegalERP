# 03: Extract CompanyDocumentsSection and Polish CompanyDetail

**What to build:**
Extract the general company documents grid and file uploader out of `CompanyDetail.razor` into `CompanyDocumentsSection.razor`. Slim down the main `CompanyDetail.razor` into a clean, concise orchestrator (~150 lines) that handles:
- Page routing (`/companies/{id}`) and title.
- Header card with company basic data (File Number, Category, Trade Name, Registration, Address).
- Articles of Incorporation document card with replacement modal.
- Orchestrating the three child sections: `<CompanyPartnersSection>`, `<CompanyAmendmentsSection>`, `<CompanyDocumentsSection>`.
- Top-level permission loading (`_canEdit`, `_canDelete`).

**Blocked by:** 02: Extract CompanyPartnersSection

**Status:** resolved

- [x] `CompanyDocumentsSection.razor` created under `LegalERP.Web/Components/Pages/Companies/Sections/`.
- [x] Handles general document upload, file type checking, and document listing.
- [x] `CompanyDetail.razor` reduced from 1,061 lines to ~240 lines (clean orchestrator).
- [x] No regression: all actions (edit company, delete company, view contract, print, back) remain intact.
- [x] Clean build with 0 errors and all 35 tests passing.
