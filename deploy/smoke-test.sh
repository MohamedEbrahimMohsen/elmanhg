#!/usr/bin/env bash
# Usage: bash deploy/smoke-test.sh — builds the images and runs the whole production stack locally and in CI:
# migrate, serve, backup and a restore drill (docs/deployment.md, Run the production stack locally).
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck source=lib.sh disable=SC1091
source ./lib.sh

export ENV_FILE=.smoke/.env BACKUP_DIR=.smoke/backups
port=${SMOKE_HTTP_PORT:-8088}
base="http://localhost:$port"

rm -rf .smoke && mkdir -p .smoke
cp .env.example .smoke/.env
cp api.env.example .smoke/api.env
cp ai.env.example .smoke/ai.env

set_env() {
  local file=$1 key=$2 value=$3
  if grep -q "^$key=" "$file"; then
    sed -i "s|^$key=.*|$key=$value|" "$file"
  else
    printf '%s=%s\n' "$key" "$value" >> "$file"
  fi
}

set_env "$ENV_FILE" COMPOSE_PROJECT_NAME "${SMOKE_PROJECT_NAME:-elmanhg-smoke}"
set_env "$ENV_FILE" COMPOSE_PROFILES ai
set_env "$ENV_FILE" IMAGE_REGISTRY local
set_env "$ENV_FILE" IMAGE_TAG smoke
set_env "$ENV_FILE" SITE_ADDRESS :80
set_env "$ENV_FILE" HTTP_PORT "$port"
set_env "$ENV_FILE" HTTPS_PORT "${SMOKE_HTTPS_PORT:-8443}"
set_env "$ENV_FILE" DOCKER_SUBNET "${SMOKE_DOCKER_SUBNET:-172.30.250.0/24}"
set_env "$ENV_FILE" API_ENV_FILE .smoke/api.env
set_env "$ENV_FILE" AI_ENV_FILE .smoke/ai.env
set_env "$ENV_FILE" POSTGRES_PASSWORD smokepostgrespasswordnotasecret

cleanup() {
  local status=$?
  if [ "$status" -ne 0 ]; then
    compose ps -a || true
    compose logs --no-color --tail 200 || true
  fi
  compose down -v --remove-orphans || true
  if [ "${SMOKE_KEEP:-}" != 1 ]; then
    rm -rf .smoke
  fi
}
trap cleanup EXIT

if [ "${SMOKE_SKIP_BUILD:-}" != 1 ]; then
  docker build -f ../api/Dockerfile -t local/elmanhg-api:smoke ..
  docker build -t local/elmanhg-web:smoke ../web
  docker build -t local/elmanhg-ai:smoke ../ai
fi

compose up -d
wait_healthy postgres
wait_healthy api 300
wait_healthy web
wait_healthy ai
[ "$(migrate_exit_code)" = 0 ] || fail "migrate exited with $(migrate_exit_code)"

# Whole responses are read into variables: no curl -o /dev/null (Git Bash hands the path unconverted to the native
# curl.exe) and no grep -q (an early exit can break the pipe under pipefail).
shell=$(curl -fsS "$base/") || fail "SPA shell not served at /"
[[ $shell == *'id="root"'* ]] || fail "SPA shell at / has no root element"
login=$(curl -fsS "$base/login") || fail "SPA fallback not served at /login"
[[ $login == *'id="root"'* ]] || fail "SPA fallback at /login has no root element"
content_type=$(curl -fsS -w '\n%{content_type}' "$base/api/questions/servable-count" | tail -1) || fail "/api is not proxied to the API"
[[ $content_type == application/json* ]] || fail "/api answered with '$content_type', not JSON"
shell_headers=$(curl -fsSI "$base/")
grep -i '^cache-control: no-cache' <<< "$shell_headers" > /dev/null || fail "SPA shell is not Cache-Control: no-cache"
asset=$(grep -o '/assets/[^"]*\.js' <<< "$shell" | head -1)
[ -n "$asset" ] || fail "no /assets/*.js referenced by the SPA shell"
asset_headers=$(curl -fsSI "$base$asset")
grep -i 'cache-control:.*immutable' <<< "$asset_headers" > /dev/null || fail "$asset is not cached as immutable"

compose run --rm migrate || fail "a second migrate run failed"

dump=$(bash ./backup.sh | tail -1)
[ -s "$dump" ] || fail "backup produced no dump"
bash ./restore.sh "$dump" elmanhg_restore_drill

echo "Smoke test passed"
