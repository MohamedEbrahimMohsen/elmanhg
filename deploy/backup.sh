#!/usr/bin/env bash
# Usage: bash backup.sh — PostgreSQL dump + media tarball into $BACKUP_DIR; prints the dump path last (docs/deployment.md).
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck source=lib.sh disable=SC1091
source ./lib.sh

umask 077
mkdir -p "$BACKUP_DIR"
stamp=$(date -u +%Y%m%dT%H%M%SZ)
dump="$BACKUP_DIR/postgres-$stamp.dump"
trap 'rm -f "$dump"' ERR

# shellcheck disable=SC2016
compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom' > "$dump"
[ -s "$dump" ] || fail "empty dump"
compose exec -T postgres pg_restore --list < "$dump" > /dev/null

if [ -n "$(compose ps -q --status running api)" ]; then
  compose exec -T api tar -czf - -C /app/App_Data media > "$BACKUP_DIR/media-$stamp.tar.gz"
fi

find "$BACKUP_DIR" -maxdepth 1 -type f \( -name 'postgres-*.dump' -o -name 'media-*.tar.gz' \) -mtime +"${BACKUP_RETENTION_DAYS:-14}" -delete
echo "$dump"
