#!/bin/sh
# Sidecar that keeps /backup/mydigitallibrary.dump reasonably fresh:
#   - immediately after any INSERT/UPDATE/DELETE on works/editions (a book
#     being added, edited or removed), debounced so a bulk CSV import
#     collapses into one backup instead of hundreds;
#   - unconditionally at least once a week, even if nothing changed.
# Connection details (PGHOST/PGPORT/PGUSER/PGPASSWORD, POSTGRES_DB) come
# from the environment set in docker-compose.yml.
set -eu

BACKUP_DIR=/backup
BACKUP_FILE="$BACKUP_DIR/mydigitallibrary.dump"
TMP_FILE="$BACKUP_FILE.tmp"

POLL_SECONDS=30          # how often we check for a change notification
MIN_GAP_SECONDS=300      # never back up more often than this on change (debounce)
WEEKLY_SECONDS=604800    # guaranteed backup ceiling: at most 7 days stale

mkdir -p "$BACKUP_DIR"

log() {
    echo "$(date -u +"%Y-%m-%dT%H:%M:%SZ") $*"
}

run_backup() {
    log "backup: starting..."
    if pg_dump --format=custom --file="$TMP_FILE" "$POSTGRES_DB"; then
        mv "$TMP_FILE" "$BACKUP_FILE"
        log "backup: done -> $BACKUP_FILE"
    else
        log "backup: FAILED, keeping previous $BACKUP_FILE"
        rm -f "$TMP_FILE"
    fi
}

last_backup_epoch() {
    if [ -f "$BACKUP_FILE" ]; then
        stat -c %Y "$BACKUP_FILE"
    else
        echo 0
    fi
}

until pg_isready -q; do
    log "waiting for database..."
    sleep 2
done

# (Re)install the change trigger on every start. Idempotent. On a brand new
# database the api container may not have applied EF Core migrations yet,
# so retry until the tables actually exist instead of crashing.
until psql -v ON_ERROR_STOP=1 -q <<'SQL'
CREATE OR REPLACE FUNCTION notify_book_changed() RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('book_changed', TG_TABLE_NAME);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS works_notify_change ON works;
CREATE TRIGGER works_notify_change
    AFTER INSERT OR UPDATE OR DELETE ON works
    FOR EACH STATEMENT EXECUTE FUNCTION notify_book_changed();

DROP TRIGGER IF EXISTS editions_notify_change ON editions;
CREATE TRIGGER editions_notify_change
    AFTER INSERT OR UPDATE OR DELETE ON editions
    FOR EACH STATEMENT EXECUTE FUNCTION notify_book_changed();
SQL
do
    log "works/editions tables not ready yet (migrations pending?), retrying trigger setup in 5s"
    sleep 5
done
log "book-change triggers installed, watching for changes (poll ${POLL_SECONDS}s, weekly fallback)"

while true; do
    age=$(( $(date +%s) - $(last_backup_epoch) ))
    if [ "$age" -ge "$WEEKLY_SECONDS" ]; then
        log "weekly backup due (last one ${age}s ago)"
        run_backup
    fi

    notified=$(psql -X -q -c "LISTEN book_changed;" -c "SELECT pg_sleep($POLL_SECONDS);" 2>&1 | grep -c "Asynchronous notification" || true)

    if [ "$notified" -gt 0 ]; then
        age=$(( $(date +%s) - $(last_backup_epoch) ))
        if [ "$age" -ge "$MIN_GAP_SECONDS" ]; then
            log "book change detected, backing up"
            run_backup
        else
            log "book change detected, but backed up ${age}s ago - skipping (debounced)"
        fi
    fi
done
