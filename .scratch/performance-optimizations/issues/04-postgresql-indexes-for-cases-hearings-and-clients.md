# 04: PostgreSQL Performance Indexes for Cases, Hearings, and Clients

**What to build:** Add explicit database indexes in Entity Framework Core model configurations on frequently searched, filtered, and foreign-key columns, eliminating full-table sequential scans as data multiplies in production.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] EF Core entity configurations in `ApplicationDbContext` (or entity type configurations) define indexes for:
  - `case_hearings`: Composite index on `(HearingDate, IsDeleted)` and index on `CaseId`.
  - `cases`: Index on `CaseNumber`, composite index on `(Status, IsDeleted)`, and index on `FilingDate`.
  - `clients`: Index on `NationalIdNumber` and composite index on `(FullName, IsDeleted)`.
  - `companies`: Index on `RegistrationNumber` and `Category`.
- [x] A clean EF Core migration is generated and tested to ensure it applies cleanly to the PostgreSQL database without breaking existing records.
- [x] Automated tests verify index definitions and ensure query execution continues returning accurate results.
