#!/usr/bin/env bash
# Record a change to the pipeline package. Run after editing ANY file under global/.claude or project/.claude.
#   ./bump.sh global  "reviewer: check Postman assertions"     → 1.0.3 → 1.0.4 in global/.claude/manifest.yml
#   ./bump.sh project "react skill: forbid default exports"    → project/.claude/manifest.yml
#   ./bump.sh global  "…" --minor                              → 1.0.4 → 1.1.0
# Same rule applies inside an installed ~/.claude or <repo>/.claude: run it there with the path:
#   ./bump.sh ~/.claude "…"       ./bump.sh <repo>/.claude "…"
# The version is how the installer and /pipeline-sync detect divergence. Editing without bumping hides your change.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
case "${1:-}" in global) DIR="$HERE/global/.claude";; project) DIR="$HERE/project/.claude";; "") echo "usage: bump.sh <global|project|path> \"message\" [--minor|--major]"; exit 1;; *) DIR="$1";; esac
MSG="${2:?message required}"; LEVEL="${3:-}"
M="$DIR/manifest.yml"; [ -e "$M" ] || { echo "no manifest at $M"; exit 1; }
cur="$(grep -m1 '^version:' "$M" | awk '{print $2}')"; IFS=. read -r a b c <<<"$cur"
case "$LEVEL" in --major) a=$((a+1)); b=0; c=0;; --minor) b=$((b+1)); c=0;; *) c=$((c+1));; esac
new="$a.$b.$c"; who="$(git config user.name 2>/dev/null || whoami)"; when="$(date +%F)"
# rewrite version + append change; regenerate file hashes (skip repo-owned data files)
{
  echo "package: momenta-feature-pipeline"
  echo "side: $(grep -m1 '^side:' "$M" | awk '{print $2}')"
  echo "version: $new"
  echo "base: $(grep -m1 '^base:' "$M" | awk '{print $2}')"
  echo "files:"
  (cd "$DIR" && find . -type f ! -name 'manifest.yml*' ! -name 'pipeline*.yml' ! -name triage-memory.md ! -name design-system.md ! -path './.pipeline-incoming/*' -print0 | sort -z | xargs -0 sha256sum | awk '{printf "  %s: %s\n", substr($2,3), substr($1,1,12)}')
  echo "changes:"
  sed -n '/^changes:/,$p' "$M" | tail -n +2
  echo "  - { v: $new, date: $when, by: \"$who\", note: \"$MSG\" }"
} > "$M.tmp" && mv "$M.tmp" "$M"
echo "$DIR → v$new  ($MSG)"
