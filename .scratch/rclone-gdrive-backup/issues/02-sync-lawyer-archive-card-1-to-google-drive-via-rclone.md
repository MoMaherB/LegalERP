# 02: Sync Lawyer's Organized Archive (Card 1) to Google Drive via IRcloneRunner

**What to build:**
1. Inject `IRcloneRunner` into `BackupArchiveService`.
2. In `BackupArchiveService.GenerateHumanReadableArchiveAsync`:
   - Clean any stale empty folders or outdated links in the local `legal_archive` staging directory before building the fresh hardlinked tree from `BackupArchiveManifestBuilder`.
   - When `LawyerRemote` is configured (and `customTargetPath` is not an explicit local-only test override), invoke `await _rcloneRunner.SyncDirectoryAsync(targetBase, lawyerRemote, ct)` to incrementally sync only new/changed Arabic folders and files to `gdrive:LegalERP_Test/Lawyer_Archive` on Google Drive.
   - Store the synced Google Drive remote path (`gdrive:LegalERP_Test/Lawyer_Archive`) in `BackupRecord.DestinationPath`.
3. Update `BackupManagement.razor` (`/admin/backups`) so the Backup History table displays the full `DestinationPath` without truncating it at 35 characters.

**Blocked by:** 01: Shared IRcloneRunner Seam & Omit Empty Archive Directories

**Status:** ready-for-agent

- [ ] Clicking "إنشاء / تحديث الأرشيف الآن" (Card 1) builds the zero-byte-hardlinked Arabic folder tree without empty folders and calls `IRcloneRunner.SyncDirectoryAsync` to sync `legal_archive` to the configured `LawyerRemote` (`gdrive:LegalERP_Test/Lawyer_Archive`).
- [ ] `BackupRecord.DestinationPath` records the `LawyerRemote` destination when synced to Google Drive, or marks the backup `Failed` with the error message if `rclone` fails.
- [ ] `BackupManagement.razor` renders the full `DestinationPath` in the Backup History table without cutting it off at 35 characters.
- [ ] Automated tests in `LegalERP.Tests/Backups/BackupArchiveServiceTests.cs` verify that `GenerateHumanReadableArchiveAsync` invokes `IRcloneRunner.SyncDirectoryAsync` with the staging folder and configured `LawyerRemote`, and omits empty directories on disk.
