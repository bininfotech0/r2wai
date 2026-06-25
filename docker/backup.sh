#!/bin/bash
# R2WAI Database Backup Script
# Usage: ./backup.sh [retention_days]
# Runs pg_dump inside the postgres container and stores in ./backups/

set -euo pipefail

RETENTION_DAYS=${1:-30}
BACKUP_DIR="$(dirname "$0")/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="r2wai_backup_${TIMESTAMP}.sql.gz"
CONTAINER_NAME="postgres"

mkdir -p "$BACKUP_DIR"

echo "[$(date)] Starting R2WAI database backup..."

docker compose -f "$(dirname "$0")/docker-compose.production.yml" exec -T "$CONTAINER_NAME" \
    pg_dump -U r2wai -d r2wai --clean --if-exists | gzip > "$BACKUP_DIR/$BACKUP_FILE"

FILESIZE=$(stat -f%z "$BACKUP_DIR/$BACKUP_FILE" 2>/dev/null || stat --printf="%s" "$BACKUP_DIR/$BACKUP_FILE" 2>/dev/null || echo "unknown")
echo "[$(date)] Backup complete: $BACKUP_FILE ($FILESIZE bytes)"

# Cleanup old backups
DELETED=$(find "$BACKUP_DIR" -name "r2wai_backup_*.sql.gz" -mtime +$RETENTION_DAYS -delete -print | wc -l)
if [ "$DELETED" -gt 0 ]; then
    echo "[$(date)] Cleaned up $DELETED backup(s) older than $RETENTION_DAYS days"
fi

echo "[$(date)] Backup finished. Active backups:"
ls -lh "$BACKUP_DIR"/r2wai_backup_*.sql.gz 2>/dev/null | tail -5
