# 01: Extract CompanyAmendmentsSection

**What to build:** 
Extract the amendment contracts lifecycle out of the monolithic `CompanyDetail.razor` into an independent, deep component `CompanyAmendmentsSection.razor`. The component encapsulates:
- Rendering the ordered list of company amendment contracts.
- Automatic Arabic sequence title generation (`عقد التعديل الأول`, `الثاني`, etc.).
- Form state for adding a new amendment with date, title, and PDF/image contract file upload.
- Edit and delete actions with permission checks (`CanEdit`, `CanDelete`).
- Replacing existing amendment contract files inline.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] `CompanyAmendmentsSection.razor` created under `LegalERP.Web/Components/Pages/Companies/Sections/`.
- [x] Accepts parameters for `Company`, `CanEdit`, `CanDelete`, and `OnChanged` callback.
- [x] Preserves automatic ordinal numbering (`الأول`, `الثاني`...).
- [x] Preserves file upload, contract thumbnail replacement, edit, and delete actions.
- [x] `CompanyDetail.razor` delegates amendments rendering to `<CompanyAmendmentsSection>`.
- [x] Solution builds with 0 errors and amendment functionality is completely intact.
