#!/usr/bin/env bash
# Usage: bash deploy.sh <image-tag> — back up, pull, migrate and start one environment (docs/deployment.md).
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck source=lib.sh disable=SC1091
source ./lib.sh

tag=${1:?usage: deploy.sh <image-tag>}
[[ $tag =~ ^(sha-[0-9a-f]{7,40}|main)$ ]] || fail "image tag must be sha-<7-40 hex> or main, got '$tag'"
check_observability_config
grep -q '^IMAGE_TAG=' "$ENV_FILE" || fail "$ENV_FILE has no IMAGE_TAG= line"
previous=$(grep '^IMAGE_TAG=' "$ENV_FILE" | cut -d= -f2-)

on_exit() {
  local status=$?
  [ "$status" -eq 0 ] && return
  echo "Deploy of $tag failed. Roll back: bash deploy.sh $previous; if a migration ran, restore the pre-deploy dump first (docs/deployment.md)." >&2
  compose ps -a || true
  compose logs --no-color --tail 100 migrate api || true
}
# EXIT, not ERR: fail() and wait_healthy() leave through exit, which ERR never sees.
trap on_exit EXIT

sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$tag/" "$ENV_FILE"
compose pull

if [ -n "$(compose ps -q --status running postgres)" ]; then
  bash ./backup.sh
fi

# Migrate before up: a failed migration stops here, while the old api and web still serve.
compose run --rm migrate
compose up -d --remove-orphans
wait_healthy api 300
[ "$(migrate_exit_code)" = 0 ] || fail "migrate exited with $(migrate_exit_code)"
wait_healthy web
if [ -n "$(compose ps -q ai)" ]; then
  wait_healthy ai
fi
# Loki and Tempo images ship no probe tool, so they have no health check to wait for.
for service in prometheus alertmanager blackbox grafana; do
  if [ -n "$(compose ps -q "$service")" ]; then
    wait_healthy "$service"
  fi
done

docker image prune -f
echo "Deployed $tag (previous: $previous)"
