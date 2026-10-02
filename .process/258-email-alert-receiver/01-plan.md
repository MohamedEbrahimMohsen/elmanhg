# Plan — Email alert receiver (E18.S6, #258)

## Goal
The owner gets every production alert by email. Today Alertmanager reads a hand-copied host file (`deploy/alertmanager.yml`) whose only receiver has no integrations, so alerts go nowhere. After this ships, the `alertmanager` container renders its own config at start from `.env` keys and an optional secret file. When `ALERTMANAGER_EMAIL_TO`, `ALERTMANAGER_EMAIL_FROM` and an SMTP password are set, every alert rule is routed to an email receiver over SMTP (Resend SMTP by default). When they are not set, the null receiver stays the default and the stack starts as before, with no host file to copy. A documented `amtool` command sends a test alert, and the smoke test (CI `images` workflow, `deploy-smoke` job) validates both rendered configs and runs that same command.

## Scope
**In:** `deploy/` infra (Alertmanager entrypoint script, compose service, `.env.example`, `lib.sh` pre-deploy check, `smoke-test.sh` validation), `.gitignore`, and the docs `docs/observability.md`, `docs/deployment.md`, `docs/implementation-report.md`.
**Out:** webhook or chat receivers (dev decision on #213: "Emails only for now"), inhibition rules, the external uptime monitor and dead-man's switch (#213, still deferred), any `api/`, `web/` or `ai/` change, and any change to the alert rules or their promtool tests.
**Deferred:** a live delivery check against Resend SMTP. It needs a real Resend API key and a verified sender domain, and neither exists here. It is already tracked in #213. Placeholders only: no real values are committed anywhere.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | How can a config file that Alertmanager reads come "from host env"? Alertmanager cannot expand env vars. | The container's entrypoint becomes a committed POSIX `sh` script, `deploy/observability/alertmanager/entrypoint.sh`. It renders `/run/alertmanager/alertmanager.yml` from env, then `exec`s `/bin/alertmanager`. | `prom/alertmanager` is busybox-based (`sh`, `tr`, `cat` and `printf` are present; the healthcheck already uses busybox `wget`). No new image and no new tool. |
| 2 | What happens to the host-file mechanism (`ALERTMANAGER_CONFIG_FILE`, `alertmanager.example.yml`, the `lib.sh` file check)? | Remove it. Delete `alertmanager.example.yml`, the `ALERTMANAGER_CONFIG_FILE` key and the compose mount. Replace the `/deploy/alertmanager.yml` `.gitignore` line with `/deploy/secrets/`. | One mechanism only. No live host exists yet (implementation-report "Hosting: none live"), so no deployed file breaks. Email-only means the webhook example is no longer needed. |
| 3 | Where do the rendered config and the password file live? | On a `tmpfs` at `/run/alertmanager`, with Docker's default mode 1777. The script sets `umask 077`. | Nothing secret reaches the `alertmanager-data` volume or the image. The container runs as `nobody`, and 1777 lets it write. |
| 4 | Password source: env or secret file? | Both. A non-empty secret file wins. Its host path is `ALERTMANAGER_SMTP_PASSWORD_FILE`, bind-mounted read-only at `/run/secrets/alertmanager-smtp-password`. Otherwise `ALERTMANAGER_SMTP_PASSWORD` from `.env` is used. Either way the script copies the value with `\r` and `\n` stripped into `/run/alertmanager/smtp-password`, and the config references that file through `auth_password_file`. The password is never written into the YAML. | This is what the story asks for ("host env or secret files"). `auth_password_file` keeps the secret out of the config that `--check` prints. Stripping newlines avoids a trailing `\n` breaking SMTP auth. |
| 5 | Where does the secret-file mount point when the file option is not used? | Compose default `./observability/alertmanager/smtp-password.placeholder`: a committed, empty file. The script treats an empty file (`[ -s ]` false) as not set. | A bind mount of a missing host path makes Docker create a directory, and `/dev/null` is unreliable on Docker Desktop for Windows. An empty placeholder always exists. |
| 6 | When is email "configured"? | `TO` is non-empty, `FROM` is non-empty, and a password is resolved (decision 4). If none of `TO`, `FROM` or the password is set, the null config is rendered and one info line goes to stderr. If they are partly set or invalid, the null config is rendered and a warning naming the missing or invalid key goes to stderr (never the password). The container still starts. | The story says the null receiver stays the default and the stack still starts. Fail-fast for operators happens in `deploy.sh` (decision 7), not in the container. |
| 7 | How does a half-configured host get caught? | `lib.sh` `check_observability_config` fails the deploy when email is partly configured or when the secret file path is invalid. | `deploy.sh` already refuses the profile on a missing Grafana password. Same pattern, same place. |
| 8 | Value validation in the script | `TO` and `FROM` must contain `@`. `SMARTHOST` must contain `:`. None of `TO`, `FROM`, `SMARTHOST`, `USERNAME` or `ENVIRONMENT` may contain a single quote `'` or a newline. Values are written as YAML single-quoted scalars. A failed check counts as "invalid" (decision 6). | Single-quoted YAML needs only the `'` escape, so rejecting `'` makes injection impossible. Comma-separated `TO` lists stay valid (Alertmanager accepts `a@x, b@y`). |
| 9 | Default SMTP server | `ALERTMANAGER_SMTP_SMARTHOST` defaults to `smtp.resend.com:587` and `ALERTMANAGER_SMTP_USERNAME` defaults to `resend`. The password is the Resend API key. | This is the Resend SMTP contract, and the same Resend account already sends OTP and invitation email. |
| 10 | TLS on 587 vs 465 | When the smarthost ends in `:465`, render `require_tls: false`: Alertmanager uses implicit TLS on 465, and STARTTLS is not offered there. For any other port, render `require_tls: true` (STARTTLS required). | Both Resend ports work, and the password is never sent in clear text. |
| 11 | Routing and timing | Top route: `receiver: email`, `group_by: [alertname, severity]`, `group_wait: 30s`, `group_interval: 5m`, `repeat_interval: 4h`. One child route, `matchers: ['severity="critical"']`, with `receiver: email`, `group_wait: 10s` and `repeat_interval: 1h`. `send_resolved: true`. | All 16 current rules carry `severity` `critical` or `warning`, so the top route catches every rule, current and future. Critical alerts (fast burn, ServiceDown, OTP) reach the owner sooner and repeat hourly. Warnings repeat every 4 h, which matches the existing grouping in the example file. |
| 12 | Telling staging and prod emails apart | A `Subject` header: `'[<ALERTMANAGER_ENVIRONMENT>] {{ template "email.default.subject" . }}'`. Compose sets `ALERTMANAGER_ENVIRONMENT: ${COMPOSE_PROJECT_NAME:-elmanhg}`. | Both environments may email the same owner. `COMPOSE_PROJECT_NAME` is already `elmanhg-prod` or `elmanhg-staging`. |
| 13 | Null config shape | `route.receiver: default`, with the same `group_by`, `group_wait`, `group_interval` and `repeat_interval` as the top route. `receivers: [{name: default}]`. | This is exactly today's behaviour. Grafana's Alertmanager datasource is unchanged. |
| 14 | Test-alert procedure | `docker compose -f docker-compose.prod.yml --env-file .env exec alertmanager amtool alert add alertname=ElmanhgTestAlert severity=warning --annotation=summary="Test alert" --alertmanager.url=http://127.0.0.1:9093`. The firing email arrives after about 30 s. With no `--end`, the alert resolves after the default `resolve_timeout` of 5 m, and the resolved email follows within about 10 min. Failures show in `docker compose ... logs alertmanager` as `Notify for alerts failed`. | `amtool` ships in the image. No port is published, so a host-side `curl` cannot reach 9093. |
| 15 | CI validation | Reuse the existing smoke test, which runs in CI (`images.yml`, `deploy-smoke`, triggered by `deploy/**`). Replace its single `amtool check-config` with three `--check` runs (null, env password, file password), each with output assertions. After the stack is up, run the documented `amtool alert add` and assert with `amtool alert query`. | No new workflow. The CI that already exists is extended. |
| 16 | How `--check` works | `entrypoint.sh --check` renders the config, `cat`s it to stdout, then `exec /bin/amtool check-config <file>`. Every other argument list starts Alertmanager, and extra args are appended after the script's own flags. | Smoke can assert on the rendered YAML. The YAML holds only the password file path, never the password. |
| 17 | Secret file permissions on a host | Docs: `mkdir -p secrets && chmod 700 secrets`, then `printf '%s' '<key>' > secrets/alertmanager-smtp-password`, `chmod 400` and `sudo chown 65534:65534` on the file. Set `ALERTMANAGER_SMTP_PASSWORD_FILE=./secrets/alertmanager-smtp-password`. | The container reads the file as `nobody` (65534). `lib.sh`'s `-s` check needs only directory search permission, which the deploy user has. |
| 18 | Secret file path form | Must start with `./` or `/`. `lib.sh` fails otherwise. | Compose treats a bare name as a named volume. |

## Existing code touched
| File | Change |
|------|--------|
| `deploy/docker-compose.prod.yml` | `alertmanager` service (lines 158–172) changes as specified in "Compose service" below. Nothing else in the file changes. |
| `deploy/.env.example` | Replace the last two lines (the `ALERTMANAGER_CONFIG_FILE` comment and key) with the block in "`.env.example` block". |
| `deploy/lib.sh` | `check_observability_config`: delete the `alertmanager_file` local and its two lines plus the `[ -f ... ]` check. Append a call to the new function `check_alert_email_config`, and add that function (contract below). |
| `deploy/smoke-test.sh` | Lines 40–41 (the `ALERTMANAGER_CONFIG_FILE` `set_env` and the `cp`) are removed. Line 68 (the `amtool check-config` run) is replaced by the three checks in "Smoke-test changes". A test-alert block is added after `eventually "Alertmanager ready" alertmanager_ready`. |
| `deploy/observability/alertmanager/alertmanager.example.yml` | **Delete** (`git rm`). |
| `.gitignore` | Replace the line `/deploy/alertmanager.yml` with `/deploy/secrets/`. |
| `docs/observability.md` | Updated as described in "Docs changes". |
| `docs/deployment.md` | Updated as described in "Docs changes". |
| `docs/implementation-report.md` | The "Observability receivers" row (line 223) and the run-section sentence are updated as described in "Docs changes". |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `deploy/observability/alertmanager/entrypoint.sh` | POSIX `sh` (busybox), LF line endings (already enforced by `.gitattributes`: `*.sh` and `deploy/**`) | Full contract below. Invoked as `/bin/sh /etc/alertmanager/entrypoint.sh [args]`, so it needs no exec bit. |
| 2 | `deploy/observability/alertmanager/smtp-password.placeholder` | Empty file, 0 bytes | The default bind-mount source for the secret file. It must stay empty: add no comment line, because any content would count as a password. |

### `entrypoint.sh` contract
Header comment (2 lines): `# Renders the Alertmanager config from env at start, then runs alertmanager; "--check" prints it and runs amtool check-config.` and `# Email when ALERTMANAGER_EMAIL_TO, _FROM and a password are set, else the null receiver (docs/observability.md §9).`

Ordered steps:
1. `set -eu`; `umask 077`.
2. Constants: `out_dir=/run/alertmanager`, `config=$out_dir/alertmanager.yml`, `password_out=$out_dir/smtp-password`, `secret_file=/run/secrets/alertmanager-smtp-password`.
3. Read env with defaults: `to=${ALERTMANAGER_EMAIL_TO:-}`, `from=${ALERTMANAGER_EMAIL_FROM:-}`, `smarthost=${ALERTMANAGER_SMTP_SMARTHOST:-smtp.resend.com:587}`, `username=${ALERTMANAGER_SMTP_USERNAME:-resend}`, `environment=${ALERTMANAGER_ENVIRONMENT:-elmanhg}`. Then empty `smarthost` falls back to `smtp.resend.com:587`, and empty `username` to `resend`. The `:-` already does this; state it in a comment.
4. Password resolution:
   - `rm -f "$password_out"`.
   - `if [ -s "$secret_file" ]`, run `tr -d '\r\n' < "$secret_file" > "$password_out"`.
   - Otherwise, `if [ -n "${ALERTMANAGER_SMTP_PASSWORD:-}" ]`, run `printf '%s' "$ALERTMANAGER_SMTP_PASSWORD" | tr -d '\r\n' > "$password_out"`.
   - `has_password=1` iff `[ -s "$password_out" ]`, otherwise `0`.
5. Function `unsafe() { case $1 in *"'"*|*"$nl"*) return 0 ;; esac; return 1; }`, where `nl` is set once, in this order: `nl=$(printf '\n_')` then `nl=${nl%_}`. Inside it, check the carriage return the same way.
6. Decide `mode` (`email` or `null`) and `reason`:
   - If `to`, `from` and `has_password` are all empty or 0: `mode=null`, and echo `alertmanager-entrypoint: no alert email configured; alerts go to the null receiver` to stderr.
   - Else set `reason` to the first match in this order:
     - `to` empty: `ALERTMANAGER_EMAIL_TO is empty`
     - `from` empty: `ALERTMANAGER_EMAIL_FROM is empty`
     - `has_password=0`: `no SMTP password (ALERTMANAGER_SMTP_PASSWORD or ALERTMANAGER_SMTP_PASSWORD_FILE)`
     - `to` lacks `@`: `ALERTMANAGER_EMAIL_TO is not an email address`
     - `from` lacks `@`: `ALERTMANAGER_EMAIL_FROM is not an email address`
     - `smarthost` lacks `:`: `ALERTMANAGER_SMTP_SMARTHOST must be host:port`
     - any of `to`, `from`, `smarthost`, `username` or `environment` is `unsafe`: `<KEY> contains a quote or a line break`
   - If `reason` is non-empty: `mode=null`, echo `alertmanager-entrypoint: alert email not used: $reason; alerts go to the null receiver` to stderr, and `rm -f "$password_out"`. Else `mode=email`, echo `alertmanager-entrypoint: alerts are emailed through $smarthost` to stderr.
7. `require_tls`: `case $smarthost in *:465) require_tls=false ;; *) require_tls=true ;; esac`.
8. Write `$config` with `cat > "$config" <<EOF` (an unquoted heredoc, so `$vars` expand; there are no other `$` in the body). Bodies:

Null:
```yaml
# Rendered by entrypoint.sh at container start; edit .env, not this file.
route:
  receiver: default
  group_by: [alertname, severity]
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
receivers:
  - name: default
```
Email:
```yaml
# Rendered by entrypoint.sh at container start; edit .env, not this file.
route:
  receiver: email
  group_by: [alertname, severity]
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
  routes:
    - receiver: email
      matchers: ['severity="critical"']
      group_wait: 10s
      repeat_interval: 1h
receivers:
  - name: email
    email_configs:
      - to: '$to'
        from: '$from'
        smarthost: '$smarthost'
        auth_username: '$username'
        auth_password_file: $password_out
        require_tls: $require_tls
        send_resolved: true
        headers:
          Subject: '[$environment] {{ template "email.default.subject" . }}'
```
9. `if [ "${1:-}" = --check ]`, run `cat "$config"` then `exec /bin/amtool check-config "$config"`.
10. `exec /bin/alertmanager --config.file="$config" --storage.path=/alertmanager "$@"`.

### Compose service (`deploy/docker-compose.prod.yml`, `alertmanager`)
Replace the `command:` and `volumes:` lines, and add `entrypoint`, `environment` and `tmpfs`. Keep `profiles`, `image` (unchanged digest), `restart`, `healthcheck`, `mem_limit` and `logging` exactly as they are. Add the comment above `entrypoint`.
```yaml
    # entrypoint.sh renders the config from .env at start (docs/observability.md §9).
    entrypoint: ["/bin/sh", "/etc/alertmanager/entrypoint.sh"]
    environment:
      ALERTMANAGER_EMAIL_TO: ${ALERTMANAGER_EMAIL_TO:-}
      ALERTMANAGER_EMAIL_FROM: ${ALERTMANAGER_EMAIL_FROM:-}
      ALERTMANAGER_SMTP_SMARTHOST: ${ALERTMANAGER_SMTP_SMARTHOST:-smtp.resend.com:587}
      ALERTMANAGER_SMTP_USERNAME: ${ALERTMANAGER_SMTP_USERNAME:-resend}
      ALERTMANAGER_SMTP_PASSWORD: ${ALERTMANAGER_SMTP_PASSWORD:-}
      ALERTMANAGER_ENVIRONMENT: ${COMPOSE_PROJECT_NAME:-elmanhg}
    volumes:
      - ./observability/alertmanager:/etc/alertmanager:ro
      - ${ALERTMANAGER_SMTP_PASSWORD_FILE:-./observability/alertmanager/smtp-password.placeholder}:/run/secrets/alertmanager-smtp-password:ro
      - alertmanager-data:/alertmanager
    tmpfs:
      - /run/alertmanager
```

### `.env.example` block (replaces the last two lines)
```
# Alert email (docs/observability.md §9). Leave TO, FROM and the password empty to keep the null receiver (alerts show
# in Grafana and Alertmanager only). Resend SMTP by default: the username is "resend" and the password is a Resend API key.
# TO accepts a comma-separated list; FROM must be on a domain verified in Resend.
ALERTMANAGER_EMAIL_TO=
ALERTMANAGER_EMAIL_FROM=
ALERTMANAGER_SMTP_SMARTHOST=smtp.resend.com:587
ALERTMANAGER_SMTP_USERNAME=resend
# Set one of the two. The file wins; its path starts with ./ or / (e.g. ./secrets/alertmanager-smtp-password).
ALERTMANAGER_SMTP_PASSWORD=
ALERTMANAGER_SMTP_PASSWORD_FILE=
```

### `lib.sh` additions
- New helper `env_value() { grep "^$1=" "$ENV_FILE" | cut -d= -f2- || true; }`. Use it in the new function only. Leave the existing `password=` line in `check_observability_config` untouched.
- New function `check_alert_email_config`, called as the last line of `check_observability_config`, which only runs when the profile is enabled. Locals `to from password password_file`, read with `env_value ALERTMANAGER_EMAIL_TO`, `..._FROM`, `..._SMTP_PASSWORD` and `..._SMTP_PASSWORD_FILE`. Ordered steps:
  1. If all four are empty, `return 0`.
  2. If `password_file` is non-empty:
     - `[[ $password_file == ./* || $password_file == /* ]] || fail "ALERTMANAGER_SMTP_PASSWORD_FILE must start with ./ or / (docs/observability.md §9)"`
     - `[ -s "$password_file" ] || fail "ALERTMANAGER_SMTP_PASSWORD_FILE $password_file is missing or empty (docs/observability.md §9)"`
  3. `[ -n "$to" ] && [ -n "$from" ] && { [ -n "$password" ] || [ -n "$password_file" ]; } || fail "alert email is half configured in $ENV_FILE: set ALERTMANAGER_EMAIL_TO, ALERTMANAGER_EMAIL_FROM and ALERTMANAGER_SMTP_PASSWORD or _FILE, or clear them all (docs/observability.md §9)"`
- The comment above the function: `# Fails a deploy whose alert email is half configured; the container would silently fall back to the null receiver.`

### Smoke-test changes (`deploy/smoke-test.sh`)
1. Delete the two lines `set_env "$ENV_FILE" ALERTMANAGER_CONFIG_FILE .smoke/alertmanager.yml` and `cp observability/alertmanager/alertmanager.example.yml .smoke/alertmanager.yml`. Keep `check_observability_config`.
2. Replace `compose run --rm --no-deps --entrypoint amtool alertmanager check-config /etc/alertmanager/alertmanager.yml` with:
```bash
  # Alertmanager renders its config from env: check the null and both email variants (docs/observability.md §9).
  am_null=$(compose run --rm --no-deps alertmanager --check) || fail "null Alertmanager config is invalid"
  [[ $am_null == *'receiver: default'* && $am_null != *email_configs* ]] || fail "unconfigured Alertmanager does not use the null receiver"
  am_email=$(compose run --rm --no-deps -e ALERTMANAGER_EMAIL_TO=alerts@example.com -e ALERTMANAGER_EMAIL_FROM=alerts@example.com \
    -e ALERTMANAGER_SMTP_PASSWORD=smoke-placeholder-not-a-key alertmanager --check) || fail "email Alertmanager config is invalid"
  [[ $am_email == *'receiver: email'* && $am_email == *'smtp.resend.com:587'* && $am_email == *'severity="critical"'* \
    && $am_email == *'[elmanhg-smoke]'* && $am_email != *smoke-placeholder-not-a-key* ]] || fail "email Alertmanager config is wrong or leaks the password"
  printf 'smoke-file-placeholder\n' > .smoke/alertmanager-smtp-password
  am_file=$(ALERTMANAGER_SMTP_PASSWORD_FILE=./.smoke/alertmanager-smtp-password compose run --rm --no-deps \
    -e ALERTMANAGER_EMAIL_TO=alerts@example.com -e ALERTMANAGER_EMAIL_FROM=alerts@example.com alertmanager --check) \
    || fail "file-password Alertmanager config is invalid"
  [[ $am_file == *'receiver: email'* && $am_file != *smoke-file-placeholder* ]] || fail "Alertmanager ignores the password file or leaks it"
```
The `[elmanhg-smoke]` assertion assumes the default `SMOKE_PROJECT_NAME`. Write it as `*"[${SMOKE_PROJECT_NAME:-elmanhg-smoke}]"*` instead of a literal.

3. After `eventually "Alertmanager ready" alertmanager_ready`, add the documented test-alert procedure:
```bash
  # The test-alert procedure from docs/observability.md §9.
  compose exec -T alertmanager amtool alert add alertname=ElmanhgTestAlert severity=warning \
    --annotation=summary="Smoke test alert" --alertmanager.url=http://127.0.0.1:9093 || fail "amtool could not add the test alert"
  alertmanager_has_test_alert() {
    local body
    body=$(compose exec -T alertmanager amtool alert query alertname=ElmanhgTestAlert --alertmanager.url=http://127.0.0.1:9093) || return 1
    [[ $body == *ElmanhgTestAlert* ]]
  }
  eventually "test alert accepted by Alertmanager" alertmanager_has_test_alert
```

### Docs changes
**`docs/observability.md`**
- §1 diagram line: `Alertmanager ──▶ email / webhook (host config)` becomes `Alertmanager ──▶ email over SMTP (.env; Resend by default)`.
- §2 step 2 becomes: "Optional, for alert email: set `ALERTMANAGER_EMAIL_TO`, `ALERTMANAGER_EMAIL_FROM` and the Resend key (`ALERTMANAGER_SMTP_PASSWORD` or `ALERTMANAGER_SMTP_PASSWORD_FILE`) in `.env` (section 9). Without them alerts go to the null receiver."
- §2 step 3 becomes: "It refuses the profile when the Grafana password is missing or the alert email is half configured, and waits…" (the rest is unchanged).
- §9 "Alert delivery" is rewritten in full and holds:
  1. How it works: `entrypoint.sh` renders the config onto a tmpfs at start. There is no host config file, and the password is referenced through `auth_password_file`.
  2. Null default: when unconfigured, the stack still starts, and alerts show in Grafana and Alertmanager only. A half-configured `.env` fails `deploy.sh`. A container started outside `deploy.sh` logs a warning and uses the null receiver.
  3. A variable table (key, default, notes) for `ALERTMANAGER_EMAIL_TO`, `_FROM`, `_SMTP_SMARTHOST` (`smtp.resend.com:587`; `:465` = implicit TLS), `_SMTP_USERNAME` (`resend`), `_SMTP_PASSWORD`, `_SMTP_PASSWORD_FILE` (wins; `./` or `/`), and the compose-set `ALERTMANAGER_ENVIRONMENT` = `COMPOSE_PROJECT_NAME` (subject prefix).
  4. Secret-file setup commands (decision 17).
  5. Routing: every rule goes to email; group by `alertname` and `severity`; 30 s wait, 5 m group interval, repeat every 4 h; critical alerts wait 10 s and repeat every 1 h; resolved notices are sent.
  6. Apply changes: `docker compose -f docker-compose.prod.yml --env-file .env up -d alertmanager` (a restart does not re-read `.env`).
  7. "Send a test alert": the decision 14 command, the expected timing, and where failures show.
  8. Validation: `docker compose -f docker-compose.prod.yml --env-file .env run --rm --no-deps alertmanager --check` prints the rendered config and runs `amtool check-config`. The smoke test checks all three variants.
- §13 table: delete the `ALERTMANAGER_CONFIG_FILE` row. Add one `.env` row per new key, with the defaults above.
- §15: replace "`amtool check-config`" with "`amtool check-config` on the null, env-password and file-password renderings". Append "that Alertmanager is ready and accepts a test alert from `amtool`".

**`docs/deployment.md`**
- §1 services table, `alertmanager` row: "Routes alerts; reads the host file `alertmanager.yml`" becomes "Routes alerts; renders its config from `.env` (email or null receiver)".
- §3: "Three files…, four with the `observability` profile" becomes three files, plus an optional `secrets/alertmanager-smtp-password` (profile `observability`; Resend API key, owned by 65534, `chmod 400`). Replace the `alertmanager.yml` table row with that file. In "Never committed", replace `/deploy/alertmanager.yml` with `/deploy/secrets/`.
- §4 `.env` table: delete the `ALERTMANAGER_CONFIG_FILE` row. Add rows for the six `ALERTMANAGER_*` keys ("with the profile, optional"), linking to observability §9.
- §6 step 4: replace the `cp …alertmanager.example.yml…` clause with "optionally set the alert email keys (…§9)".
- §7 "What `deploy.sh` does" item 1: "…without `GRAFANA_ADMIN_PASSWORD` or with a half-configured alert email…".
- §12 paragraph: "`amtool check-config`" becomes "`amtool check-config` on the rendered Alertmanager configs", and append "and that Alertmanager accepts a test alert".
- §13 Observability row: "live alert receivers are deferred" becomes "alert email over Resend SMTP is built (#258) and waits for the key and addresses; an external uptime monitor and a vendor error tracker (Sentry) are deferred".

**`docs/implementation-report.md`** line 223, "Observability receivers" row:
- Fake column: "with the alert email keys empty, Alertmanager's null receiver: alerts show in Grafana and Alertmanager but go nowhere".
- Real column: "email over Resend SMTP rendered by `deploy/observability/alertmanager/entrypoint.sh`".
- Keys column: replace `ALERTMANAGER_CONFIG_FILE` with `ALERTMANAGER_EMAIL_TO`, `ALERTMANAGER_EMAIL_FROM`, `ALERTMANAGER_SMTP_PASSWORD` or `_FILE`.
- How-to column: "`docs/observability.md` §9: set the keys, `up -d alertmanager`, send the test alert."
- Keep the uptime-monitor sentence and `(#213)`.

## Error codes
None. This story makes no API change. Failure messages are the `fail` and stderr strings given verbatim above.

## Domain behaviour
None (no entity). The behavioural contract is the entrypoint's mode decision (decision 6), its ordered steps above, and the two rendered YAML bodies.

## API surface
None.

## Test plan
The infra checks run in `bash deploy/smoke-test.sh`, which runs in CI as `images.yml` → `deploy-smoke`. There are no unit tests: the repo has no shell test framework, and the testing conventions cover only `api/`, `web/` and `ai/`.

| # | Where | Check | Asserts |
|---|-------|-------|---------|
| 1 | `smoke-test.sh` pre-start | `am_null` | With no email keys, `--check` exits 0 (amtool valid). The output contains `receiver: default` and no `email_configs`. |
| 2 | `smoke-test.sh` pre-start | `am_email` | With env password, `--check` exits 0. The output contains `receiver: email`, `smtp.resend.com:587`, the `severity="critical"` child route and the `[<project>]` subject prefix, and does not contain the password value. |
| 3 | `smoke-test.sh` pre-start | `am_file` | With `ALERTMANAGER_SMTP_PASSWORD_FILE` and no env password, `--check` exits 0. The output contains `receiver: email` and does not contain the file's content. |
| 4 | `smoke-test.sh` stack up | existing `wait_healthy alertmanager` + `alertmanager_ready` | The container starts with the null config (no host file needed). |
| 5 | `smoke-test.sh` stack up | `amtool alert add` + `eventually "test alert accepted by Alertmanager"` | The documented test-alert command works, and the alert is listed by `amtool alert query`. |
| 6 | `smoke-test.sh` | existing `promtool check config` / `test rules` | Unchanged and still pass (the rules are not touched). |
| 7 | Implementer, manual, before handing back | `bash -n deploy/lib.sh deploy/smoke-test.sh`; `sh -n deploy/observability/alertmanager/entrypoint.sh`; `docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env.example --profile observability config -q` (with `IMAGE_TAG` set) | Syntax is valid and compose interpolates. Report the results in `02-implementation.md`. Run the full smoke test if Docker is available (`SMOKE_OBSERVABILITY=1`). On Windows it may need `SMOKE_OBSERVABILITY=0`, which skips these checks, so say so if that is what ran. |
| 8 | Implementer, manual | `check_alert_email_config` with a scratch env file: (a) all four keys empty: returns 0; (b) only TO set: fails "half configured"; (c) PASSWORD_FILE=`secrets/x`: fails "must start with ./ or /"; (d) PASSWORD_FILE=`./missing`: fails "missing or empty"; (e) TO, FROM and PASSWORD set: returns 0 | Each lib branch. Run with `ENV_FILE=<scratch> bash -c 'source deploy/lib.sh; COMPOSE_PROFILES… check_observability_config'`, with a `COMPOSE_PROFILES=observability` and a `GRAFANA_ADMIN_PASSWORD=x` line in the scratch file. Report the results. |

## Definition of done
- [ ] `deploy/observability/alertmanager/alertmanager.example.yml` is deleted. `ALERTMANAGER_CONFIG_FILE` appears nowhere in `deploy/`, `docs/` or `.gitignore` (grep is clean).
- [ ] `entrypoint.sh` exists and follows the ordered steps. It never echoes or renders the password; the YAML holds only `auth_password_file: /run/alertmanager/smtp-password`.
- [ ] Unconfigured: the null receiver is rendered and the container starts healthy.
- [ ] Partly configured or invalid: the null receiver is rendered, a stderr warning names the key, and the container starts.
- [ ] Configured: the top route goes to `email`, the critical child route has a 10 s wait and a 1 h repeat, the top route has 30 s / 5 m / 4 h, `group_by [alertname, severity]`, `send_resolved: true`, and the subject is prefixed with the project name.
- [ ] `require_tls` is false only for `:465`.
- [ ] The secret file wins over the env password. Trailing CR/LF are stripped. The empty placeholder counts as not set.
- [ ] The compose `alertmanager` service has `entrypoint`, `environment` (6 keys), 3 volumes and the `tmpfs`. The image digest, healthcheck, mem_limit and logging are unchanged.
- [ ] `.env.example` has the 6 keys with empty placeholders or Resend defaults, and no real value.
- [ ] `.gitignore` has `/deploy/secrets/` instead of `/deploy/alertmanager.yml`.
- [ ] `lib.sh` `check_alert_email_config` fails on half config, a bad path form and a missing or empty file, and passes on empty or full config.
- [ ] `smoke-test.sh` runs the 3 `--check` variants with the assertions and the test alert. The host-file lines are gone.
- [ ] `docs/observability.md` §1, §2, §9, §13 and §15, `docs/deployment.md` §1, §3, §4, §6, §7, §12 and §13, and `docs/implementation-report.md` line 223 all match the new behaviour. No doc still says "host `alertmanager.yml`" or "webhook receiver".
- [ ] §9 documents the test-alert command, its timing (about 30 s firing, resolved within about 10 min) and where failures appear.
- [ ] No file is created outside the two listed. No real credential is committed.
