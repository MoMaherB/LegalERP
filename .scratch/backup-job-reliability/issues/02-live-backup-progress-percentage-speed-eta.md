# 02: Show Live Backup Progress (Percentage, Transferred/Total, Speed, Time Remaining)

**What to build:**
While a backup is running, the backups page shows a live progress widget on the running card: a progress bar with percentage, transferred vs. total size (e.g. `5.6 MB / 12.4 MB`), current upload speed, and estimated time remaining, so the user can decide whether to wait or cancel and retry later. Figures come from rclone's own periodic transfer statistics, refresh every 1–2 seconds, and are still shown correctly after a page reload because they're read from the app-wide active job.

**Blocked by:** 01: Run the Archive Backup as a Single App-Wide Background Job

**Status:** resolved

- [x] The rclone runner reports live transfer statistics (percentage, transferred bytes, total bytes, speed, ETA) while `sync` / `copy` runs, parsed from rclone's periodic stats output.
- [x] The active job exposes its latest progress snapshot through the active-job status endpoint.
- [x] The running card shows a progress bar, percentage, `transferred / total`, speed, and time remaining (bilingual Arabic/English), updating every 1–2 seconds and surviving page reload.
- [x] When the job ends, the widget disappears and the history/summary refresh.
- [x] Automated tests verify parsing of real rclone stats lines (including the "0 B / 0 B, -, ETA -" idle case) into a progress snapshot.
