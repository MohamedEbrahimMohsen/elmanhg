#!/usr/bin/env bash
# Usage: bash deploy/smoke-test.sh — builds the images and runs the whole production stack locally and in CI:
# migrate, serve, backup and a restore drill, plus the observability stack end to end (docs/observability.md).
# SMOKE_OBSERVABILITY=0 skips the observability part (Docker Desktop may not expose /var/lib/docker/containers).
set -euo pipefail
cd "$(dirname "$0")"
# shellcheck source=lib.sh disable=SC1091
source ./lib.sh

export ENV_FILE=.smoke/.env BACKUP_DIR=.smoke/backups
port=${SMOKE_HTTP_PORT:-8088}
base="http://localhost:$port"
observability=${SMOKE_OBSERVABILITY:-1}
image_tag=${SMOKE_IMAGE_TAG:-smoke}
grafana_port=${SMOKE_GRAFANA_PORT:-3300}
grafana_password=smokegrafanapasswordnotasecret

rm -rf .smoke && mkdir -p .smoke
cp .env.example .smoke/.env
cp api.env.example .smoke/api.env
cp ai.env.example .smoke/ai.env
replace_example_secrets .smoke/api.env .smoke/ai.env

set_env "$ENV_FILE" COMPOSE_PROJECT_NAME "${SMOKE_PROJECT_NAME:-elmanhg-smoke}"
set_env "$ENV_FILE" COMPOSE_PROFILES ai
set_env "$ENV_FILE" IMAGE_REGISTRY local
set_env "$ENV_FILE" IMAGE_TAG "$image_tag"
set_env "$ENV_FILE" SITE_ADDRESS :80
set_env "$ENV_FILE" HTTP_PORT "$port"
set_env "$ENV_FILE" HTTPS_PORT "${SMOKE_HTTPS_PORT:-8443}"
set_env "$ENV_FILE" DOCKER_SUBNET "${SMOKE_DOCKER_SUBNET:-172.30.250.0/24}"
set_env "$ENV_FILE" API_ENV_FILE .smoke/api.env
set_env "$ENV_FILE" AI_ENV_FILE .smoke/ai.env
set_env "$ENV_FILE" POSTGRES_PASSWORD smokepostgrespasswordnotasecret
if [ "$observability" = 1 ]; then
  set_env "$ENV_FILE" COMPOSE_PROFILES ai,observability
  set_env "$ENV_FILE" OTLP_ENDPOINT http://otel-collector:4317
  set_env "$ENV_FILE" GRAFANA_ADMIN_PASSWORD "$grafana_password"
  set_env "$ENV_FILE" GRAFANA_PORT "$grafana_port"
  check_observability_config
fi

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
  docker build -f ../api/Dockerfile -t "local/elmanhg-api:$image_tag" ..
  docker build -t "local/elmanhg-web:$image_tag" ../web
  docker build -t "local/elmanhg-ai:$image_tag" ../ai
fi

compose run --rm --no-deps --entrypoint caddy web validate --config /etc/caddy/Caddyfile --adapter caddyfile
if [ "$observability" = 1 ]; then
  compose run --rm --no-deps --entrypoint promtool prometheus check config /etc/prometheus/prometheus.yml
  compose run --rm --no-deps --entrypoint promtool prometheus test rules /etc/prometheus/tests/elmanhg.rules.test.yml
  # Alertmanager renders its config from env: check the null and both email variants (docs/observability.md §9).
  am_null=$(compose run --rm --no-deps alertmanager --check) || fail "null Alertmanager config is invalid"
  [[ $am_null == *'receiver: default'* && $am_null != *email_configs* ]] || fail "unconfigured Alertmanager does not use the null receiver"
  am_email=$(compose run --rm --no-deps -e ALERTMANAGER_EMAIL_TO=alerts@example.com -e ALERTMANAGER_EMAIL_FROM=alerts@example.com \
    -e ALERTMANAGER_SMTP_PASSWORD=smoke-placeholder-not-a-key alertmanager --check) || fail "email Alertmanager config is invalid"
  [[ $am_email == *'receiver: email'* && $am_email == *'smtp.resend.com:587'* && $am_email == *'severity="critical"'* \
    && $am_email == *"[${SMOKE_PROJECT_NAME:-elmanhg-smoke}]"* && $am_email != *smoke-placeholder-not-a-key* ]] \
    || fail "email Alertmanager config is wrong or leaks the password"
  printf 'smoke-file-placeholder\n' > .smoke/alertmanager-smtp-password
  am_file=$(ALERTMANAGER_SMTP_PASSWORD_FILE=./.smoke/alertmanager-smtp-password compose run --rm --no-deps \
    -e ALERTMANAGER_EMAIL_TO=alerts@example.com -e ALERTMANAGER_EMAIL_FROM=alerts@example.com alertmanager --check) \
    || fail "file-password Alertmanager config is invalid"
  [[ $am_file == *'receiver: email'* && $am_file != *smoke-file-placeholder* ]] || fail "Alertmanager ignores the password file or leaks it"
  compose run --rm --no-deps otel-collector validate --config=/etc/otelcol-contrib/config.yaml
fi

compose up -d
wait_healthy postgres
wait_healthy api 300
wait_healthy web
wait_healthy ai
if [ "$observability" = 1 ]; then
  for service in prometheus alertmanager blackbox grafana; do
    wait_healthy "$service"
  done
fi
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
asset_br=$(curl -fsSI -H 'Accept-Encoding: br' "$base$asset")
grep -i '^content-encoding: br' <<< "$asset_br" > /dev/null || fail "$asset is not served brotli-precompressed"
for expected in "content-security-policy: default-src 'self'" "frame-ancestors 'none'" "x-frame-options: DENY"   "x-content-type-options: nosniff" "referrer-policy: strict-origin-when-cross-origin"   "strict-transport-security: max-age=31536000" "permissions-policy:" "cross-origin-opener-policy: same-origin"; do
  grep -iF "$expected" <<< "$shell_headers" > /dev/null || fail "SPA shell is missing the header '$expected'"
done
if grep -i '^server:' <<< "$shell_headers" > /dev/null; then fail "SPA shell exposes a Server header"; fi

# Signals the observability assertions below look for; the public checks run with or without the profile.
health=$(curl -fsS "$base/api/health") || fail "/api/health is not served"
[ "$health" = Healthy ] || fail "/api/health answered '$health', not Healthy"
api_headers=$(curl -fsS -D - "$base/api/questions/servable-count") || fail "servable-count failed"
trace_id=$(grep -i '^x-trace-id:' <<< "$api_headers" | head -1 | cut -d: -f2 | tr -d ' \r')
[[ $trace_id =~ ^[0-9a-f]{32}$ ]] || fail "no W3C trace id in X-Trace-Id (got '$trace_id')"
grep -iF "content-security-policy: default-src 'none'" <<< "$api_headers" > /dev/null || fail "/api response has no API CSP"
grep -iF 'cache-control: public, max-age=60' <<< "$api_headers" > /dev/null || fail "/api overwrote the API's own Cache-Control"
if grep -i '^server:' <<< "$api_headers" > /dev/null; then fail "/api response exposes a Server header"; fi
refresh=$(curl -sS -D - -X POST "$base/api/auth/refresh")
grep -iF 'cache-control: no-store' <<< "$refresh" > /dev/null || fail "/api/auth/refresh is not Cache-Control: no-store"
curl -fsS -H 'CF-Connecting-IP: 198.51.100.77' "$base/api/plans" > /dev/null || fail "/api/plans failed"
api_log=""
for _ in $(seq 1 10); do
  api_log=$(compose logs --no-color api)
  [[ $api_log == *'/api/plans'* ]] && break
  sleep 1
done
[[ $api_log == *'/api/plans'* ]] || fail "the API did not log the /api/plans request"
[[ $api_log != *'198.51.100.0/24'* ]] || fail "the API trusted a client-sent CF-Connecting-IP"
client_error=$(curl -sS -w '\n%{http_code}' -X POST -H 'Content-Type: application/json' \
  -d '{"message":"smoke client error","source":"Window","path":"/smoke"}' "$base/api/client-errors")
[ "$(tail -1 <<< "$client_error")" = 200 ] || fail "POST /api/client-errors answered '$client_error'"
redaction_probe=$(curl -fsS "$base/smoke-redaction/mona@example.com") || fail "redaction probe request failed"
[[ $redaction_probe == *'id="root"'* ]] || fail "redaction probe did not reach the SPA fallback"
ai_token=$(grep '^ELMANHG_AI_SERVICE_TOKEN=' .smoke/ai.env | cut -d= -f2-)
embeddings=$(compose exec -T api curl -fsS -X POST -H "Authorization: Bearer $ai_token" -H 'Content-Type: application/json' \
  -d '{"inputType":"query","texts":["smoke"]}' http://ai:8000/v1/embeddings) || fail "AI embeddings call failed"
[[ $embeddings == *'"embeddings":[['* ]] || fail "AI embeddings answered without vectors"

if [ "$observability" = 1 ]; then
  eventually() {
    local description=$1 waited=0
    shift
    until "$@"; do
      [ "$waited" -ge 180 ] && fail "observability: $description not seen after 180s"
      sleep 5
      waited=$((waited + 5))
    done
    echo "observability: $description"
  }
  in_prometheus() {
    compose exec -T prometheus wget -qO- "$@"
  }
  has_series() {
    local body
    body=$(in_prometheus "http://localhost:9090/api/v1/query?query=$1") || return 1
    [[ $body == *'"result":[{'* ]]
  }
  prometheus_contains() {
    local body
    body=$(in_prometheus "$1") || return 1
    [[ $body == *"$2"* ]]
  }
  tempo_has_trace() {
    local body
    body=$(in_prometheus --header 'Accept: application/json' "http://tempo:3200/api/traces/$trace_id") || return 1
    [[ $body == *elmanhg-api* ]]
  }
  loki_query() {
    in_prometheus "http://loki:3100/loki/api/v1/query_range?limit=1000&since=1h&query=$1"
  }
  loki_has_api_trace() {
    local body
    body=$(loki_query "%7Bservice_name%3D%22elmanhg-api%22%7D%20%7C%20trace_id%3D%22$trace_id%22") || return 1
    [[ $body == *"$trace_id"* ]]
  }
  loki_redacts_web() {
    local body
    body=$(loki_query '%7Bservice_name%3D%22elmanhg-web%22%7D%20%7C%3D%20%22smoke-redaction%22') || return 1
    [[ $body == *'[redacted-email]'* && $body != *'mona@example.com'* ]]
  }
  alertmanager_ready() {
    local body
    body=$(in_prometheus http://alertmanager:9093/-/ready) || return 1
    [ -n "$body" ]
  }
  grafana_has_dashboards() {
    local uid board
    for uid in elmanhg-service-health elmanhg-background-jobs elmanhg-business; do
      board=$(curl -fsS -u "admin:$grafana_password" "http://localhost:$grafana_port/api/dashboards/uid/$uid") || return 1
      [[ $board == *"\"uid\":\"$uid\""* ]] || return 1
    done
  }

  eventually "API request metrics" has_series elmanhg_requests_total
  eventually "API HTTP server metrics" has_series 'http_server_request_duration_seconds_count%7Bjob%3D%22elmanhg-api%22%7D'
  eventually "AI model metrics" has_series 'gen_ai_client_operation_duration_seconds_count%7Bjob%3D%22elmanhg-ai%22%7D'
  eventually "client error metric" has_series elmanhg_client_errors_total
  eventually "background job metrics" has_series elmanhg_job_interval_seconds
  eventually "edge probe up" has_series 'probe_success%7Btarget_name%3D%22edge%22%7D%3D%3D1'
  eventually "alert rules loaded" prometheus_contains http://localhost:9090/api/v1/rules ApiErrorBudgetFastBurn
  eventually "API trace in Tempo" tempo_has_trace
  eventually "API logs linked to the trace" loki_has_api_trace
  eventually "Caddy access log redacted" loki_redacts_web
  eventually "Grafana dashboards provisioned" grafana_has_dashboards
  eventually "Alertmanager ready" alertmanager_ready
  # The test-alert procedure from docs/observability.md §9.
  compose exec -T alertmanager amtool alert add alertname=ElmanhgTestAlert severity=warning \
    --annotation=summary="Smoke test alert" --alertmanager.url=http://127.0.0.1:9093 || fail "amtool could not add the test alert"
  alertmanager_has_test_alert() {
    local body
    body=$(compose exec -T alertmanager amtool alert query alertname=ElmanhgTestAlert --alertmanager.url=http://127.0.0.1:9093) || return 1
    [[ $body == *ElmanhgTestAlert* ]]
  }
  eventually "test alert accepted by Alertmanager" alertmanager_has_test_alert
fi

compose run --rm migrate || fail "a second migrate run failed"

dump=$(bash ./backup.sh | tail -1)
[ -s "$dump" ] || fail "backup produced no dump"
bash ./restore.sh "$dump" elmanhg_restore_drill

echo "Smoke test passed"
