#!/usr/bin/env bash
# Usage: bash deploy/load-test.sh — builds the images, runs the production stack with the load-test seed, then the
# pinned k6 API load and the throttled lesson-page browser run against Caddy, and captures EXPLAIN plans
# (docs/performance.md §5). LOAD_PROFILE=full runs 50 students for 6.5 minutes; the default smoke profile runs 45 s.
# Results (k6 summaries, explain.txt) land in deploy/.loadtest-results, which the script never deletes.
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck source=lib.sh disable=SC1091
source ./lib.sh

export LOAD_PROFILE=${LOAD_PROFILE:-smoke}
export LOAD_KEY=${LOAD_KEY:-loadtest}
# Ephemeral stack, torn down at exit: the password only guards throwaway students.
export LOAD_STUDENT_PASSWORD=loadtest-password-1
export ENV_FILE=.loadtest/.env BACKUP_DIR=.loadtest/backups
image_tag=${LOAD_IMAGE_TAG:-loadtest}
student_count=${LOAD_STUDENT_COUNT:-60}

rm -rf .loadtest && mkdir -p .loadtest .loadtest-results && chmod 777 .loadtest-results
cp .env.example .loadtest/.env
cp api.env.example .loadtest/api.env
cp ai.env.example .loadtest/ai.env
replace_example_secrets .loadtest/api.env .loadtest/ai.env

set_env "$ENV_FILE" COMPOSE_PROJECT_NAME "${LOAD_PROJECT_NAME:-elmanhg-load}"
set_env "$ENV_FILE" COMPOSE_PROFILES ai
set_env "$ENV_FILE" ASPNETCORE_ENVIRONMENT Staging
set_env "$ENV_FILE" IMAGE_REGISTRY local
set_env "$ENV_FILE" IMAGE_TAG "$image_tag"
set_env "$ENV_FILE" SITE_ADDRESS :80
set_env "$ENV_FILE" HTTP_PORT "${LOAD_HTTP_PORT:-8098}"
set_env "$ENV_FILE" HTTPS_PORT "${LOAD_HTTPS_PORT:-8453}"
set_env "$ENV_FILE" DOCKER_SUBNET "${LOAD_DOCKER_SUBNET:-172.30.251.0/24}"
set_env "$ENV_FILE" API_ENV_FILE .loadtest/api.env
set_env "$ENV_FILE" AI_ENV_FILE .loadtest/ai.env
set_env "$ENV_FILE" POSTGRES_PASSWORD loadpostgrespasswordnotasecret
# One client IP signs in every student; the browser receives the refresh cookie over plain http; the avatar quota and rate limits are not under test.
set_env .loadtest/api.env Auth__CredentialPermitLimit 10000
set_env .loadtest/api.env RateLimiting__AuthRefreshPermitLimit 100000
set_env .loadtest/api.env RateLimiting__PublicReadPermitLimit 100000
set_env .loadtest/api.env RateLimiting__AvatarMessagePermitLimit 100000
set_env .loadtest/api.env RateLimiting__StudentConcurrentRequestLimit 100
set_env .loadtest/api.env Auth__RefreshTokenCookieSecure false
set_env .loadtest/api.env Subscriptions__BaseDailyAvatarMessages 10000
set_env .loadtest/api.env LoadTestSeed__Key "$LOAD_KEY"
set_env .loadtest/api.env LoadTestSeed__StudentCount "$student_count"
set_env .loadtest/api.env LoadTestSeed__StudentPassword "$LOAD_STUDENT_PASSWORD"
set_env .loadtest/ai.env ELMANHG_AI_LLM_PROVIDER fake

load_compose() {
  docker compose -f docker-compose.prod.yml -f loadtest/docker-compose.loadtest.yml --env-file "$ENV_FILE" "$@"
}

cleanup() {
  local status=$?
  if [ "$status" -ne 0 ]; then
    compose ps -a || true
    compose logs --no-color --tail 200 || true
  fi
  load_compose --profile '*' down -v --remove-orphans || true
  if [ "${LOAD_KEEP:-}" != 1 ]; then
    rm -rf .loadtest
  fi
}
trap cleanup EXIT

if [ "${LOAD_SKIP_BUILD:-}" != 1 ]; then
  docker build -f ../api/Dockerfile -t "local/elmanhg-api:$image_tag" ..
  docker build -t "local/elmanhg-web:$image_tag" ../web
  docker build -t "local/elmanhg-ai:$image_tag" ../ai
fi

compose up -d
wait_healthy postgres
wait_healthy api 300
wait_healthy web
wait_healthy ai
[ "$(migrate_exit_code)" = 0 ] || fail "migrate exited with $(migrate_exit_code)"

compose run --rm migrate --SeedLoadTestAndExit=true || fail "load-test seed failed"

api_status=0
load_compose run --rm -e LOAD_PROFILE -e LOAD_KEY -e LOAD_STUDENT_PASSWORD k6 run --summary-export /results/api-summary.json /scripts/api-load.js || api_status=$?
browser_status=0
load_compose run --rm -e LOAD_PROFILE -e LOAD_KEY -e LOAD_STUDENT_PASSWORD k6 run /scripts/lesson-page.js || browser_status=$?

# $0 carries the key into the container shell, so psql sees it as the :key variable.
# shellcheck disable=SC2016
compose exec -T postgres sh -c 'psql -v ON_ERROR_STOP=1 -v key="$0" -U "$POSTGRES_USER" -d "$POSTGRES_DB"' "$LOAD_KEY" < loadtest/explain.sql > .loadtest-results/explain.txt || fail "explain failed"

{ [ "$api_status" = 0 ] && [ "$browser_status" = 0 ]; } || fail "budgets exceeded (api $api_status, browser $browser_status); see deploy/.loadtest-results"
echo "Load test ($LOAD_PROFILE) passed"
