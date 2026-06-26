#!/bin/bash
# R2WAI Database Restore Script
# Usage: ./restore.sh <backup_file>
# Example: ./restore.sh backups/r2wai_backup_20260618_120000.sql.gz

set -euo pipefail

if [ $# -lt 1 ]; then
    echo "Usage: $0 <backup_file>"
    echo "Available backups:"
    ls -lh "$(dirname "$0")/backups"/r2wai_backup_* 2>/dev/null || echo "  No backups found"
    exit 1
fi

BACKUP_FILE="$1"
CONTAINER_NAME="postgres"
ENCRYPT_KEY="${BACKUP_ENCRYPTION_KEY:-}"

if [ ! -f "$BACKUP_FILE" ]; then
    echo "Error: Backup file not found: $BACKUP_FILE"
    exit 1
fi

# Verify backup file integrity
echo "[$(date)] Verifying backup file integrity..."
FILESIZE=$(stat -f%z "$BACKUP_FILE" 2>/dev/null || stat --printf="%s" "$BACKUP_FILE" 2>/dev/null || echo "0")
if [ "$FILESIZE" -lt 100 ]; then
    echo "Error: Backup file appears corrupted (size: $FILESIZE bytes)"
    exit 1
fi

IS_ENCRYPTED=false
if [[ "$BACKUP_FILE" == *.enc ]]; then
    IS_ENCRYPTED=true
    if [ -z "$ENCRYPT_KEY" ]; then
        echo "Error: Backup is encrypted but BACKUP_ENCRYPTION_KEY is not set."
        exit 1
    fi
fi

echo "WARNING: This will replace the current database with the backup."
echo "Backup file: $BACKUP_FILE ($(numfmt --to=iec "$FILESIZE" 2>/dev/null || echo "${FILESIZE} bytes"))"
echo "Encrypted: $IS_ENCRYPTED"
echo ""
read -p "Are you sure? (yes/no): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
    echo "Restore cancelled."
    exit 0
fi

echo "[$(date)] Starting database restore from $BACKUP_FILE..."

if [ "$IS_ENCRYPTED" = true ]; then
    openssl enc -aes-256-cbc -d -salt -pbkdf2 -pass "pass:${ENCRYPT_KEY}" -in "$BACKUP_FILE" \
        | gunzip \
        | docker compose -f "$(dirname "$0")/docker-compose.production.yml" exec -T "$CONTAINER_NAME" \
            psql -U r2wai -d r2wai
else
    gunzip -c "$BACKUP_FILE" | docker compose -f "$(dirname "$0")/docker-compose.production.yml" exec -T "$CONTAINER_NAME" \
        psql -U r2wai -d r2wai
fi

echo "[$(date)] Restore complete."
echo "[$(date)] Verify: docker compose -f docker/docker-compose.production.yml exec postgres psql -U r2wai -d r2wai -c 'SELECT count(*) FROM users;'"
