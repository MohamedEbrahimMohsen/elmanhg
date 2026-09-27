#!/usr/bin/env bash
# Feature pipeline installer. Bash (Linux, macOS, Git Bash on Windows). PowerShell twin: install.ps1
#
#   ./install.sh                 → interactive: global + project in the current directory
#   ./install.sh --global        → only ~/.claude
#   ./install.sh --project DIR   → only DIR/.claude, asks for the role
#   ./install.sh --role fullstack --project DIR   → no questions
#
# Roles: backend (dotnet) · frontend (react) · fullstack (dotnet+react) · mobile-flutter · mobile-kmp · ai (python) · custom
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_GLOBAL="$HERE/global/.claude"; SRC_PROJECT="$HERE/project/.claude"
DST_GLOBAL="${CLAUDE_HOME:-$HOME/.claude}"
DO_GLOBAL=0; DO_PROJECT=0; PROJECT_DIR="$PWD"; ROLE=""
while [ $# -gt 0 ]; do case "$1" in
  --global) DO_GLOBAL=1;; --project) DO_PROJECT=1; PROJECT_DIR="$2"; shift;; --role) ROLE="$2"; shift;;
  -h|--help) sed -n '2,10p' "$0"; exit 0;; *) echo "unknown arg $1"; exit 1;; esac; shift; done
[ "$DO_GLOBAL$DO_PROJECT" = "00" ] && DO_GLOBAL=1 && DO_PROJECT=1

ver()  { [ -e "$1/manifest.yml" ] && grep -m1 '^version:' "$1/manifest.yml" | awk '{print $2}' || true; }
sha()  { sha256sum "$1" | cut -c1-12; }
ask()  { local a; read -r -p "$1 " a; echo "$a"; }
# pristine copy for 3-way merges later (/pipeline-sync)
keep_base() { local side; case "$3" in global*) side=global;; *) side=project;; esac; mkdir -p "$DST_GLOBAL/.pipeline-base/$2/$side"; cp -R "$1/." "$DST_GLOBAL/.pipeline-base/$2/$side/"; }

# Per-file compare of a tree. Prints: same / changed / missing / extra. Skips repo-owned data files.
other_stack() { # rel → 0 if the file belongs to a stack NOT in the installed pipeline.yml
  local rel="$1" st
  case "$rel" in skills/*-feature/*) st="${rel#skills/}"; st="${st%%-feature/*}";; conventions/*-testing.md) st="${rel#conventions/}"; st="${st%-testing.md}";; *) return 1;; esac
  grep -qE "^  $st:" "$PIPE" 2>/dev/null && return 1 || return 0
}
compare_tree() { # src dst
  local src="$1" dst="$2" rel s d; PIPE="$dst/pipeline.yml"
  while IFS= read -r -d '' f; do
    rel="${f#$src/}"
    case "$rel" in pipeline.yml|pipeline.local.yml|triage-memory.md|design-system.md|manifest.yml) continue;; esac
    other_stack "$rel" && continue
    if [ ! -e "$dst/$rel" ]; then echo "missing  $rel"
    elif [ "$(sha "$f")" != "$(sha "$dst/$rel")" ]; then echo "changed  $rel"
    fi
  done < <(find "$src" -type f -print0)
  # extra = a file the installed manifest lists that the package no longer ships (removed upstream).
  # The dev's own files in ~/.claude are never listed: they are not the package's business.
  [ -e "$dst/manifest.yml" ] && sed -n '/^files:/,/^changes:/p' "$dst/manifest.yml" | grep -E '^  \S+: ' | awk -F': ' '{print $1}' | sed 's/^  //' | while read -r rel; do
    [ -e "$src/$rel" ] || other_stack "$rel" || echo "removed  $rel"
  done
}

install_tree() { # src dst label
  local src="$1" dst="$2" label="$3" new cur diff
  new="$(ver "$src")"; cur="$(ver "$dst")"
  if [ -z "$cur" ]; then
    echo "→ $label: fresh install v$new"; mkdir -p "$dst"
    # files that already exist at the destination and differ: never overwrite silently
    local clash; clash="$(cd "$src" && find . -type f -print0 | while IFS= read -r -d '' rel; do rel="${rel#./}"; if [ -e "$dst/$rel" ] && [ "$(sha "$src/$rel")" != "$(sha "$dst/$rel")" ]; then echo "$rel"; fi; done; true)"
    if [ -n "$clash" ]; then
      echo "  these already exist in $dst and differ from the package:"; echo "$clash" | sed 's/^/    /'
      echo "  Options: [o]verwrite them · [k]eep yours (package versions saved next to them as *.pkg) · [a]bort"
      case "$(ask "  choose:")" in
        o) ;;
        k) echo "$clash" | while read -r rel; do cp "$src/$rel" "$dst/$rel.pkg"; done
           (cd "$src" && find . -type f -print0) | while IFS= read -r -d '' rel; do rel="${rel#./}"; echo "$clash" | grep -qx "$rel" || { mkdir -p "$(dirname "$dst/$rel")"; cp "$src/$rel" "$dst/$rel"; }; done
           keep_base "$src" "$new" "$label"; echo "  installed v$new; your files kept, package copies as *.pkg — diff them when you have time"; return;;
        *) echo "  aborted"; exit 1;;
      esac
    fi
    cp -R "$src/." "$dst/"; keep_base "$src" "$new" "$label"; return
  fi
  echo "→ $label: installed v$cur, package v$new"
  diff="$(compare_tree "$src" "$dst")"
  if [ -z "$diff" ] && [ "$cur" = "$new" ]; then echo "  identical, nothing to do"; return; fi
  echo "$diff" | sed 's/^/  /'
  echo "  changed = edited locally · missing = new in package · removed = package no longer ships it"
  echo "  Options: [u]pdate all (overwrites changed) · [n]ew files only (keeps your edits) · [s]kip · [c]laude merge (run /pipeline-sync later)"
  case "$(ask "  choose:")" in
    u) cp -R "$src/." "$dst/"; keep_base "$src" "$new" "$label"; echo "  updated to v$new";;
    n) (cd "$src" && find . -type f -print0) | while IFS= read -r -d '' rel; do
         [ -e "$dst/$rel" ] || { mkdir -p "$(dirname "$dst/$rel")"; cp "$src/$rel" "$dst/$rel"; echo "  + $rel"; }
       done
       # manifest: record base version so /pipeline-sync can 3-way merge later
       sed -i.bak "s/^base:.*/base: $new/" "$dst/manifest.yml" 2>/dev/null && rm -f "$dst/manifest.yml.bak"
       echo "  new files added; edited files kept; manifest base → $new";;
    c) mkdir -p "$dst/.pipeline-incoming"; cp -R "$src/." "$dst/.pipeline-incoming/"
       echo "  package copied to $dst/.pipeline-incoming — open claude in that repo and run: /pipeline-sync";;
    *) echo "  skipped";;
  esac
}

# ---------- project role → stacks ----------
pick_role() {
  [ -n "$ROLE" ] && return
  echo "Project role:"
  echo "  1) backend         dotnet"
  echo "  2) frontend        react"
  echo "  3) fullstack       dotnet + react"
  echo "  4) mobile-flutter  flutter"
  echo "  5) mobile-kmp      kmp"
  echo "  6) ai              python + node + react   (AI tools + POC web app)"
  echo "  Stacks available for custom: dotnet · react · node · flutter · kmp · python · python-flask · python-django"
  echo "  7) custom          pick stacks"
  case "$(ask "choose 1-7:")" in
    1) ROLE=backend;; 2) ROLE=frontend;; 3) ROLE=fullstack;; 4) ROLE=mobile-flutter;; 5) ROLE=mobile-kmp;; 6) ROLE=ai;; 7) ROLE=custom;;
    *) echo "invalid"; exit 1;; esac
}
stacks_for_role() {
  case "$ROLE" in
    backend) echo dotnet;; frontend) echo react;; fullstack) echo "dotnet react";;
    mobile-flutter) echo flutter;; mobile-kmp) echo kmp;; ai) echo "python node react";;
    custom) ask "stacks (space separated):";;
    *) echo "unknown role $ROLE"; exit 1;; esac
}

install_project() {
  local dst="$PROJECT_DIR/.claude" stacks frag
  pick_role; stacks="$(stacks_for_role)"
  echo "→ project: $PROJECT_DIR · role $ROLE · stacks: $stacks"
  if [ -e "$dst/manifest.yml" ]; then
    install_tree "$SRC_PROJECT" "$dst" "project"
  else
    mkdir -p "$dst/skills" "$dst/conventions"
    cp "$SRC_PROJECT/README.md" "$SRC_PROJECT/manifest.yml" "$dst/"
    [ -e "$dst/triage-memory.md" ] || cp "$SRC_PROJECT/triage-memory.md" "$dst/"
    for s in $stacks; do
      cp -R "$SRC_PROJECT/skills/$s-feature" "$dst/skills/"
      t="$s"; cp "$SRC_PROJECT/conventions/$t-testing.md" "$dst/conventions/"
    done
    # pipeline.yml assembled from fragments so only the chosen stacks appear
    if [ -e "$dst/pipeline.yml" ]; then echo "  pipeline.yml exists, kept"; else
      frag="$SRC_GLOBAL/templates/pipeline"
      { cat "$frag/00-header.yml"; for s in $stacks; do cat "$frag/10-stack-$s.yml"; done; cat "$frag/90-rest.yml"; } > "$dst/pipeline.yml"
    fi
    cp "$SRC_PROJECT/pipeline.local.example.yml" "$dst/"
    for ig in .claude/pipeline.local.yml .claude/settings.local.json; do
      grep -qsxF "$ig" "$PROJECT_DIR/.gitignore" 2>/dev/null || echo "$ig" >> "$PROJECT_DIR/.gitignore"
    done
    # design system only for UI stacks
    case " $stacks " in *" react "*|*" flutter "*|*" kmp "*) [ -e "$dst/design-system.md" ] || cp "$SRC_PROJECT/design-system.md" "$dst/";; esac
    keep_base "$SRC_PROJECT" "$(ver "$SRC_PROJECT")" "project"
    echo "  installed v$(ver "$SRC_PROJECT")"
  fi
  echo
  echo "Next in $PROJECT_DIR:"
  echo "  1. edit .claude/pipeline.yml — roots, build/test commands, OpenCode model, base branch"
  echo "  2. tune .claude/skills/<stack>-feature/SKILL.md and .claude/conventions/<stack>-testing.md"
  case " $stacks " in *" react "*|*" flutter "*|*" kmp "*) echo "  3. UI/UX team fills .claude/design-system.md (frontend work refuses to start without it)";; esac
  echo "  then: claude → /feature #<issue>"
}

[ "$DO_GLOBAL" = 1 ] && install_tree "$SRC_GLOBAL" "$DST_GLOBAL" "global (~/.claude)"
[ "$DO_PROJECT" = 1 ] && install_project
