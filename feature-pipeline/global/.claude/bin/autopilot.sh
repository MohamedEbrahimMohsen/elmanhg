#!/usr/bin/env bash
# autopilot.sh — unattended supervisor for the feature pipeline. POC repos ONLY.
#
#   ~/.claude/bin/autopilot.sh --epic 12              run every open story of epic #12, in order
#   ~/.claude/bin/autopilot.sh --prd docs/PRD.md      run /product first, then its epic
#   options: --max-tasks N (stop after N /feature runs) · -h
#
# Refuses to start unless .claude/pipeline.local.yml has `autopilot: on: true` and the git tree is clean.
# One fresh `claude -p` process per story step (/feature does one sub-task per process, then prints RESULT:).
# Never stops on failures: a failed task is labelled autopilot:blocked and skipped; a red main gets a
# "repair main" task first; a subscription usage limit → wait and resume (does not count as an attempt).
# Stops only on: all done · `hours` reached (default 72) · nothing runnable left (all remaining depend on blocked)
#                · main still red after max_attempts repair tasks · --max-tasks · Ctrl+C.
# Files: .process/autopilot/{ledger.md,REPORT.md,logs/} (git-ignored via .process/autopilot/.gitignore).
# Env overrides: AUTOPILOT_MAX_TURNS (400) · AUTOPILOT_VERIFY_MAIN (1: run scripts/verify.sh --fast on base between tasks)
#                CLAUDE_BIN (claude) · GH_BIN (gh)
set -euo pipefail

CLAUDE_BIN="${CLAUDE_BIN:-claude}"
GH_BIN="${GH_BIN:-gh}"
MAX_TURNS="${AUTOPILOT_MAX_TURNS:-400}"
VERIFY_MAIN="${AUTOPILOT_VERIFY_MAIN:-1}"

EPIC=""; PRD=""; MAX_TASKS=0

say()  { printf '[autopilot %s] %s\n' "$(date -u +%H:%M:%S)" "$*"; }
die()  { printf 'autopilot: %s\n' "$*" >&2; exit 1; }
usage() { sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'; }

while [ $# -gt 0 ]; do
  case "$1" in
    --epic) EPIC="${2:-}"; EPIC="${EPIC#\#}"; shift 2 ;;
    --prd) PRD="${2:-}"; shift 2 ;;
    --max-tasks) MAX_TASKS="${2:-0}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) usage; die "unknown argument: $1" ;;
  esac
done
[ -n "$EPIC" ] || [ -n "$PRD" ] || { usage; die "need --epic <n> or --prd <path>"; }
case "$EPIC" in ''|*[!0-9]*) [ -z "$EPIC" ] || die "--epic must be a number" ;; esac
case "$MAX_TASKS" in *[!0-9]*|'') die "--max-tasks must be a number" ;; esac

ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || die "not inside a git repository"
cd "$ROOT"

LOCAL=".claude/pipeline.local.yml"
TEAM=".claude/pipeline.yml"
[ -f "$TEAM" ] || die "no $TEAM — run install.sh --project . first"
[ -f "$LOCAL" ] || die "no $LOCAL — autopilot is enabled only there (see pipeline.local.example.yml)"

# ---------- config (plain awk/grep; no yq needed) ----------
# Prints the uncommented lines of the top-level `autopilot:` block of a yml file.
block_of() {
  awk '
    /^autopilot:[[:space:]]*(#.*)?$/ { f = 1; next }
    f && /^[^[:space:]#]/             { f = 0 }
    f && !/^[[:space:]]*#/            { print }
  ' "$1"
}
# ap_get <key> <default> — first `key: value` inside the autopilot block (plain or { flow } style).
ap_get() {
  local v
  v="$(block_of "$LOCAL" | sed 's/#.*$//' | grep -oE "(^|[[:space:],{])$1:[[:space:]]*[^,}[:space:]]+" | head -n 1 | sed -E "s/.*$1:[[:space:]]*//; s/[\"']//g" || true)"
  printf '%s' "${v:-$2}"
}
num_or() { case "$1" in ''|*[!0-9.]*) printf '%s' "$2" ;; *) printf '%s' "$1" ;; esac; }

if [ -n "$(block_of "$TEAM")" ]; then
  say "warning: autopilot block in $TEAM ignored — only $LOCAL can turn it on"
fi

AP_ON="$(ap_get on false)"
BUDGET_USD="$(num_or "$(ap_get usd 0)" 0)"          # 0 = no money cap (subscription). Old key, still honoured if set.
BUDGET_HOURS="$(num_or "$(ap_get hours 72)" 72)"
MAX_ATTEMPTS="$(num_or "$(ap_get max_attempts 3)" 3)"
TIMEOUT_MIN="$(num_or "$(ap_get timeout_min 240)" 240)"
ON_BLOCKED="$(ap_get on_blocked skip)"
BASE="$(awk '/^github:/{f=1;next} f&&/^[^[:space:]#]/{f=0} f&&/^[[:space:]]+base:/{print $2; exit}' "$TEAM")"
BASE="${BASE:-main}"

[ "$AP_ON" = "true" ] || die "autopilot.on is not true in $LOCAL — refusing to start (human gates stay on)"
case "$ON_BLOCKED" in skip|stop) ;; *) say "warning: autopilot.on_blocked '$ON_BLOCKED' unknown — using skip"; ON_BLOCKED=skip ;; esac
RATE_WAIT_MIN="${AUTOPILOT_RATE_WAIT_MIN:-20}"   # minutes to sleep when the subscription usage limit is hit
REPAIRS=0

# ---------- tools ----------
command -v "$CLAUDE_BIN" >/dev/null 2>&1 || die "$CLAUDE_BIN not on PATH"
command -v "$GH_BIN" >/dev/null 2>&1 || die "$GH_BIN not on PATH"
if command -v timeout >/dev/null 2>&1; then TIMEOUT=timeout
elif command -v gtimeout >/dev/null 2>&1; then TIMEOUT=gtimeout
else die "timeout (coreutils) not on PATH — macOS: brew install coreutils"; fi
HAVE_JQ=0; command -v jq >/dev/null 2>&1 && HAVE_JQ=1
"$GH_BIN" auth status >/dev/null 2>&1 || die "gh is not authenticated (gh auth login)"

# ---------- state dir (ignored by git through its own .gitignore) ----------
AP=".process/autopilot"
LOGS="$AP/logs"
mkdir -p "$LOGS"
[ -f "$AP/.gitignore" ] || printf '*\n' > "$AP/.gitignore"
LEDGER="$AP/ledger.md"
REPORT="$AP/REPORT.md"

[ -z "$(git status --porcelain)" ] || { git status --short >&2; die "git tree is not clean — commit or stash first"; }
if [ ! -f .claude/settings.local.json ] || ! grep -q '"deny"' .claude/settings.local.json; then
  say "warning: no permissions.deny in .claude/settings.local.json — copy ~/.claude/settings.autopilot.example.json there"
fi

export BASH_DEFAULT_TIMEOUT_MS="${BASH_DEFAULT_TIMEOUT_MS:-300000}"
export BASH_MAX_TIMEOUT_MS="${BASH_MAX_TIMEOUT_MS:-1200000}"

iso() { date -u -d "@$1" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -r "$1" +%Y-%m-%dT%H:%M:%SZ; }
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
START="$(date +%s)"
DEADLINE="$(awk -v s="$START" -v h="$BUDGET_HOURS" 'BEGIN { printf "%d", s + h * 3600 }')"
DEADLINE_ISO="$(iso "$DEADLINE")"
TRIES="$AP/tries-$RUN_ID"
: > "$TRIES"

SPENT=0; TASKS=0; MERGED=0; BLOCKED=0; OTHER=0; CONSEC=0
STOP_REASON=""

[ -f "$LEDGER" ] || {
  printf '# Autopilot ledger\n\n| Run | # | Issue | Started (UTC) | Minutes | Cost $ | Exit | Result |\n|-----|---|-------|---------------|---------|--------|------|--------|\n' > "$LEDGER"
}

add_cost() { SPENT="$(awk -v a="$SPENT" -v b="$1" 'BEGIN { printf "%.4f", a + b }')"; }
over_budget() { awk -v a="$SPENT" -v b="$BUDGET_USD" 'BEGIN { exit !(b > 0 && a >= b) }'; }
remaining_usd() { awk -v a="$SPENT" -v b="$BUDGET_USD" 'BEGIN { if (b <= 0) { printf "unlimited"; exit } r = b - a; if (r < 0) r = 0; printf "%.2f", r }'; }
# rate_limited <json> <err> → 0 when the process ended on a subscription usage / rate limit
rate_limited() { grep -qiE 'usage limit|rate.?limit|limit reached|resets at|429|overloaded' "$1" "$2" 2>/dev/null; }

# ---------- one claude process ----------
# run_claude <label> <prompt>  → sets RC, COST, RESULT; logs to $LOGS/<run>-<label>.{json,err}
run_claude() {
  local label="$1" prompt="$2" json err t0
  json="$LOGS/$RUN_ID-$label.json"; err="$LOGS/$RUN_ID-$label.err"
  t0="$(date +%s)"
  say "run: $prompt  (timeout ${TIMEOUT_MIN}m, budget left \$$(remaining_usd))"
  local budget_args=()
  [ "$(remaining_usd)" = unlimited ] || budget_args=(--max-budget-usd "$(remaining_usd)")
  set +e
  "$TIMEOUT" -s INT -k 120 "${TIMEOUT_MIN}m" \
    "$CLAUDE_BIN" -p "$prompt" \
      --permission-mode bypassPermissions \
      --output-format json \
      --max-turns "$MAX_TURNS" \
      ${budget_args[@]+"${budget_args[@]}"} \
      2> "$err" | tee "$json" > /dev/null
  RC="${PIPESTATUS[0]}"
  set -e
  ELAPSED_MIN="$(awk -v a="$t0" -v b="$(date +%s)" 'BEGIN { printf "%.1f", (b - a) / 60 }')"
  if [ "$HAVE_JQ" = 1 ] && jq -e . "$json" >/dev/null 2>&1; then
    COST="$(jq -r '.total_cost_usd // 0' "$json")"
    RESULT="$(jq -r '.result // ""' "$json" | grep -E '^RESULT:' | tail -n 1 || true)"
  else
    COST="$(grep -oE '"total_cost_usd"[[:space:]]*:[[:space:]]*[0-9.eE+-]+' "$json" | tail -n 1 | sed -E 's/.*:[[:space:]]*//' || true)"
    RESULT="$(grep -oE 'RESULT: [^"\\]*' "$json" | tail -n 1 || true)"
  fi
  COST="$(num_or "${COST:-0}" 0)"
  if [ -z "$RESULT" ]; then
    if [ "$RC" = 124 ] || [ "$RC" = 137 ]; then RESULT="RESULT: stopped timeout"
    elif rate_limited "$json" "$err"; then RESULT="RESULT: stopped rate-limit"
    else RESULT="RESULT: stopped no-result (exit $RC)"; fi
  fi
  add_cost "$COST"
  printf '| %s | %s | %s | %s | %s | %s | %s | %s |\n' "$RUN_ID" "$TASKS" "$label" "$(iso "$t0")" "$ELAPSED_MIN" "$COST" "$RC" "${RESULT#RESULT: }" >> "$LEDGER"
  say "→ ${RESULT} · \$$COST · ${ELAPSED_MIN} min · spent \$$SPENT / \$$BUDGET_USD"
}

# ---------- GitHub helpers ----------
lower() { tr '[:upper:]' '[:lower:]'; }

# children_of <epic> → lines "number<TAB>state<TAB>labels" in order
children_of() {
  local out n
  out="$("$GH_BIN" api "repos/{owner}/{repo}/issues/$1/sub_issues?per_page=100" \
          --jq '.[] | "\(.number)\t\(.state)\t\([.labels[].name] | join(","))"' 2>/dev/null || true)"
  if [ -n "$out" ]; then printf '%s\n' "$out"; return 0; fi
  # fallback: task list / #refs in the epic body, in order
  for n in $("$GH_BIN" issue view "$1" --json body --jq .body 2>/dev/null | grep -oE '#[0-9]+' | tr -d '#' | awk '!seen[$0]++'); do
    [ "$n" = "$1" ] && continue
    "$GH_BIN" issue view "$n" --json state,labels \
      --jq "\"$n\t\(.state)\t\([.labels[].name] | join(\",\"))\"" 2>/dev/null || true
  done
}

# has_open_blocker <n> → 0 when an issue it is blocked by is still open
has_open_blocker() {
  local d st api
  api="$("$GH_BIN" api "repos/{owner}/{repo}/issues/$1/dependencies/blocked_by" \
          --jq '.[] | select(.state == "open") | .number' 2>/dev/null || true)"
  [ -n "$api" ] && return 0
  for d in $("$GH_BIN" issue view "$1" --json body --jq .body 2>/dev/null | grep -ioE 'blocked by #[0-9]+' | grep -oE '[0-9]+' || true); do
    st="$("$GH_BIN" issue view "$d" --json state --jq .state 2>/dev/null | lower || true)"
    [ "$st" = "open" ] && return 0
  done
  return 1
}

tries_of() { grep -cx "$1" "$TRIES" 2>/dev/null || true; }

# next_story → prints the next runnable issue number, or nothing
next_story() {
  local n st labels t
  while IFS="$(printf '\t')" read -r n st labels; do
    [ -n "$n" ] || continue
    st="$(printf '%s' "$st" | lower)"
    [ "$st" = "open" ] || continue
    case ",$labels," in *",autopilot:blocked,"*) continue ;; esac
    t="$(tries_of "$n")"; [ "${t:-0}" -lt "$MAX_ATTEMPTS" ] || continue
    has_open_blocker "$n" && continue
    printf '%s\n' "$n"; return 0
  done <<EOF
$(children_of "$EPIC")
EOF
  return 0
}

open_children() {
  children_of "$EPIC" | awk -F '\t' 'tolower($2) == "open" { printf "#%s%s ", $1, ($3 ~ /autopilot:blocked/ ? " (blocked)" : "") }'
}

mark_blocked() {
  "$GH_BIN" issue edit "$1" --add-label "autopilot:blocked" >/dev/null 2>&1 || say "warning: could not label #$1"
  "$GH_BIN" issue comment "$1" --body "autopilot run $RUN_ID: blocked — $2. Logs: .process/autopilot/logs/$RUN_ID-$1.*" >/dev/null 2>&1 || true
}

# ---------- base branch hygiene ----------
sync_base() {
  if [ -n "$(git status --porcelain)" ]; then
    git stash push -u -m "autopilot $RUN_ID leftover after task $TASKS" >/dev/null
    say "stashed leftover changes (git stash list)"
  fi
  if ! { git switch -q "$BASE" && git pull -q --ff-only origin "$BASE"; }; then
    STOP_REASON="cannot switch/pull $BASE"; return 1
  fi
  if [ "$VERIFY_MAIN" = 1 ] && [ -x scripts/verify.sh ]; then
    if ! scripts/verify.sh --fast > "$LOGS/$RUN_ID-verify-$TASKS.log" 2>&1; then
      MAIN_RED_LOG="$LOGS/$RUN_ID-verify-$TASKS.log"
      return 2
    fi
  fi
  return 0
}

# ---------- repair main ----------
# repair_main → creates a "Repair main" issue under the epic and runs /feature on it until main is green.
# Returns 0 when main is green again, 1 after max_attempts repair runs (the run then stops: building on red is waste).
repair_main() {
  local sha body rn rc
  while :; do
    REPAIRS=$((REPAIRS + 1))
    if [ "$REPAIRS" -gt "$MAX_ATTEMPTS" ]; then
      STOP_REASON="main still red after $MAX_ATTEMPTS repair tasks — see ${MAIN_RED_LOG:-logs}"; return 1
    fi
    sha="$(git rev-parse --short HEAD)"
    body="Autopilot: scripts/verify.sh --fast fails on $BASE at $sha.
Fix main so verify passes. Smallest change; no feature work.

Last lines of the failure:

$(tail -n 60 "${MAIN_RED_LOG:-/dev/null}" 2>/dev/null)"
    rn="$("$GH_BIN" issue create --title "Repair main: verify failing at $sha" --body "$body" --label "autopilot:repair" 2>/dev/null | grep -oE '[0-9]+$' || true)"
    [ -n "$rn" ] || { STOP_REASON="main red and could not create a repair issue"; return 1; }
    [ -n "$EPIC" ] && "$GH_BIN" api -X POST "repos/{owner}/{repo}/issues/$EPIC/sub_issues" -F sub_issue_id="$("$GH_BIN" api "repos/{owner}/{repo}/issues/$rn" --jq .id 2>/dev/null)" >/dev/null 2>&1 || true
    say "main red at $sha → repair task #$rn (repair $REPAIRS/$MAX_ATTEMPTS)"
    TASKS=$((TASKS + 1))
    run_claude "repair-$rn" "/feature #$rn --autopilot --deadline $DEADLINE_ISO"
    set +e; sync_base; rc=$?; set -e
    [ "$rc" = 0 ] && { say "main green again"; return 0; }
    [ "$rc" = 1 ] && return 1
  done
}

# ---------- report ----------
# shellcheck disable=SC2329  # invoked by trap
finish() {
  local end; end="$(date +%s)"
  [ -n "$STOP_REASON" ] || STOP_REASON="interrupted"
  {
    printf '# Autopilot report — run %s\n\n' "$RUN_ID"
    printf '| Field | Value |\n|---|---|\n'
    printf '| Epic | #%s |\n' "${EPIC:-?}"
    printf '| Started / ended (UTC) | %s / %s |\n' "$(iso "$START")" "$(iso "$end")"
    printf '| Wall time | %s h of %s h |\n' "$(awk -v a="$START" -v b="$end" 'BEGIN { printf "%.2f", (b - a) / 3600 }')" "$BUDGET_HOURS"
    printf '| Cost (reported by claude) | $%s (cap: %s) |\n' "$SPENT" "$( [ "$BUDGET_USD" = 0 ] && echo none || echo "\$$BUDGET_USD")"
    printf '| /feature runs | %s (merged %s · blocked %s · other %s) |\n' "$TASKS" "$MERGED" "$BLOCKED" "$OTHER"
    printf '| Stop reason | %s |\n' "$STOP_REASON"
    printf '| Still open | %s |\n\n' "$( [ -n "$EPIC" ] && open_children || true)"
    printf '## Runs\n\n| Run | # | Issue | Started (UTC) | Minutes | Cost $ | Exit | Result |\n|-----|---|-------|---------------|---------|--------|------|--------|\n'
    grep -F "| $RUN_ID |" "$LEDGER" || true
    # shellcheck disable=SC2016  # backticks are markdown
    printf '\nLogs: `%s/%s-*` · per-task detail: `.process/<issue>-<slug>/00-status.md` and `metrics.md`\n' "$LOGS" "$RUN_ID"
  } > "$REPORT"
  rm -f "$TRIES"
  say "stopped: $STOP_REASON — report: $REPORT"
}
trap 'finish' EXIT
trap 'STOP_REASON="interrupted (signal)"; exit 130' INT TERM

say "AUTOPILOT (POC) run $RUN_ID · base $BASE · ${BUDGET_HOURS}h (deadline $DEADLINE_ISO) · attempts $MAX_ATTEMPTS · task timeout ${TIMEOUT_MIN}m · on_blocked $ON_BLOCKED"
"$GH_BIN" label create "autopilot:blocked" --color B60205 --description "autopilot gave up on this issue" --force >/dev/null 2>&1 || true
"$GH_BIN" label create "autopilot:repair" --color FBCA04 --description "autopilot: fix a red main" --force >/dev/null 2>&1 || true

# sync_base_or_repair → 0 green · exits the run only when repair is impossible
sync_base_or_repair() {
  local rc
  set +e; sync_base; rc=$?; set -e
  case "$rc" in 0) return 0 ;; 2) repair_main ;; *) return 1 ;; esac
}

sync_base_or_repair || exit 1

# ---------- optional: PRD → epic ----------
if [ -n "$PRD" ]; then
  [ -f "$PRD" ] || { STOP_REASON="no PRD at $PRD"; exit 1; }
  TASKS=$((TASKS + 1))
  run_claude "product" "/product $PRD --autopilot"
  EPIC="$(printf '%s' "$RESULT" | grep -oE 'epic #[0-9]+' | grep -oE '[0-9]+' || true)"
  [ -n "$EPIC" ] || { STOP_REASON="/product did not create an epic: $RESULT"; exit 1; }
  say "epic #$EPIC"
  sync_base_or_repair || exit 1
fi

# ---------- main loop ----------
while :; do
  now="$(date +%s)"
  if [ "$now" -ge "$DEADLINE" ]; then STOP_REASON="hours budget reached (${BUDGET_HOURS}h)"; break; fi
  if over_budget; then STOP_REASON="usd budget reached (\$$SPENT of \$$BUDGET_USD)"; break; fi
  if [ "$MAX_TASKS" -gt 0 ] && [ "$TASKS" -ge "$MAX_TASKS" ]; then STOP_REASON="--max-tasks $MAX_TASKS reached"; break; fi

  n="$(next_story)"
  if [ -z "$n" ]; then
    left="$(open_children)"
    if [ -z "$left" ]; then STOP_REASON="all done"; else STOP_REASON="nothing runnable (open: $left)"; fi
    break
  fi

  TASKS=$((TASKS + 1))
  printf '%s\n' "$n" >> "$TRIES"
  run_claude "$n" "/feature #$n --autopilot --deadline $DEADLINE_ISO"

  if [ "$RESULT" = "RESULT: stopped rate-limit" ]; then
    # subscription usage limit: not the task's fault — undo the attempt, wait, retry the same task
    awk -v n="$n" 'BEGIN{d=0} $0==n && !d {d=1; next} {print}' "$TRIES" > "$TRIES.tmp" && mv "$TRIES.tmp" "$TRIES"
    say "usage limit hit — sleeping ${RATE_WAIT_MIN}m, then retrying #$n"
    sleep "$((RATE_WAIT_MIN * 60))"
    sync_base_or_repair || break
    continue
  fi

  case "$RESULT" in
    "RESULT: merged"*|"RESULT: done"*)
      MERGED=$((MERGED + 1)); CONSEC=0
      # a story with more sub-tasks stays open and is picked again; reset its failure count
      grep -vx "$n" "$TRIES" > "$TRIES.tmp" || true; mv "$TRIES.tmp" "$TRIES" ;;
    "RESULT: blocked"*)
      BLOCKED=$((BLOCKED + 1)); CONSEC=$((CONSEC + 1))
      mark_blocked "$n" "${RESULT#RESULT: blocked }"
      if [ "$ON_BLOCKED" = stop ]; then STOP_REASON="#$n blocked and on_blocked: stop — ${RESULT#RESULT: }"; break; fi ;;
    "RESULT: stopped main-red"*)
      OTHER=$((OTHER + 1))
      MAIN_RED_LOG="$LOGS/$RUN_ID-$n.json"
      repair_main || break ;;
    "RESULT: stopped deadline"*)
      OTHER=$((OTHER + 1)); STOP_REASON="deadline reached inside #$n"; break ;;
    "RESULT: stopped autopilot-off"*)
      OTHER=$((OTHER + 1)); STOP_REASON="/feature says autopilot is off"; break ;;
    *)
      OTHER=$((OTHER + 1)); CONSEC=$((CONSEC + 1))
      t="$(tries_of "$n")"
      if [ "${t:-0}" -ge "$MAX_ATTEMPTS" ]; then mark_blocked "$n" "runner: $t non-merged runs (${RESULT#RESULT: })"; BLOCKED=$((BLOCKED + 1)); fi ;;
  esac

  # no stop on consecutive failures: blocked tasks are skipped, the run keeps going (CONSEC is only reported)
  sync_base_or_repair || break
done

exit 0
