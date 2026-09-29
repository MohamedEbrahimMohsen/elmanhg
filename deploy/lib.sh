#!/usr/bin/env bash
# Shared helpers for the deploy scripts; sourced, never run. Paths are relative to deploy/ (docs/deployment.md).

# Git Bash on Windows would otherwise rewrite container paths such as /app/App_Data in docker arguments.
export MSYS_NO_PATHCONV=1

ENV_FILE=${ENV_FILE:-.env}
BACKUP_DIR=${BACKUP_DIR:-backups}
export ENV_FILE BACKUP_DIR

compose() {
  docker compose -f docker-compose.prod.yml --env-file "$ENV_FILE" "$@"
}

fail() {
  echo "error: $*" >&2
  exit 1
}

wait_healthy() {
  local service=$1 timeout=${2:-180} waited=0 id status
  id=$(compose ps -q "$service")
  [ -n "$id" ] || fail "$service is not running"
  until status=$(docker inspect -f '{{.State.Health.Status}}' "$id") && [ "$status" = healthy ]; do
    [ "$waited" -ge "$timeout" ] && fail "$service not healthy after ${timeout}s (status: $status)"
    sleep 3
    waited=$((waited + 3))
  done
  echo "$service healthy"
}

migrate_exit_code() {
  docker inspect -f '{{.State.ExitCode}}' "$(compose ps -aq migrate)"
}
