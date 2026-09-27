# Feature pipeline — install

    ./install.sh                     # bash · Git Bash on Windows
    .\install.ps1                    # PowerShell

One run does both: `~/.claude` (the engine, once per machine) and `<repo>/.claude` (config, committed). It asks for the project role — backend · frontend · fullstack · mobile-flutter · mobile-kmp · ai · custom — and copies only that role's stacks, skills, and conventions. `pipeline.yml` is assembled with only those stacks.

Already installed? It shows the version on each side and every file that differs (`changed` = edited locally, `missing` = new in the package, `extra` = only on your side) and asks: update all · new files only · skip · or hand it to Claude (`/pipeline-sync`) for a 3-way merge that keeps your edits.

## Versions

Both `.claude` trees carry a `manifest.yml`: `version`, `base` (the package version this was installed from), a hash per file, and a `changes` log. **Any edit to any file under a `.claude` tree must bump the version**:

    ./bump.sh global  "reviewer: check Postman assertions"      # package side, before you share it
    ./bump.sh ~/.claude "my tweak"                              # an installed tree, after a local edit

Patch bumps for edits, `--minor` for new steps or agents, `--major` for a config format change. Two people bumping the same file to the same number is fine: hashes tell `/pipeline-sync` which files actually differ, the version only says "look".

Then in the repo:

1. Edit `.claude/pipeline.yml` — delete the stacks this repo doesn't have, fix roots and commands, OpenCode model, base branch.
2. Tune `.claude/skills/<stack>-feature/SKILL.md` for each stack you kept. All eight are team defaults with the same section numbering (§1 layout … §6 DO/DON'T).
3. Fill `.claude/conventions/<stack>-testing.md` for each stack you kept.
4. UI repos: have the UI/UX team fill `.claude/design-system.md`. Frontend work refuses to start without it.

Tools per machine, only for switches you turn on:

| Switch | Needs |
|---|---|
| `security.secrets` | `gitleaks` |
| `security.sast` | `semgrep` |
| `security.packages` | built-in for dotnet / npm · `pip-audit` for Python · `osv-scanner` for Flutter / KMP |
| `tests.e2e` | `agy` (Antigravity CLI) · model in `agy.model` · runs with `--dangerously-skip-permissions`: local / test environments only |

`/feature` checks these at start and names the missing one.
5. `.process/` is committed on purpose.
6. GitHub: branch protection on the base branch, 1 required approval. PR-Agent enabled.

Dev reads `<repo>/.claude/README.md` (3 min). Runs `/feature #<issue>`.

Files:

    GUIDE.md · GUIDE.html            the dev guide (same content, two formats)
    DECISIONS.md                     why the pipeline is shaped this way (models, gates, guards, autopilot)
    research/                        the sources behind the skills and agents (6 files)
    FLOW.html                        the full flow diagram + who pays for what
    install.sh · install.ps1          installer (role → stacks; diff + ask on existing installs)
    bump.sh · bump.ps1                bump version + hashes + changelog after any edit

    global/.claude/
    ├── manifest.yml                 version · base · file hashes · changes
    ├── commands/feature.md          the orchestrator
    ├── commands/pipeline-sync.md    3-way merge of a newer package with your local edits
    ├── commands/product.md          PRD → assumptions + ADRs + GitHub epic/stories (story 1 = walking skeleton)
    ├── bin/autopilot.sh · .ps1      POC only: unattended runner, one fresh claude process per sub-task
    ├── settings.autopilot.example.json   deny list for unattended runs → copy to <repo>/.claude/settings.local.json
    ├── agents/
    │   ├── story-splitter.md        opus
    │   ├── repo-explorer.md         sonnet
    │   ├── planner.md               opus
    │   ├── style-checker.md         sonnet
    │   ├── reviewer.md              opus (also verify mode)
    │   ├── security-reviewer.md     opus · reads scan output · prompt-injection pass
    │   ├── plan-judge.md            opus · autopilot only: approves split / plan / backlog instead of the dev
    │   ├── comment-collector.md     sonnet
    │   ├── triager.md               opus
    │   └── implementer.md           Claude implementer · used when steps.implement.model is opus/sonnet
    ├── skills/                      vendored third-party skills · see skills/README.md for who uses what
    │   └── momenta-*/               ours: api-contract · dependency-policy · greenfield-bootstrap
    └── templates/
        ├── pipeline/                pipeline.yml fragments: header · one per stack · rest — the installer assembles them
        ├── status.md                run status / resume point
        ├── metrics.md               run log
        ├── implement-code.md        prompt for the implementer (OpenCode or Claude, same file)
        ├── implement-rework.md
        ├── implement-postman.md      collection sync + pm.test assertions
        ├── implement-fix.md
        └── e2e-agy.md               prompt for Agy: walk the plan's E2E scenarios, screenshot each

    project/.claude/
    ├── manifest.yml                 version · base · file hashes · changes
    ├── pipeline.yml                 THE config · every stack: tests + security switches · agy · steps
    ├── pipeline.local.example.yml   personal overrides (copy to pipeline.local.yml · gitignored)
    ├── README.md                    for the dev
    ├── design-system.md             tokens + components · owned by UI/UX · required for UI stacks
    ├── triage-memory.md             starts empty · triager appends
    ├── skills/
    │   ├── dotnet-feature/SKILL.md  team default
    │   ├── react-feature/SKILL.md   team default
    │   ├── python-flask-feature/SKILL.md · python-django-feature/SKILL.md   team defaults
    │   ├── node-feature/SKILL.md    team default
    │   ├── flutter-feature/SKILL.md team default
    │   ├── kmp-feature/SKILL.md     team default
    │   └── python-feature/SKILL.md team default
    └── conventions/<stack>-testing.md  stubs — fill

## Autopilot (POC only)

Human gates stay the default. Autopilot is a personal switch for throwaway POC repos: write `docs/PRD.md`, start it, come back to merged PRs.

1. `.claude/pipeline.local.yml` (gitignored): copy the commented `autopilot:` block from `pipeline.local.example.yml`, set `on: true` (keys: `judge`, `hours`, `max_attempts`). An `autopilot:` block in `pipeline.yml` is ignored.
2. Trim `.claude/pipeline.yml` to the POC's stacks. UI stacks still need `.claude/design-system.md`.
3. Permissions: `cp ~/.claude/settings.autopilot.example.json <repo>/.claude/settings.local.json` (the installer already gitignores it; the runner refuses a dirty tree). Its `deny` list holds even in `bypassPermissions` mode: no force-push, no `reset --hard`, no `gh pr merge --admin`, no repo/secret deletion, no reading `.env` / keys. `allow` only matters when you run without bypass. OpenCode does not read it — keep `opencode.json` free of `"ask"` rules and deny `git push*` / `gh *` there.
4. Run in a disposable VM or container, with a repo-scoped token (PAT or GitHub App; `GITHUB_TOKEN` pushes do not trigger CI):

       bash ~/.claude/bin/autopilot.sh --prd docs/PRD.md      # /product → epic + stories, then builds them
       bash ~/.claude/bin/autopilot.sh --epic 12              # existing epic
       pwsh ~/.claude/bin/autopilot.ps1 -Epic 12              # Windows (needs Git Bash for scripts/verify.sh)

   One fresh `claude -p` per sub-task. `plan-judge` (opus) replaces you at split/plan gates; PRs squash-merge when checks are green; a red main is reverted by PR. Never stops on a failure: failed tasks are skipped, a red main gets a "repair main" task, a usage limit waits and resumes. Ends on: all done · `hours` (72) · nothing runnable left · main unrepairable after `max_attempts` tries.
5. Read `.process/autopilot/REPORT.md` and `ledger.md`. Blocked issues carry the label `autopilot:blocked` and a comment with the reason; every automatic decision is under `## Autopilot decisions` in the task's `00-status.md`.

New files: `commands/product.md` · `agents/plan-judge.md` · `bin/autopilot.sh` · `bin/autopilot.ps1` · `settings.autopilot.example.json` · `skills/momenta-greenfield-bootstrap/SKILL.md`.
