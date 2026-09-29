#!/usr/bin/env bash
# Usage: bash restore.sh <dump-file> <target-database>
# A target other than the live database is a drill: restore into a scratch database and compare migration counts.
# The live database is overwritten only with CONFIRM_LIVE_RESTORE=yes (docs/deployment.md).
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck source=lib.sh disable=SC1091
source ./lib.sh

usage="usage: restore.sh <dump-file> <target-database>"
dump=${1:?$usage}
target=${2:?$usage}
[ -f "$dump" ] || fail "$dump not found; $usage"
[[ $target =~ ^[a-z_][a-z0-9_]{0,62}$ ]] || fail "invalid target database '$target'; $usage"

user=$(compose exec -T postgres printenv POSTGRES_USER | tr -d '\r')
live=$(compose exec -T postgres printenv POSTGRES_DB | tr -d '\r')

if [ "$target" = "$live" ]; then
  [ "${CONFIRM_LIVE_RESTORE:-}" = yes ] || fail "live restore overwrites $live; rerun with CONFIRM_LIVE_RESTORE=yes"
  compose stop api
  compose exec -T postgres pg_restore -U "$user" -d "$target" --clean --if-exists --no-owner --single-transaction --exit-on-error < "$dump"
  compose start api
  wait_healthy api 300
  echo "Restored $dump into $target"
  exit 0
fi

count_migrations() {
  compose exec -T postgres psql -U "$user" -d "$1" -tAc 'SELECT count(*) FROM "__EFMigrationsHistory"' | tr -d '\r'
}

compose exec -T postgres dropdb -U "$user" --if-exists "$target"
compose exec -T postgres createdb -U "$user" "$target"
compose exec -T postgres pg_restore -U "$user" -d "$target" --no-owner --exit-on-error < "$dump"

expected=$(count_migrations "$live")
count=$(count_migrations "$target")
[ "$count" = "$expected" ] || fail "restore drill: $target has $count migrations, $live has $expected"

if [ "${KEEP_RESTORE_DB:-}" != 1 ]; then
  compose exec -T postgres dropdb -U "$user" "$target"
fi

echo "Restore drill passed: $count migrations in $target"
