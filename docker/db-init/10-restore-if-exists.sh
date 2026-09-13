#!/bin/sh
# Runs automatically by the postgres image ONLY on first boot of a fresh,
# empty data volume (docker-entrypoint-initdb.d convention) — never on a
# volume that already has data. That is exactly the "container got wiped"
# recovery path: if a backup file is present we restore it, otherwise we
# just let the normal empty-database startup continue.
set -e

BACKUP_FILE="/backup/mydigitallibrary.dump"

if [ -f "$BACKUP_FILE" ]; then
    echo "10-restore-if-exists: found $BACKUP_FILE, restoring into '$POSTGRES_DB'..."
    pg_restore --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --no-owner --clean --if-exists "$BACKUP_FILE"
    echo "10-restore-if-exists: restore complete."
else
    echo "10-restore-if-exists: no backup file at $BACKUP_FILE, starting with an empty database."
fi
