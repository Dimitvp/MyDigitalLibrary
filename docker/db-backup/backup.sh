#!/bin/sh
# Sidecar that keeps /backup fresh with timestamped, never-overwritten
# pg_dump snapshots:
#   - shortly after any INSERT/UPDATE/DELETE on any content table (library
#     items, editions, works, authors, genres, reading sessions, wishlist,
#     shelves, notes, quotes, reviews, ratings, loans, reading goals,
#     series, bookstore listings, import jobs — see the trigger list
#     below), debounced so a burst of changes collapses into one backup
#     instead of dozens;
#   - unconditionally at least once a week, even if nothing changed.
#
# Each backup gets its own UTC-timestamped filename
# (mydigitallibrary_YYYYmmddTHHMMSSZ.dump) — nothing is overwritten, and
# pruning (below) only ever deletes, never replaces, a snapshot.
#
# Pruning, run after every successful backup: file COUNT is checked
# before file AGE, in that order, specifically so a long-neglected
# database can never be pruned down to nothing in one pass:
#   1. If there are KEEP_MIN or fewer backups total, delete nothing at
#      all — regardless of how old they are.
#   2. Otherwise, the newest KEEP_MIN files are always kept no matter
#      their age; only among the excess (older) files does age matter,
#      and only those older than MAX_AGE_SECONDS get deleted.
# So there are always at least KEEP_MIN backups on disk, and pruning
# only ever removes files beyond that floor.
#
# A change detected while still inside the debounce window is never
# dropped: it's remembered ("pending") and backed up as soon as the
# window clears, instead of silently being skipped like the old
# works/editions-only version of this script did (that gap is why this
# version exists — see .claude/history for the incident that prompted it).
#
# Connection details (PGHOST/PGPORT/PGUSER/PGPASSWORD, POSTGRES_DB) come
# from the environment set in docker-compose.yml.
set -eu

BACKUP_DIR=/backup

POLL_SECONDS=30          # how often we check for a change notification
MIN_GAP_SECONDS=300      # never start a new backup more often than this
WEEKLY_SECONDS=604800    # guaranteed backup ceiling: at most 7 days stale

KEEP_MIN=10                    # never prune below this many backups, regardless of age
MAX_AGE_SECONDS=$((180*24*3600)) # ~6 months; only considered once above KEEP_MIN

# Every table whose contents represent real user data — a change to any of
# these is worth a fresh snapshot. Deliberately excludes the ASP.NET
# Identity tables (login activity isn't "your library changed") and
# __EFMigrationsHistory.
TABLES="works editions authors work_authors genres library_items reading_sessions reading_progress wishlist_entries shelves shelf_items notes quotes reviews work_ratings loans reading_goals series bookstores bookstore_listings bookstore_listing_price_history import_jobs tags"

mkdir -p "$BACKUP_DIR"

log() {
    echo "$(date -u +"%Y-%m-%dT%H:%M:%SZ") $*"
}

latest_backup_file() {
    ls -1 "$BACKUP_DIR"/mydigitallibrary_*.dump 2>/dev/null | sort | tail -n 1
}

last_backup_epoch() {
    f=$(latest_backup_file)
    if [ -n "$f" ]; then
        stat -c %Y "$f"
    else
        echo 0
    fi
}

run_backup() {
    ts=$(date -u +"%Y%m%dT%H%M%SZ")
    target="$BACKUP_DIR/mydigitallibrary_$ts.dump"
    tmp="$BACKUP_DIR/.tmp_$ts.dump"
    log "backup: starting -> $target"
    if pg_dump --format=custom --file="$tmp" "$POSTGRES_DB"; then
        mv "$tmp" "$target"
        log "backup: done -> $target"
        prune_backups
    else
        log "backup: FAILED, discarding partial file"
        rm -f "$tmp"
    fi
}

# See the header comment: count first, then age, and never below KEEP_MIN.
prune_backups() {
    total=$(ls -1 "$BACKUP_DIR"/mydigitallibrary_*.dump 2>/dev/null | wc -l)
    if [ "$total" -le "$KEEP_MIN" ]; then
        return
    fi

    excess=$((total - KEEP_MIN))
    cutoff=$(( $(date +%s) - MAX_AGE_SECONDS ))
    n=0

    # No early break: iterating every file under set -e keeps this safe (a
    # `[ cond ] && break` short-circuit here would trip errexit on every
    # non-matching iteration) and the file counts involved are small enough
    # that the extra iterations cost nothing.
    ls -1 "$BACKUP_DIR"/mydigitallibrary_*.dump | sort | while IFS= read -r f; do
        n=$((n + 1))
        if [ "$n" -le "$excess" ]; then
            file_epoch=$(stat -c %Y "$f")
            if [ "$file_epoch" -lt "$cutoff" ]; then
                rm -f "$f"
                log "prune: removed $f (older than 6 months, $total backups on disk)"
            fi
        fi
    done
}

until pg_isready -q; do
    log "waiting for database..."
    sleep 2
done

# (Re)install the change triggers on every start. Idempotent. On a brand
# new database the api container may not have applied EF Core migrations
# yet, so retry until the tables actually exist instead of crashing.
until psql -v ON_ERROR_STOP=1 -q <<SQL
CREATE OR REPLACE FUNCTION notify_book_changed() RETURNS trigger AS \$\$
BEGIN
    PERFORM pg_notify('book_changed', TG_TABLE_NAME);
    RETURN NULL;
END;
\$\$ LANGUAGE plpgsql;

-- Drop the old per-table trigger names from the previous (works/editions
-- only) version of this script, so re-running against an existing
-- database doesn't leave orphaned duplicates.
DROP TRIGGER IF EXISTS works_notify_change ON works;
DROP TRIGGER IF EXISTS editions_notify_change ON editions;

DO \$\$
DECLARE
    t text;
BEGIN
    FOREACH t IN ARRAY ARRAY[$(echo "$TABLES" | sed "s/\([a-z_]*\)/'\1'/g" | tr ' ' ',')]
    LOOP
        EXECUTE format('DROP TRIGGER IF EXISTS notify_change ON %I', t);
        EXECUTE format('CREATE TRIGGER notify_change AFTER INSERT OR UPDATE OR DELETE ON %I FOR EACH STATEMENT EXECUTE FUNCTION notify_book_changed()', t);
    END LOOP;
END \$\$;
SQL
do
    log "content tables not ready yet (migrations pending?), retrying trigger setup in 5s"
    sleep 5
done
log "change triggers installed on: $TABLES"
log "watching for changes (poll ${POLL_SECONDS}s, min gap ${MIN_GAP_SECONDS}s, weekly fallback)"

pending=0

while true; do
    age=$(( $(date +%s) - $(last_backup_epoch) ))
    if [ "$age" -ge "$WEEKLY_SECONDS" ]; then
        log "weekly backup due (last one ${age}s ago)"
        run_backup
        pending=0
    fi

    notified=$(psql -X -q -c "LISTEN book_changed;" -c "SELECT pg_sleep($POLL_SECONDS);" 2>&1 | grep -c "Asynchronous notification" || true)

    if [ "$notified" -gt 0 ]; then
        pending=1
    fi

    if [ "$pending" -eq 1 ]; then
        age=$(( $(date +%s) - $(last_backup_epoch) ))
        if [ "$age" -ge "$MIN_GAP_SECONDS" ]; then
            log "change detected, backing up"
            run_backup
            pending=0
        else
            log "change detected, still within debounce window (${age}s/${MIN_GAP_SECONDS}s) - will back up once it clears"
        fi
    fi
done
