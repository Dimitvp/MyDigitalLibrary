#!/bin/sh
# Runs automatically by the postgres image ONLY on first boot of a fresh,
# empty data volume (docker-entrypoint-initdb.d convention) — never on a
# volume that already has data. That is exactly the "container got wiped"
# recovery path: if a backup file is present we restore it, otherwise we
# just let the normal empty-database startup continue.
set -e

# Backups are timestamped and never overwritten (mydigitallibrary_<UTC
# timestamp>.dump — see docker/db-backup/backup.sh); the filename sorts
# chronologically, so the lexicographically-last one is the newest.
BACKUP_FILE=$(ls -1 /backup/mydigitallibrary_*.dump 2>/dev/null | sort | tail -n 1 || true)

# Legacy fallback: a pre-timestamped-backups checkout may still only have
# the old fixed-name dump on disk.
if [ -z "$BACKUP_FILE" ] && [ -f /backup/mydigitallibrary.dump ]; then
    BACKUP_FILE=/backup/mydigitallibrary.dump
fi

if [ -n "$BACKUP_FILE" ] && [ -f "$BACKUP_FILE" ]; then
    echo "10-restore-if-exists: found $BACKUP_FILE, restoring into '$POSTGRES_DB'..."
    pg_restore --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --no-owner --clean --if-exists "$BACKUP_FILE"
    echo "10-restore-if-exists: restore complete."
else
    echo "10-restore-if-exists: no backup file in /backup, starting with an empty database."
fi
