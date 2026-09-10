#!/bin/bash
# ==============================================================================
# LegalERP Automated Daily Backup & Google Drive Sync
# Runs nightly at 2:00 AM via cron
# ==============================================================================
set -e

DATE=$(date +%Y-%m-%d)
BACKUP_DIR="/var/legalerp/backups"
STORAGE_DIR="/var/legalerp/storage/uploads"
ARCHIVE_EXPORT_DIR="/var/legalerp/storage/exports/legal_archive"
DB_NAME="legalerp_prod"
DB_USER="postgres"

mkdir -p "$BACKUP_DIR"
mkdir -p "$ARCHIVE_EXPORT_DIR"

echo "[$DATE] Starting LegalERP Daily Backup..."

# 1. PostgreSQL Database Snapshot (Compressed format)
DUMP_FILE="$BACKUP_DIR/db_$DATE.dump"
echo "[$DATE] Exporting PostgreSQL Database snapshot..."
pg_dump -U "$DB_USER" -d "$DB_NAME" -F c -f "$DUMP_FILE"

# 2. Trigger C# Human-Readable Archive Generation
# (Can be triggered via curl to localhost internal endpoint or CLI tool)
echo "[$DATE] Refreshing Human-Readable Archive..."
curl -s -X POST "http://localhost:5000/api/backups/export-archive" \
     -H "Content-Type: application/json" \
     -H "X-System-Cron-Key: ${CRON_SECRET:-default_internal_key}" || true

# 3. Incremental Sync to Google Drive (Lawyer's Business Archive)
if command -v rclone &> /dev/null; then
    echo "[$DATE] Syncing Human-Readable Legal Archive to Google Drive..."
    rclone sync "$ARCHIVE_EXPORT_DIR" "gdrive:LawFirmArchive" --fast-list --transfers 4 --checkers 8

    # 4. Off-site copy of Database Snapshot for Developer Disaster Recovery
    echo "[$DATE] Uploading Database snapshot to off-site backup storage..."
    rclone copy "$DUMP_FILE" "gdrive:SystemBackups/Database/"
fi

# 5. Local Snapshot Retention Policy (Keep last 14 days on VPS disk)
echo "[$DATE] Cleaning up local snapshots older than 14 days..."
find "$BACKUP_DIR" -name "db_*.dump" -mtime +14 -delete

echo "[$DATE] Backup completed successfully."
