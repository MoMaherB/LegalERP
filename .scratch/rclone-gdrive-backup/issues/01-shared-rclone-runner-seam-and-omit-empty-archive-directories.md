# 01: Shared IRcloneRunner Seam & Omit Empty Archive Directories

**What to build:**
1. Update `BackupArchiveManifestBuilder.Build` so that `Manifest.Directories` only includes directories that actually contain at least one `BackupArchiveEntry` (eliminating empty `clients/`, `cases/قضية ...`, or `companies/...` folders when an entity has zero attached documents).
2. Create the shared `IRcloneRunner` interface (`SyncDirectoryAsync(string localDirectoryPath, string remoteDestination, CancellationToken ct)` and `CopyFileAsync(string localFilePath, string remoteDestinationDirectory, CancellationToken ct)`) in `LegalERP.Application.Backups` and its `RcloneProcessRunner` implementation in `LegalERP.Infrastructure.Services`, plus `BackupRemoteOptions` (`LawyerRemote`, `DeveloperRemote`) loaded from `.env` (`RCLONE_LAWYER_REMOTE`, `RCLONE_DEV_REMOTE`) or `IConfiguration` (`Backup:RcloneLawyerRemote`, `Backup:RcloneDevRemote`).
3. Register `IRcloneRunner` in `LegalERP.Api/Program.cs`.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] `BackupArchiveManifestBuilder.Build` returns only directories that contain at least one exported file (no empty folders for cases, clients, companies, or top-level categories with 0 documents).
- [x] `IRcloneRunner` and `RcloneProcessRunner` locate `rclone` across Windows (PATH and WinGet Links/Packages) and Linux (`/usr/bin/rclone`, `/usr/local/bin/rclone`) and execute `rclone sync` and `rclone copy` with clear error reporting on non-zero exit codes.
- [x] `BackupRemoteOptionsResolver` resolves `LawyerRemote` and `DeveloperRemote` from `IConfiguration` or `.env` in the solution root.
- [x] Unit tests in `LegalERP.Tests/Backups/` verify that empty folders are omitted from `BackupArchiveManifestBuilder.Build` and that `BackupRemoteOptionsResolver` parses `.env` and configuration values cleanly.
