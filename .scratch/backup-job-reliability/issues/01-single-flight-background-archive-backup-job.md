# 01: Run the Archive Backup as a Single App-Wide Background Job

**What to build:**
Clicking "إنشاء / تحديث الأرشيف الآن" (Card 1) starts the lawyer archive backup as a background job and returns immediately with the new backup's id, instead of holding the HTTP request / Blazor circuit open for the whole Google Drive upload (which currently times out mid-upload, shows the yellow "Reload" footer, and leaves Drive with only part of the archive).

Only one backup may run at a time **across the whole app** (all browsers, tabs and users). Any attempt to start a second backup while one is running is rejected with a clear "a backup is already running" message (API responds `409 Conflict`). The backups page detects a running backup on load, so the button stays disabled and the running state stays visible even after a page reload.

Backups left in `Running` by a crash or restart are reconciled on app startup to `Failed` with the reason "Interrupted", so the history never shows phantom running jobs.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Triggering the archive backup returns immediately with the backup id; the upload continues in the background and no longer depends on the page staying open.
- [x] The full archive reaches Google Drive: after one successful run, the Drive folder matches the local staging archive (all files and total size), with no duplicate files left over from earlier concurrent runs.
- [x] Only one backup runs at a time app-wide; a second trigger (same tab, another tab, or another user) gets `409 Conflict` and a user-facing message, and does not touch the staging folder or the remote.
- [x] On page load (and while a job is running) the page polls the active job status; the trigger button is disabled while any backup is running, and re-enables when it finishes.
- [x] On app startup, any `BackupRecord` still `Running` is marked `Failed` with an "Interrupted" error message.
- [x] Automated tests cover: single-flight rejection of a concurrent start, the job completing and updating its `BackupRecord`, and startup reconciliation of stale `Running` records.
