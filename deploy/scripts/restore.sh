#!/bin/bash
# ==============================================================================
# LegalERP Disaster Recovery & Server Migration Restore Script
# Usage: ./restore.sh <path_to_db_dump> <path_to_uploads_archive>
# ==============================================================================
set -e

if [ "$#" -lt 1 ]; then
    echo "Usage: $0 <path_to_db_dump> [path_to_uploads_archive]"
    echo "Example: $0 /backups/db_2026-09-10.dump /backups/uploads.tar.gz"
    exit 1
fi

DUMP_FILE="$1"
UPLOADS_ARCHIVE="$2"
DB_NAME="legalerp_prod"
DB_USER="postgres"
STORAGE_DIR="/var/legalerp/storage/uploads"

echo "=== LegalERP Disaster Recovery Restore ==="
echo "Restoring database from: $DUMP_FILE"

# 1. Terminate existing connections to DB
sudo -u "$DB_USER" psql -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DB_NAME' AND pid <> pg_backend_pid();" || true

# 2. Restore Database schema and all data cleanly
sudo -u "$DB_USER" pg_restore --clean --if-exists -d "$DB_NAME" "$DUMP_FILE"

echo "Database restored successfully."

# 3. Restore uploaded physical documents if archive provided
if [ -n "$UPLOADS_ARCHIVE" ] && [ -f "$UPLOADS_ARCHIVE" ]; then
    echo "Restoring uploads directory to: $STORAGE_DIR"
    mkdir -p "$STORAGE_DIR"
    tar -xzf "$UPLOADS_ARCHIVE" -C "$STORAGE_DIR"
    chown -R www-data:www-data "$STORAGE_DIR"
    echo "Uploads restored successfully."
fi

echo "=== System restore completed successfully! ==="
