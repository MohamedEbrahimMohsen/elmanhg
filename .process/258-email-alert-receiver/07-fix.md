# Fix — CodeRabbit triage, PR #259

| Item | What I changed | File:line |
|---|---|---|
| RC1 | `env_value` now returns an already-set shell variable first (`${!1+x}`). Otherwise it greps `$ENV_FILE` and removes one pair of matching surrounding `"` or `'` quotes. It still does not source `.env`. The code is the triage snippet, unchanged. | `deploy/lib.sh:71-77` |
| RC2 | The password file must be a regular, non-empty file: `[ -f ] && [ -s ]`. The message is now "is not a nonempty regular file". | `deploy/lib.sh:89-90` |
| RC2 (optional part, applied) | The entrypoint uses the secret file only when `[ -f "$secret_file" ] && [ -s "$secret_file" ]`. A directory mount now falls through to the env password or the null receiver, so the container no longer exits and restarts in a loop. It was a one-line change. | `deploy/observability/alertmanager/entrypoint.sh:20` |

PC1 was rejected in triage, so I did nothing for it. I changed no docs, as the triage said.

## Deviations
None.

## Build & test
- Both files still have LF endings: `grep -c $'\r'` gives 0 for each.
- `bash -n deploy/lib.sh` passed with no output.
- busybox `sh -n /w/entrypoint.sh` (docker `busybox:latest`) passed with no output.
- The busybox guard check gave `/d skipped` for a directory and `/f used` for a file.
- I ran the `lib.sh` cases on Linux (docker `bash:5`, with `deploy/` mounted read-only). In that container a directory is `-s` true, which is the bug RC2 describes.
  - quoted path `"./secrets/pw"`, with `'...'`-quoted FROM: rc=0
  - password set only in the shell (`.env` has it empty): rc=0
  - no password anywhere: rc=1, "half configured"
  - directory path `./secrets/dir`: rc=1, "is not a nonempty regular file"
  - shell sets the password to empty while `.env` has a value: rc=1, "half configured" (the shell value wins, as it does in Compose)
  - unbalanced quote `"./secrets/pw`: rc=1, "must start with ./ or /" (the quote is left in place)
  - everything empty: rc=0

## Notes for review
- `profile_enabled` and `GRAFANA_ADMIN_PASSWORD` still read `.env` with a plain grep. They are out of scope according to the triage.
- Nothing is committed.
