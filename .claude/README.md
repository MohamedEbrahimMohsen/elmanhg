# /feature — how it works in this repo

> **Elmanhg:** this repo runs the **local** `/feature` command in `.claude/commands/feature.md` (plan → implement → review → PR → CodeRabbit → merge, every agent Opus 5.5 at medium effort). The rest of this README describes the global pipeline, which is a fallback only. Differences in this repo: no Figma (screens come from `prototype/`), CodeRabbit instead of PR-Agent, no OpenCode/Agy, gitleaks off.

Read time: 3 minutes.

## Run it

```
/feature #100      # a story → agent splits it, then runs each sub-task
/feature #101      # one sub-task (or a plain task) → runs it alone
```

## What happens

```
story ─▶ split ─▶ [you approve] ─▶ sub-issues on GitHub
                                        │
          ┌─────────────────────────────┘  per sub-task, in order
          ▼
  explore ─▶ plan ─▶ [you approve] ─▶ implement (+unit tests) ─▶ postman ─▶ style ─▶ coverage ─▶ security ─▶ e2e (Agy) ─▶ review (≤2 rounds)
          ─▶ draft PR + CI + PR-Agent ─▶ collect ─▶ triage ─▶ fix (≤2 cycles)
          ─▶ PR ready ─▶ [you review] ─▶ [a teammate approves] ─▶ squash merge ─▶ next sub-task
```

The agent stops at every `[ ]`. Nothing runs until you answer.

## Stacks: you never say which

The repo lists its stacks in `pipeline.yml` (dotnet, react, node, flutter, kmp, python, python-flask, python-django). The explorer reads the task and decides which ones it touches. A backend engineer picking up a fullstack task gets `dotnet + react` without saying anything. You see the detected stacks at the plan gate and correct them there if wrong.

Folder renamed, or not there yet? `/feature` looks for it by marker file (`*.sln`, `package.json` + react, `pubspec.yaml`, `manage.py`, …) and asks you: pick the right folder (it fixes every command in that stack's block and shows the diff), `n` = this task creates it from scratch (the plan starts with the scaffold), or `d` = remove the stack. It never picks for you.

Multi-stack task = one plan with one section per stack (backend first), OpenCode runs once per stack on the same branch, one PR.

## Tests and security: you decide, per stack and per task

Every stack in `pipeline.yml` has the same two blocks. Flip `on:` per stack; the plan gate lets you override for one task.

| Block | Switch | What it does | Default |
|---|---|---|---|
| tests | unit | writes the plan's tests, updates/deletes existing tests the change breaks | on |
| tests | postman | API stacks: request per added / changed / removed endpoint · `assert: true` adds pm.test per scenario | on (API stacks) |
| tests | e2e | starts the app, Agy opens it and walks the plan's E2E scenarios, screenshots each | off |
| tests | coverage | runs the coverage command, review fails below `min` | off |
| security | packages | vulnerable dependencies (dotnet list --vulnerable · npm audit · pip-audit · osv-scanner) | on |
| security | secrets | leaked keys and tokens (gitleaks) | on |
| security | sast | static analysis (semgrep) | off |
| security | prompt-injection | agent reviews every place untrusted text reaches an LLM prompt or tool | off |

Security findings at or above `steps.security.fail_on` (default `high`) block the review. Findings in code the PR didn't touch are listed as pre-existing and never block.

Say "no unit tests, POC" or "run e2e this time" at the plan gate. The next run is back to the defaults.

## Frontend: two hard stops

Any task that touches a `ui: true` stack:

1. `.claude/design-system.md` must exist. The UI/UX team owns it. Missing → the run stops before planning. No guessing colours from the repo.
2. You paste the Figma link(s) for the task's screens when asked. No link → stop. If there is no Figma MCP, you also export the frames as PNG into `.process/<run>/figma/`.

Every visual value in the code must be a token from the design system. A literal `#hex` or `13px` is a blocking finding.

## Who does what

| Step | Brain | Job |
|---|---|---|
| split, plan, review, triage | Opus 5.5 medium | thinks |
| explore, style check, collect | Sonnet medium | reads and lists, no thinking |
| implement, rework, fix, postman | OpenCode by default; `opus`/`sonnet` if `steps.implement.model` says so | writes every line of code |
| e2e | Agy (Antigravity) · `agy.model` | opens the app, clicks through it |
| security | commands + Opus for prompt-injection | finds vulnerabilities |
| the gates | you, then a teammate | decide |

## What the agents read

Besides your style guide, each agent reads a fixed set of vendored skills from `~/.claude/skills/` (ponytail, caveman, superpowers, gstack, ui-ux-pro-max). The map is in `~/.claude/skills/README.md`. You don't configure this per repo.

## Your own model for the implementer

Team default is OpenCode (cheap). If your subscription allows it, copy `pipeline.local.example.yml` to `pipeline.local.yml` and set `implement: { model: opus, effort: medium }`. The file is gitignored; nobody else's runs change. The orchestrator prints `local override: …` at the start so you always know.

## Change it

Edit `.claude/pipeline.yml`. That's the only file.

- Turn a step off: `on: false`
- Swap a model: `model: sonnet`
- Gates can't be turned off.
- Delete the stacks this repo doesn't have. `/feature` checks every `root`, `style`, `testing`, `build`, `test` before it starts and prints exactly what to fix.
- A solution at the repo root: `root: .`

## Where the run lives

`.process/<issue#>-<slug>/` — one folder per story and one per sub-task, committed to git.

```
.process/100-refunds/                 the story
├── 00-config.yml
├── 00-status.md          which sub-task is next · who it waits for
├── 00-split.md           the approved sub-task list, in order
└── metrics.md            totals across all sub-tasks

.process/101-add-refund-endpoint/     one sub-task
├── 00-config.yml         copy of pipeline.yml when the run started
├── 00-status.md          where the run is · who it waits for · resume point
├── 00-design.md          (UI) Figma links you gave
├── figma/                (UI) exported frames
├── 01-context.md         what the repo already has · first line: STACKS: dotnet, react
├── 02-plan.md            the plan you approved
├── 03-implementation-<stack>.md   what OpenCode did, one per stack
├── 03-postman-<stack>.md          collection changes + assertions (API stacks)
├── 04-coverage-<stack>.md         COVERAGE: n% (min m) (when on)
├── 04-style-<stack>.md            style checklist, one per stack
├── 04-security-<stack>.md         SECURITY: N findings ≥ high (when on)
├── 05-e2e-<stack>.md              E2E: P passed, F failed (when on)
├── e2e/<stack>/                   Agy screenshots
├── 05-review.md          verdict + numbered findings  (05-review-r2.md on rework)
├── 06-pr-comments.md     PR-Agent comments, verbatim
├── 07-triage.md          fix / reject / ask-you, per comment
├── 08-fix-<stack>.md     what OpenCode changed
├── 09-verify.md          fix confirmed
├── prompts/              what OpenCode was told
├── logs/                 what OpenCode printed
└── metrics.md            tokens per model · time per step · rounds · verdict
```
Second rounds and cycles add `-r2` files next to the first.

## What it remembers

`.claude/triage-memory.md`. Every PR-Agent comment pattern the triager has decided on, with the decision. Next time the same pattern shows up, same decision, no re-reading. When a pattern hits 3 (`triage.promote_at`), the report asks you to make it a style rule or a PR-Agent ignore, then delete the row. You can edit the file by hand.

## Versions

`.claude/manifest.yml` says which package version this repo runs. Edited a skill or convention? Run `bump.sh .claude "what changed"` so the version moves and the team's `/pipeline-sync` sees it. `pipeline.yml`, `triage-memory.md` and `design-system.md` are yours; they are never versioned or overwritten.

## Guards you can't turn off

- **Test integrity.** Editing, skipping, or deleting an existing test that the plan didn't list is a blocking finding. A wrong test is reported as `BLOCKED`, never "fixed" to pass.
- **BLOCKED.** Any agent can stop with `BLOCKED: <reason>` instead of faking success. You get the question; the run waits.
- **New packages** only when the plan names them, after a registry check (no guessed names).

## New project from a PRD

Write `docs/PRD.md`, then `/product docs/PRD.md`. It writes assumptions and ADRs, creates the epic and stories on GitHub (story 1 = walking skeleton: scaffold, `init.sh`, `scripts/verify.sh`, CI), and stops at a backlog gate for you. Then `/feature #<story>` as usual.

## Autopilot (POC repos only)

Off by default, and only switchable in your own `pipeline.local.yml`. When on, `plan-judge` (Opus) approves split and plan instead of you, and PRs squash-merge when CI, review, security and E2E are green. Run it with `bash ~/.claude/bin/autopilot.sh --prd docs/PRD.md`. Every automatic decision is logged under `## Autopilot decisions` in `00-status.md` and on the issue. Never on a team repo.

## Resume

Closed the terminal at a gate? Run `/feature #101` again. It reads `00-status.md` and continues from there.

## Rules that don't change

- One PR per sub-task. Never one PR per function. A big task is a big PR, that's fine.
- Next sub-task starts only after the previous PR is merged.
- The agent never edits a `.process/` file you approved. It writes the next one.
- Git must be clean before it starts.
