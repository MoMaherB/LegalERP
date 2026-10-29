# 03: Sync Developer's Full System Disaster-Recovery Backup (Card 2) to Google Drive via IRcloneRunner

**What to build:**
1. Add `Task<BackupResultDto> SyncDeveloperSystemBackupAsync(CancellationToken ct = default)` to `IBackupArchiveService` and implement it in `BackupArchiveService`:
   - Creates a `BackupRecord` with `BackupType = BackupType.DatabaseSnapshot` and `Status = BackupStatus.Running`.
   - Runs `pg_dump -F c` to produce `legalerp_db_YYYYMMDD_HHmmss.dump` in temp storage.
   - Calls `await _rcloneRunner.CopyFileAsync(tempDumpFile, $"{devRemote}/Database", ct)` to upload the database dump to Google Drive (`gdrive:LegalERP_Test/Dev_SystemBackup/Database`).
   - Calls `await _rcloneRunner.SyncDirectoryAsync(_storageBasePath, $"{devRemote}/uploads", ct)` to incrementally sync all raw uploaded files (`uploads/{ownerType}/{ownerId}/{storedFileName}`, including `users/` profile pictures) to Google Drive (`gdrive:LegalERP_Test/Dev_SystemBackup/uploads`).
   - Computes total file count (1 database dump + raw uploaded files count) and total bytes, marks the `BackupRecord` `Completed` with `DestinationPath = devRemote`, deletes the temporary `.dump` file, and returns `BackupResultDto`.
2. Expose `POST api/backups/sync-developer-backup` (`[Authorize(Roles = "SuperAdmin")]`) on `BackupsController` and `SyncDeveloperBackupAsync()` on `BackupApiClient`.
3. Update Card 2 in `BackupManagement.razor` (`/admin/backups`) to add a primary **"مزامنة نسخة النظام الشاملة"** button (with loading spinner and toast notification that refreshes `LoadData()` so "آخر نسخة لقاعدة البيانات" and the Backup History table update live on screen) alongside the existing local `.dump` download link.

**Blocked by:** 01: Shared IRcloneRunner Seam & Omit Empty Archive Directories, 02: Sync Lawyer's Organized Archive (Card 1) to Google Drive via IRcloneRunner

**Status:** ready-for-agent

- [ ] `POST api/backups/sync-developer-backup` runs `pg_dump`, uploads the `.dump` file to `{DeveloperRemote}/Database` via `IRcloneRunner.CopyFileAsync`, incrementally syncs `uploads/` to `{DeveloperRemote}/uploads` via `IRcloneRunner.SyncDirectoryAsync`, and saves a completed `DatabaseSnapshot` `BackupRecord`.
- [ ] Card 2 on `BackupManagement.razor` provides a button to trigger `SyncDeveloperBackupAsync()`, shows a spinner while syncing, displays a toast on completion, and immediately refreshes `summary` ("آخر نسخة لقاعدة البيانات") and `history`.
- [ ] Automated tests in `LegalERP.Tests/Backups/` verify `SyncDeveloperSystemBackupAsync`, `BackupsController.SyncDeveloperBackup`, and the Card 2 button behavior.
