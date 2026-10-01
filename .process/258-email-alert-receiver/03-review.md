VERDICT: APPROVED

# Review — Email alert receiver (E18.S6, #258)

## Blocking
None.

## Non-blocking
- `deploy/lib.sh:76-89` — the pre-deploy check catches only half-configured keys and a bad file path (plan Decision 7). A fully set but invalid value (TO without `@`, SMARTHOST without `:`, a quote) passes `deploy.sh`, and the container falls back to the null receiver with only a stderr warning. This matches the plan; a later story could reuse the entrypoint's value checks in lib.sh.
- `deploy/lib.sh:85` with `deploy/observability/alertmanager/entrypoint.sh:21,26` — a secret file holding only `\n` passes `[ -s ]` in lib.sh but is empty after `tr`, so the container uses the null receiver. This is an edge case and is logged.
- `docs/observability.md` §9 secret-file snippet — `printf '%s' '<key>' >` leaves the key in shell history (the implementer flagged this too). A `read -rs` variant would avoid it. This follows plan Decision 17.
- `docs/implementation-report.md:200` — the #213 row "Real Alertmanager receivers" could now say "the Resend key and alert addresses for alert email". It is still accurate, because live delivery is deferred.

## Verified
- Both planned files exist. Nothing else was created (`git status`: the only additions are `entrypoint.sh` and the 0-byte placeholder; `alertmanager.example.yml` is staged as deleted).
- `entrypoint.sh` follows the plan's ordered steps 1–10 verbatim: `set -eu`, `umask 077`, the file-wins password resolution with CR/LF stripped, the `nl`/`cr` `unsafe()` check, the reason order, `rm -f` of the password on the null fallback, `require_tls` false only for `*:465`, both YAML bodies, `--check`, and `exec` with `"$@"`. The password is never echoed. The YAML holds only `auth_password_file`. No `set -x`.
- LF endings: `git ls-files --eol` shows `w/lf attr/text eol=lf`, and `entrypoint.sh` has 0 CR bytes. The placeholder is 0 bytes.
- Re-ran inside `prom/alertmanager:v0.34.1@sha256:e9733baf…` (busybox sh):
  - Null `--check`: the info line, the null YAML, and `SUCCESS`.
  - Env password, comma-separated TO, `:465`, and a password containing `$` and `\`: email YAML with `require_tls: false`, the password absent from the output, and `SUCCESS`.
- Re-ran via `docker compose ... run --rm --no-deps -e TO -e FROM alertmanager --check` with a shell-env `ALERTMANAGER_SMTP_PASSWORD_FILE` (content `value\r\n`) and an empty env password. The shell env overrides `--env-file`, the file is used (email mode), the subject prefix comes from `COMPOSE_PROJECT_NAME`, and the output does not contain the value. This confirms the smoke `am_file` check would fail if the file were ignored.
- `docker compose ... --profile observability config alertmanager`: the entrypoint, the 6 env keys (`ALERTMANAGER_ENVIRONMENT: elmanhg-prod`), the 3 volumes (the placeholder is the default secret source), and the tmpfs. The image digest, healthcheck, mem_limit and logging are unchanged.
- `lib.sh` `check_alert_email_config`, re-run:
  - TO plus FROM without a password: fails "half configured".
  - The password file set to the empty placeholder: fails "missing or empty".
  - `.env.example` as is: passes, which the smoke test relies on.
  - `[ -z ... ] && return 0` is safe under `set -e` because it is not the function's last command.
- `deploy.sh` `cd`s to `deploy/` before `check_observability_config`, so the relative `-s` check and compose's bind path resolve to the same file. `.github/workflows/deploy.yml:70` copies all of `deploy/observability`, so `entrypoint.sh` and the placeholder reach the host.
- Smoke test (`deploy/smoke-test.sh:66-78,212-220`):
  - The host-file lines are gone, and the three `--check` variants assert what the plan says.
  - The project name is interpolated, not a literal.
  - The test alert is added and then queried through `eventually`.
  - The full smoke run was not repeated here: it is a long image build. The Alertmanager parts were reproduced directly, as above.
- `.gitignore`: `/deploy/secrets/` replaces `/deploy/alertmanager.yml`. `.env.example` holds the 6 keys with empty values or the Resend defaults. No real credential appears anywhere.
- Docs sync:
  - `docs/observability.md` §1, §2, §9, §13 and §15, `docs/deployment.md` §1, §3, §4, §6, §7, §12 and §13, and `docs/implementation-report.md:223` all match the code.
  - `ALERTMANAGER_CONFIG_FILE`, `alertmanager.example` and "webhook receiver" appear nowhere outside `.process/`.
  - Both declared deviations (the run-section left as is; an extra `compose` row for `ALERTMANAGER_ENVIRONMENT`) are accurate and harmless.
- Postman: no API change, so not applicable.

## Test quality
The smoke checks constrain the implementation:
- `am_null` fails if email config leaks into the default.
- `am_email` fails on a wrong route, smarthost, critical child route or subject prefix, or if the password leaks.
- `am_file` can only reach `receiver: email` through the file, because the env password is empty in `.smoke/.env`.
- The test-alert query fails if `amtool add` was a no-op.

There are no vacuous checks. There is no unit-test framework for shell in this repo (plan Test plan).
