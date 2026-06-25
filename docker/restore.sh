#!/bin/bash
# R2WAI Database Restore Script
# Usage: ./restore.sh <backup_file>
# Example: ./restore.sh backups/r2wai_backup_20260618_120000.sql.gz

set -euo pipefail

if [ $# -lt 1 ]; then
    echo "Usage: $0 <backup_file>"
    echo "Available backups:"
    ls -lh "$(dirname "$0")/backups"/r2wai_backup_*.sql.gz 2>/dev/null || echo "  No backups found"
    exit 1
fi

BACKUP_FILE="$1"
CONTAINER_NAME="postgres"

if [ ! -f "$BACKUP_FILE" ]; then
    echo "Error: Backup file not found: $BACKUP_FILE"
    exit 1
fi

echo "WARNING: This will replace the current database with the backup."
echo "Backup file: $BACKUP_FILE"
echo ""
read -p "Are you sure? (yes/no): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
    echo "Restore cancelled."
    exit 0
fi

echo "[$(date)] Starting database restore from $BACKUP_FILE..."

gunzip -c "$BACKUP_FILE" | docker compose -f "$(dirname "$0")/docker-compose.production.yml" exec -T "$CONTAINER_NAME" \
    psql -U r2wai -d r2wai

echo "[$(date)] Restore complete."
echo "[$(date)] Verify by checking: docker compose -f docker/docker-compose.production.yml exec postgres psql -U r2wai -d r2wai -c 'SELECT count(*) FROM users;'"
