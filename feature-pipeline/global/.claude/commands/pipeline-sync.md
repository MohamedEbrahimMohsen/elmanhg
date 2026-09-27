---
description: Compare the installed pipeline (~/.claude and this repo's .claude) with a newer package, show every difference, and merge with the dev's decisions. Never overwrites silently.
argument-hint: "[path to the new package, default .claude/.pipeline-incoming or ~/.claude/.pipeline-incoming]"
allowed-tools: Read, Write, Edit, Bash, Grep, Glob
model: opus
---

Sync the feature pipeline package. You compare and merge; the dev decides every conflict.

## Skills — read once
- `~/.claude/skills/caveman/SKILL.md` — every message terse.
- `~/.claude/skills/i-have-adhd/SKILL.md` — one decision at a time, state restated.

## 1. Locate the three versions

For each side (`global` = `~/.claude`, `project` = `<repo>/.claude`):

- **Installed**: the live tree. Its `manifest.yml` has `version`, `base`, `files` (path → hash), `changes`.
- **Incoming**: `$ARGUMENTS`, else `<side>/.pipeline-incoming/`, else `~/.claude/templates/` for the project side. No incoming → say so, stop.
- **Base**: the pristine package the installed tree started from. `~/.claude/.pipeline-base/<base-version>/<side>/` if the installer kept it; otherwise reconstruct from the installed manifest's `files` hashes: a file whose hash still matches its manifest entry is unchanged since base, so its installed content **is** the base content.

Print one line per side: `global: installed 1.0.3 (base 1.0.0) · incoming 1.2.0`.

## 2. Classify every file (skip `pipeline.yml`, `pipeline.local.yml`, `triage-memory.md`, `design-system.md`, `manifest.yml`)

| Installed vs base | Incoming vs base | Class | Action |
|---|---|---|---|
| same | same | unchanged | nothing |
| same | changed | **upstream** | take incoming, no question |
| changed | same | **local** | keep, no question |
| changed | changed | **conflict** | 3-way merge, dev decides |
| missing | present | **new** | add |
| present | missing | **removed upstream** | ask: delete or keep |

Print the table with counts, then the file list per class. Nothing is written yet.

## 3. Conflicts, one at a time

For each conflict: show the base→installed diff and the base→incoming diff side by side, at most 40 lines each (hunk headers + changed lines). Then propose a merged version when the hunks do not overlap, or the two variants when they do. Ask: `[m]erged as proposed · [l]ocal · [i]ncoming · [e]dit (you type the result)`. **Wait.** Write the chosen result. Next conflict.

Never resolve a conflict by picking one side yourself, even when one looks obviously better.

## 4. Removed upstream

List them. Ask once for the whole list: `[d]elete all · [k]eep all · [p]ick`. Delete only what the dev says.

## 5. Write the manifest

Update `manifest.yml`: `version` = incoming version with the installed patch carried as a suffix if local edits remain (`1.2.0` when clean, `1.2.0+local.2` when two local-only files remain), `base` = incoming version, `files` = fresh hashes, append one `changes` row: `synced from <incoming>, <n> conflicts resolved, <m> local kept`. Then remove `.pipeline-incoming/`.

## 6. Report

- Per side: version before → after.
- Upstream taken / local kept / conflicts resolved (with the choice per file) / new / removed.
- Files still local-only, so the dev can send them upstream: "these differ from the package; run `bump.sh` and share the diff with the team if they should be the default."

## Never
- Never touch `pipeline.yml`, `triage-memory.md`, `design-system.md`. Those are the repo's, not the package's.
- Never delete a file the dev did not confirm.
- Never write before §3 is finished.
