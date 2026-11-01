# 03: Cancel a Running Backup

**What to build:**
While a backup is running, a **"إلغاء النسخ"** (Cancel) button is shown next to the progress. Clicking it stops the running rclone process cleanly, records the backup as `Cancelled`, and immediately unlocks the trigger buttons so a new backup can be started. Files already uploaded stay on Google Drive; the next run uploads only what's missing.

**Blocked by:** 01: Run the Archive Backup as a Single App-Wide Background Job

**Status:** resolved

- [x] A cancel endpoint (SuperAdmin only) signals the active job; it responds sensibly when no job is running.
- [x] Cancelling kills the rclone process (and its child processes), marks the `BackupRecord` `Cancelled` with completion time, and releases the single-flight lock.
- [x] The UI shows the Cancel button only while a job is running, asks for confirmation, and after cancelling shows a toast and re-enables the trigger buttons.
- [x] The history table displays the `Cancelled` status with its own badge (bilingual).
- [x] Automated tests verify cancellation stops the job, sets `Cancelled`, and allows a new job to start afterwards.
