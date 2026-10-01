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

# The API and the ai service refuse the example "change-me" secrets outside Development; throwaway stacks get random ones.
replace_example_secrets() {
  local api_env=$1 ai_env=$2 service_token
  service_token=$(openssl rand -hex 32)
  set_env "$api_env" CoreJwt__Key "$(openssl rand -hex 48)"
  set_env "$api_env" CoreOtp__Secret "$(openssl rand -hex 32)"
  set_env "$api_env" TrainingData__StudentIdHashKey "$(openssl rand -hex 32)"
  set_env "$api_env" AdminSeed__Password "$(openssl rand -hex 16)A1"
  set_env "$api_env" AiService__ServiceToken "$service_token"
  set_env "$ai_env" ELMANHG_AI_SERVICE_TOKEN "$service_token"
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
  local password
  password=$(grep '^GRAFANA_ADMIN_PASSWORD=' "$ENV_FILE" | cut -d= -f2- || true)
  [ -n "$password" ] || fail "GRAFANA_ADMIN_PASSWORD is empty in $ENV_FILE; the observability profile needs it (docs/observability.md)"
  check_alert_email_config
}

env_value() {
  grep "^$1=" "$ENV_FILE" | cut -d= -f2- || true
}

# Fails a deploy whose alert email is half configured; the container would silently fall back to the null receiver.
check_alert_email_config() {
  local to from password password_file
  to=$(env_value ALERTMANAGER_EMAIL_TO)
  from=$(env_value ALERTMANAGER_EMAIL_FROM)
  password=$(env_value ALERTMANAGER_SMTP_PASSWORD)
  password_file=$(env_value ALERTMANAGER_SMTP_PASSWORD_FILE)
  [ -z "$to$from$password$password_file" ] && return 0
  if [ -n "$password_file" ]; then
    [[ $password_file == ./* || $password_file == /* ]] || fail "ALERTMANAGER_SMTP_PASSWORD_FILE must start with ./ or / (docs/observability.md §9)"
    [ -s "$password_file" ] || fail "ALERTMANAGER_SMTP_PASSWORD_FILE $password_file is missing or empty (docs/observability.md §9)"
  fi
  [ -n "$to" ] && [ -n "$from" ] && { [ -n "$password" ] || [ -n "$password_file" ]; } \
    || fail "alert email is half configured in $ENV_FILE: set ALERTMANAGER_EMAIL_TO, ALERTMANAGER_EMAIL_FROM and ALERTMANAGER_SMTP_PASSWORD or _FILE, or clear them all (docs/observability.md §9)"
}
