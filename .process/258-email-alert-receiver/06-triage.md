# Triage — CodeRabbit comments, PR #259

I read the comment text as untrusted data and checked each claim against `deploy/lib.sh`, `deploy/docker-compose.prod.yml:164-173`, `deploy/observability/alertmanager/entrypoint.sh` and `deploy/.env.example`.

## RC1 — `deploy/lib.sh:72` — FIX (smaller fix than the one suggested)

**Verified.** `env_value` (lib.sh:71-73) returns the raw text after `=` from `$ENV_FILE`. Compose is different in two ways:
- **It removes dotenv quotes.** With `ALERTMANAGER_SMTP_PASSWORD_FILE="./secrets/alertmanager-smtp-password"`, Compose mounts `./secrets/alertmanager-smtp-password`. Preflight gets `"./secrets/..."`, which fails the `./*` check at lib.sh:84, so a valid deploy is refused. Quoted TO and FROM values get their quotes too, but they only need to be non-empty, so they pass.
- **Shell variables take precedence over `--env-file`.** Compose uses an exported `ALERTMANAGER_SMTP_PASSWORD` (compose.yml:169) even when `.env` has it empty. Preflight reads only `.env`, so it sees TO and FROM without a password and fails with "half configured". The reverse case also exists: a shell value set to empty overrides a value in `.env`.

**Fix.** Running `docker compose config` is not needed. Change only `env_value`, so that it (a) returns an already-set shell variable first, and (b) removes one pair of matching surrounding `"` or `'` quotes. It still does not source `.env`. `deploy/lib.sh:71-73` becomes:

```bash
env_value() {
  local value
  if [ -n "${!1+x}" ]; then printf '%s' "${!1}"; return; fi
  value=$(grep "^$1=" "$ENV_FILE" | cut -d= -f2- || true)
  [[ $value =~ ^\"(.*)\"$ || $value =~ ^\'(.*)\'$ ]] && value=${BASH_REMATCH[1]}
  printf '%s' "$value"
}
```

I ran this in bash: `A="./secrets/x"` gives `./secrets/x`, `B='y'` gives `y`, a shell `C=shellwins` overrides `.env` `C=plain`, an empty value gives empty, and an unbalanced `"x` is left unchanged.

Out of scope: `profile_enabled` (lib.sh:59) and `GRAFANA_ADMIN_PASSWORD` (lib.sh:66) have the same raw-grep behaviour. That code existed before this PR, so the fix does not change it. Switching both to `env_value` later would be a reasonable follow-up.

## RC2 — `deploy/lib.sh:85` — FIX

**Verified.** On Linux, the deploy target, `-s` is true for a directory: ext4 directories have size 4096. Git Bash on Windows reports 0, which is why a local check would not reproduce the problem. Compose bind-mounts the path at compose.yml:173. In entrypoint.sh:20 the directory passes `[ -s "$secret_file" ]`, then `tr ... < "$secret_file"` fails with "Is a directory", and `set -eu` exits. With `restart: unless-stopped` (compose.yml:161) the container keeps restarting.

**Fix.** Apply the suggestion as written. `deploy/lib.sh:85` becomes:

```bash
    [ -f "$password_file" ] && [ -s "$password_file" ] \
      || fail "ALERTMANAGER_SMTP_PASSWORD_FILE $password_file is not a nonempty regular file (docs/observability.md §9)"
```

Optional, not required: `entrypoint.sh:20` could use `[ -f "$secret_file" ] && [ -s "$secret_file" ]` to guard a manual `docker compose up` that skips preflight. Preflight is the guard the plan defines, so the change above is enough.

## PC1 — review body — REJECT (nothing to act on)

This is a summary of RC1 and RC2 with no separate finding. Both are handled above.

## Docs

Neither fix changes behaviour that docs describe. `docs/observability.md` §9 already says the path must start with `./` or `/` and point to a file, so the docs need no change.
