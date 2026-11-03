# 03: Review and Test Power of Attorney (التوكيلات) Controller, Create/Edit Form, and List View

**What to build:** Review, polish card layout on `AttorneyForm`, and add comprehensive unit and bUnit tests for the Power of Attorney (`التوكيلات`) CRUD lifecycle across `AttorneysController`, `AttorneyForm`, and `AttorneyList`.

**Blocked by:** 02: Fix Client 360° Profile Party Role & Case Type Localization, Card Layout, and Test the Clients Slice

**Status:** ready-for-agent

- [ ] `AttorneyForm` cards use `flex-column align-items-stretch` so form sections stack cleanly.
- [ ] Automated unit tests cover `AttorneysController` (GetAll, Search, GetById, GetByClientId, Create, Update, Delete, linking a client via `AddClient`, returning `409 Conflict` when linking an already-linked client, and unlinking a client via `RemoveClient`).
- [ ] Automated bUnit tests cover `AttorneyForm` in both Create mode (creating the attorney first, then uploading the document scan with `OwnerId == created.Id` and updating the attorney's `DocumentId`) and Edit mode (replacing or preserving the existing document via `DocumentApiClient.UploadOrReplaceDocumentAsync`).
- [ ] Automated bUnit tests cover `AttorneyList` (rendering attorneys with client counts, searching by attorney number, and confirming before deletion).
