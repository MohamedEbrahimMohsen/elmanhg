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

set_env() {
  local file=$1 key=$2 value=$3
  if grep -q "^$key=" "$file"; then
    sed -i "s|^$key=.*|$key=$value|" "$file"
  else
    printf '%s=%s\n' "$key" "$value" >> "$file"
  fi
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

profile_enabled() {
  local profiles
  profiles=$(grep '^COMPOSE_PROFILES=' "$ENV_FILE" | cut -d= -f2-)
  [[ ",$profiles," == *",$1,"* ]]
}

check_observability_config() {
  profile_enabled observability || return 0
  local password alertmanager_file
  password=$(grep '^GRAFANA_ADMIN_PASSWORD=' "$ENV_FILE" | cut -d= -f2- || true)
  [ -n "$password" ] || fail "GRAFANA_ADMIN_PASSWORD is empty in $ENV_FILE; the observability profile needs it (docs/observability.md)"
  alertmanager_file=$(grep '^ALERTMANAGER_CONFIG_FILE=' "$ENV_FILE" | cut -d= -f2- || true)
  alertmanager_file=${alertmanager_file:-alertmanager.yml}
  [ -f "$alertmanager_file" ] || fail "$alertmanager_file not found; copy observability/alertmanager/alertmanager.example.yml there (docs/observability.md)"
}
