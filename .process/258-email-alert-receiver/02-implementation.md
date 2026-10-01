# Implementation — Email alert receiver (E18.S6, #258)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `deploy/observability/alertmanager/entrypoint.sh` | 120 | POSIX sh entrypoint: resolves the SMTP password (file wins, CR/LF stripped, copied to the tmpfs), decides email or null mode with the plan's reason order, renders `/run/alertmanager/alertmanager.yml`, `--check` prints it and execs `amtool check-config`, otherwise execs `alertmanager` |
| `deploy/observability/alertmanager/smtp-password.placeholder` | 0 (0 bytes) | Default bind-mount source for the secret file; empty = not set |

## Files modified
| Path | Change |
|---|---|
| `deploy/docker-compose.prod.yml` | `alertmanager`: `command` and host-file volume replaced by the comment, `entrypoint`, 6-key `environment`, 3 volumes and `tmpfs: /run/alertmanager`. Image digest, healthcheck, mem_limit, logging unchanged |
| `deploy/.env.example` | Last two lines (`ALERTMANAGER_CONFIG_FILE`) replaced by the plan's block (6 keys, empty or Resend defaults) |
| `deploy/lib.sh` | `check_observability_config` loses the host-file check and calls the new `check_alert_email_config`; new `env_value` helper |
| `deploy/smoke-test.sh` | Host-file `set_env`/`cp` removed; the single `amtool check-config` replaced by the three `--check` variants with assertions; test-alert add + `eventually` query after "Alertmanager ready" |
| `deploy/observability/alertmanager/alertmanager.example.yml` | Deleted (`git rm`) |
| `.gitignore` | `/deploy/alertmanager.yml` → `/deploy/secrets/` |
| `docs/observability.md` | §1 diagram line, §2 steps 2–3, §9 rewritten (mechanism, null default, key table, secret-file setup, routing, apply, test alert, validation), §13 rows, §15 sentence |
| `docs/deployment.md` | §1 alertmanager row, §3 intro/table row/never-committed list, §4 six new rows, §6 step 4, §7 item 1, §12 paragraph, §13 Observability row |
| `docs/implementation-report.md` | §4 "Observability receivers" row (line 223) |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `docs/implementation-report.md`: update "the run-section sentence" | §5 "Running the system locally" (line 256) says only "observability" and never mentions the host file or receivers; nothing there is stale | Left §5 unchanged; only the line-223 row was updated |
| §13 table: "one `.env` row per new key" | `ALERTMANAGER_ENVIRONMENT` is set by compose, not `.env`, and §9 documents it | Added the six `.env` rows plus one `compose` row for `ALERTMANAGER_ENVIRONMENT` (additive, consistent with the existing `compose` rows in that table) |

No image assumption turned out false (see Build & test), so the entrypoint and compose service follow the plan verbatim.

## Build & test
No `api/`, `web/` or `ai/` change, so `dotnet`, `npm` and `pytest` were not run.

Image assumptions, verified in `prom/alertmanager:v0.34.1@sha256:e9733baf…`:
- `which sh tr cat printf rm` → all in `/bin`; default user `uid=65534(nobody)`; `/bin/alertmanager` and `/bin/amtool` present.
- `--tmpfs /run/alertmanager` (Docker default mode) shows as `drwxrwxrwt root root`; as `nobody` with `umask 077` the script writes `alertmanager.yml` and `smtp-password` as `-rw------- nobody nobody`.
- `--check`, null (no keys, placeholder mounted): stderr info line, null YAML, `Checking '/run/alertmanager/alertmanager.yml'  SUCCESS`.
- `--check`, env password (`TO="a@example.com, b@example.com"`, `ENVIRONMENT=elmanhg-staging`): email YAML with critical child route, `require_tls: true`, `auth_password_file: /run/alertmanager/smtp-password`, subject `[elmanhg-staging]`; `SUCCESS`; password file is exactly the value length (no newline); value absent from YAML.
- `--check`, file password (file content `placeholder-file-value\r\n`, env password also set, smarthost `:465`): `require_tls: false`, `SUCCESS`; password file is 22 bytes with no CR/LF; the file value won over the env value.
- Partial/invalid: only TO → "ALERTMANAGER_EMAIL_FROM is empty"; TO+FROM → "no SMTP password (…)"; TO without `@` → "…TO is not an email address"; smarthost `smtp` → "must be host:port"; FROM with `'` → "ALERTMANAGER_EMAIL_FROM contains a quote or a line break"; ENVIRONMENT with `\n` and with `\r` → "ALERTMANAGER_ENVIRONMENT contains a quote or a line break"; PW only → "ALERTMANAGER_EMAIL_TO is empty". Each rendered the null config, `check-config` SUCCESS, and `smtp-password` was removed.
- Real start (no `--check`), null and email mode, default user + tmpfs: `/-/ready` → `OK`; `ps` shows PID 1 `nobody /bin/alertmanager --config.file=/run/alertmanager/alertmanager.yml --storage.path=/alertmanager`. The documented `amtool alert add alertname=ElmanhgTestAlert severity=warning --annotation=summary="Test alert" …` returned 0 and `amtool alert query alertname=ElmanhgTestAlert` listed it `active`.

Syntax and compose:
- `bash -n deploy/lib.sh deploy/smoke-test.sh` → ok.
- `sh -n entrypoint.sh` inside the alertmanager image (busybox sh) → ok (no output, exit 0).
- `IMAGE_TAG=x API_ENV_FILE=api.env.example AI_ENV_FILE=ai.env.example docker compose -f docker-compose.prod.yml --env-file .env.example --profile observability config alertmanager` → renders entrypoint, 6 env keys (`ALERTMANAGER_ENVIRONMENT: elmanhg-prod`), 3 volumes (placeholder as default secret source), tmpfs. (Without the two `*_ENV_FILE` overrides compose fails only because `api.env` does not exist in the repo; unrelated to this change.)

`check_alert_email_config` (scratch env with `COMPOSE_PROFILES=ai,observability` and `GRAFANA_ADMIN_PASSWORD=x`):
- (a) all four empty → exit 0
- (b) only TO → exit 1 "alert email is half configured in … (docs/observability.md §9)"
- (c) `PASSWORD_FILE=secrets/x` → exit 1 "must start with ./ or /"
- (d) `PASSWORD_FILE=./missing` → exit 1 "./missing is missing or empty"
- (e) TO, FROM, PASSWORD → exit 0
- extra: PASSWORD_FILE = the empty placeholder → exit 1 "missing or empty"; PASSWORD_FILE = a non-empty file with TO+FROM → exit 0; keys absent entirely → exit 0.

Full smoke test (`SMOKE_OBSERVABILITY=1 bash deploy/smoke-test.sh`, Docker Desktop + Git Bash on Windows, images built): exit 0, `Smoke test passed`. The log shows the three `--check` runs (`no alert email configured…`, then `alerts are emailed through smtp.resend.com:587` twice) with all assertions passing, every `observability:` check including `observability: Alertmanager ready` and `observability: test alert accepted by Alertmanager`, and the promtool checks (`SUCCESS: 20 rules found`) unchanged. `grep -c` for both placeholder password values in the full log → 0.

## Notes for review
- `amtool alert add … --annotation=summary="Test alert"` logs a WARN that the input was parsed with the classic matcher parser (the shell strips the inner quotes). The alert is still added (exit 0). I kept the plan's command verbatim and noted the warning in observability §9; `--annotation='summary="Test alert"'` would silence it if the reviewer prefers.
- The secret-file setup in §9 uses `printf '%s' '<Resend API key>' > …` per decision 17; it puts the key in shell history. A reviewer may prefer a `read -rs` variant; I followed the plan.
- `deploy/observability/alertmanager/` was registered with `git add -N` (intent-to-add) to check `.gitattributes` eol; the deletion of `alertmanager.example.yml` is staged by `git rm`. Nothing is committed.
- `entrypoint.sh` is 120 lines, over the ~100-line guide (which targets C#). The bulk is the two literal YAML bodies.
- Placeholders only: `alerts@example.com`, `smoke-placeholder-not-a-key`, `smoke-file-placeholder`; no real credential anywhere.
