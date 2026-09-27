# 01 — Designing a reliable 24-hour autonomous coding harness (AUTOPILOT)

Scope: unattended PRD → GitHub epic/stories/sub-issues → per-sub-task plan → implement → checks → review → PR → auto-merge → next, for POC web apps. Everything below is phrased as rules for the harness and its agents. Numbers are defaults; tune after the first 2–3 runs.

Doc facts were checked against Claude Code docs as of Sept 2026 (v2.1.2xx). Re-check the flags against your installed version with `claude --help`.

---

## 0. Core design decisions (non-negotiable)

1. **A dumb bash/Python supervisor owns the 24h loop. No long-lived LLM session does.** Every sub-task step runs in a fresh `claude -p` (or `opencode run`) process. Each process gets its state from files, not from conversation history. Anthropic's long-running harness, Ralph, Factory Missions, Codex's 25h run and Cursor's planner/worker system all converge on fresh context per unit of work plus external state. Cursor still needed "periodic fresh starts to combat drift and tunnel vision". Amp dropped compaction because stacking "summary on top of summary" degrades quality.
2. **Keep the control flow deterministic, and use agents only inside steps.** The pipeline order (split → plan → judge → implement → check → review → PR → merge) is a workflow in code. Per "Building effective agents", use workflows where the path is known and agents where it isn't. The LLM never decides whether to skip a gate.
3. **Completion is decided by the harness running checks. The agent claiming "done" counts for nothing.** "Claude stops when the work looks done". Without an executable check, "looks done" is the only signal (Claude Code best practices). The runner re-executes `scripts/verify.sh` itself after every implementer exit.
4. **One sub-task = one branch = one PR = one squash commit on main.** Git is the checkpoint and rollback mechanism, and the handoff channel (Factory: "coordinates handoffs through git").
5. **Main must always be green.** A red main stops all feature work until it is fixed or reverted (C-compiler post: CI with "stricter enforcement … so that new commits can't break existing code").
6. **Every limit gets a hard cap:** turns, dollars, wall-clock, retries and review rounds. The agent is never trusted to stop on its own.

---

## 1. Runner script (supervisor)

### 1.1 Process model
- Run the supervisor under `systemd` (Restart=always) or `tmux` plus a cron watchdog, inside a disposable container or VM. Write `.process/_autopilot/heartbeat` (ISO timestamp) every 60 s. The watchdog kills and restarts the supervisor if the heartbeat is older than 15 min. The supervisor must be **idempotent on restart**: it reads state and resumes (see §8).
- Per step, spawn:
  ```bash
  timeout --signal=INT --kill-after=120 "${STEP_TIMEOUT:-45m}" \
    claude -p "$(cat "$PROMPT_FILE")" \
      --model "$MODEL" --fallback-model sonnet \
      --permission-mode bypassPermissions \
      --settings .autopilot/settings.autopilot.json \
      --strict-mcp-config --mcp-config .autopilot/mcp.json \
      --max-turns "$MAX_TURNS" --max-budget-usd "$STEP_BUDGET" \
      --output-format stream-json --verbose \
      --name "ap-${ISSUE}-${STEP}" \
      > ".process/${TASK_DIR}/logs/${STEP}-$(date +%s).jsonl" 2>&1
  ```
  - Send **SIGINT, not SIGTERM**. SIGTERM exits 143 and "records no result" for the in-progress turn. SIGINT ends the turn cleanly. `timeout --signal=INT --kill-after` escalates only if needed.
  - **Do not use `--bare`** for pipeline steps. It skips `.claude/agents`, commands, hooks and CLAUDE.md, which the pipeline depends on. Use `--settings` + `--strict-mcp-config` instead, so a teammate's `~/.claude` can't change the behaviour. Use `--bare` only for pure judge/classifier calls that need no project context (with `--json-schema`).
  - Put `--json-schema` on every step whose output the runner parses (judge verdict, triage result, story split). Read `.structured_output`. Never regex free text.
- Environment for every call:
  ```
  CLAUDE_CODE_RETRY_WATCHDOG=1          # retry 429/529 indefinitely w/ backoff ≤5 min (fails fast on spend-limit 429)
  API_TIMEOUT_MS=900000
  BASH_DEFAULT_TIMEOUT_MS=300000         # default 120000 is too short for e2e/build
  BASH_MAX_TIMEOUT_MS=1200000
  CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS=900000
  CLAUDE_AUTOCOMPACT_PCT_OVERRIDE=70     # compact earlier if a step does run long
  ```

### 1.2 Parse every result, then branch
Read the last `{"type":"result"}` line of the stream:
| Field | Action |
|---|---|
| `subtype=="success"` and `is_error==false` | Run the harness-side verification (§5.6). The step counts as done only if that passes. |
| `subtype=="error_max_turns"` | Count as a failed attempt. Resume once with `--resume $session_id` and "summarize state to progress file and stop". Then start fresh. |
| `subtype=="error_max_budget_usd"` | Failed attempt. Do not resume. Escalate the model or split the task. |
| `subtype=="error_during_execution"` / exit≠0 | Transient: retry with backoff. After 3 in a row: circuit-break (§8). |
| `permission_denials` non-empty | Log to `NN-denials.md`. Two or more denials of the same pattern means the prompt or the allowlist is wrong. Mark the task blocked. Do not loop. |
| `total_cost_usd` | Add to the run ledger `.process/_autopilot/budget.json`. |
Also watch `system/api_retry` events. If `error` is `billing_error`/`rate_limit` with a spend-limit message, **pause the whole run** until reset. Don't burn retries.

### 1.3 Stuck/loop detection (harness-side, from the stream-json)
Hash `(tool_name, normalized tool_input)` and `(tool_result first 500 chars)`. Kill the step (SIGINT) and count a failed attempt when any of these hold. Thresholds come from OpenHands' StuckDetector:
- same action → same observation **4×**
- same action → error **3×**
- A/B alternating pair **6 cycles**
- **3+** assistant messages with no tool calls (monologue)
- no file change and no new test result for **15 min** of wall-clock
- same failing-test signature (test id + first error line) after **3** fix attempts → stop. Record it in `NN-blockers.md` and escalate (§5.7).
- Feed time explicitly. "Claude can't tell time" (C-compiler post), so prepend to every prompt: `Elapsed: 6h12m / 24h. Run budget used: $182/$600. This step: attempt 2 of 3, budget $8.`

### 1.4 Budgets (set explicitly; defaults for a 24h POC)
Reference points: Anthropic's full 3-agent app harness cost $124–$200 for 4–6 h. Factory Missions use ~12× a normal session's tokens (~45K tokens/min). Codex's 25h run used ~13M tokens. Multi-agent systems use ~15× chat tokens.
| Cap | Default |
|---|---|
| Run hard cap | `$RUN_BUDGET` (e.g. $600). Alert at 50/80%. At 100%: finish the current merge, then stop. |
| Per-step `--max-budget-usd` | split $5, plan $4, judge $1, implement $12, review $3, triage $1 |
| Per-step `--max-turns` | plan 40, implement 120, review 30, judge 10 |
| Per-step wall clock | plan 15 m, implement 45 m, review 15 m, CI wait 30 m |
| Attempts per sub-task | 3 implement attempts (model ladder §5.7), 2 review-fix rounds, 2 plan revisions |
| Consecutive failed sub-tasks | 3 → run-level circuit breaker (pause, re-plan, or stop) |

### 1.5 Logging for post-mortem
- Keep the raw `stream-json` per step (use `--forward-subagent-text` to capture subagent transcripts).
- Append one JSON line per step to `.process/_autopilot/runlog.jsonl`: `{ts, issue, step, attempt, model, session_id, subtype, cost, turns, duration_s, verdict, sha}`.
- Test and lint output must be greppable. Print `ERROR <reason>` on one line and use quiet reporters. "The test harness should not print thousands of useless bytes" (C-compiler post). Pipe long output to a file and give the agent `tail -50` plus the file path.

---

## 2. Sandbox, permissions and hooks (set once, before any agent runs)

### 2.1 Isolation
- Run in a throwaway container/VM with **no production credentials**, an egress allowlist (package registries, github.com, api.anthropic.com, the model provider for OpenCode) and a non-root user. Ralph playbook: "It's not if it gets popped, it's when. And what is the blast radius?"
- GitHub identity: a **GitHub App installation token** (or fine-grained PAT) scoped to this one repo with `contents`, `pull_requests`, `issues` and `checks:read`. Grant no `administration` and no org scope. A PAT or App token is also *required* for CI to trigger. PRs and pushes made with `GITHUB_TOKEN` "will not create a new workflow run".
- In bypass mode, deny rules still apply ("Deny rules block in every mode, including `bypassPermissions`"). Allow rules have no effect in bypass. If you prefer `--permission-mode auto --permission-prompts none` on a less-disposable VM, know that in `-p` repeated classifier blocks **don't stop the run**: "the action doesn't run and Claude keeps working". Auto mode pauses after 3 consecutive or 20 total blocks, and still has a 17% false-negative rate on real overeager actions. The harness must watch `permission_denials`.

### 2.2 `.autopilot/settings.autopilot.json` (deny list; minimum)
```json
{
  "permissions": {
    "deny": [
      "Bash(git push --force*)", "Bash(git push -f*)", "Bash(git push origin main*)",
      "Bash(git push origin HEAD:main*)", "Bash(git reset --hard origin*)",
      "Bash(gh pr merge*)", "Bash(gh repo delete*)", "Bash(gh api -X DELETE*)",
      "Bash(gh api --method DELETE*)", "Bash(gh secret*)", "Bash(gh auth*)",
      "Bash(rm -rf /*)", "Bash(rm -rf ~*)", "Bash(curl * | sh*)", "Bash(sudo *)",
      "Read(./.env)", "Read(./.env.*)", "Edit(./.env*)",
      "Edit(./.github/workflows/**)", "Edit(./.autopilot/**)", "Edit(./.claude/**)",
      "Edit(./scripts/verify.sh)", "Edit(./features.json)"
    ]
  },
  "env": { "BASH_DEFAULT_TIMEOUT_MS": "300000" }
}
```
- Only the **runner** runs `gh pr merge`, edits `features.json` pass flags and touches workflows. Agents never do.
- Tests: add path-scoped denies per step. The implementer may create new test files, but `Edit` on existing test files outside the plan's `allowed_test_files` is blocked by a PreToolUse hook (§2.3). ImpossibleBench: Claude models cheat mostly by **modifying tests (>79% of their cheating)**. **Read-only test access** was the strongest mitigation.

### 2.3 Hooks (`.claude/settings.json`, committed)
- **PreToolUse `Edit|Write`** → `guard-paths.sh`. Deny (JSON `permissionDecision:"deny"` + reason) when the path is not in the current task's `plan.files ∪ plan.new_files ∪ plan.allowed_test_files`. The current task comes from `.process/_autopilot/current.json`. This mechanically enforces scope creep limits.
- **PreToolUse `Bash`** with `if: "Bash(npm install *)"` (and pnpm/yarn/dotnet add/pip install equivalents) → allow only packages listed in `plan.dependencies`, pinned. Everything else is denied with the reason "add the dependency to the plan; the planner owns deps".
- **Stop hook** → `verify-gate.sh`. It runs the fast verify (`scripts/verify.sh --fast`). On failure it outputs `{"decision":"block","reason":"verify failed: <first 20 ERROR lines>"}`. It must read `stop_hook_active` and exit 0 when true, **and** Claude Code overrides a Stop hook after it "blocks eight times in a row without progress". Budget for that: the runner's own verify (§5.6) is the real gate, and the Stop hook is a cheap inner loop.
- **SessionStart** → prints the task card (current.json, last 20 lines of progress.md, `git log --oneline -10`, remaining budget/time) as additional context. The agent is always oriented, even after compaction.
- **PostToolUse `Edit|Write`** → run the formatter/linter on the touched file and return errors as `additionalContext`. SWE-agent: rejecting or flagging syntax-broken edits immediately raised the solve rate from 15.0% to 18.0% and prevented "repeatedly edit the same code snippet" loops. Aider lints after every edit by default.

### 2.4 OpenCode as implementer
- Use `opencode run --model <provider/model> --format json "<prompt>"`, wrapped in the same `timeout --signal=INT`.
- `opencode.json` must contain **no `"ask"` rules**. An unanswered ask in an unattended run hangs or fails. Set explicit `allow`/`deny`: `"bash": {"*":"allow","git push*":"deny","gh *":"deny","rm -rf *":"deny"}`, `"edit": {"*":"allow",".github/**":"deny",".env*":"deny"}`. Also set `doom_loop` and `external_directory` explicitly (both default to `ask`). The last matching rule wins, so put the specific denies after the wildcards. Use the auto-approve flag only if your version has one, and still keep the denies.
- OpenCode doesn't run Claude hooks, so the runner does all guarding post-hoc: diff path check, test-integrity check and dependency check (§5.5) run **before** a PR can be opened.

---

## 3. Greenfield bootstrap (Step 0, before any PRD feature)

Do these in order, one PR each, each merged green before the next. The walking skeleton is "a tiny implementation of the system that performs a small end-to-end function" linking all main components (Cockburn). Integration problems surface in hour 1, not hour 20.

1. **Repo + settings** (runner, via `gh api`):
   ```bash
   gh repo create "$ORG/$NAME" --private --template "$TEMPLATE" --clone
   gh api -X PATCH repos/$ORG/$NAME -f allow_auto_merge=true -f delete_branch_on_merge=true \
     -F allow_squash_merge=true -F allow_merge_commit=false -F allow_rebase_merge=false
   ```
2. **Operational docs.** `CLAUDE.md`/`AGENTS.md` ≤ ~100 lines as a *map*: build/test/lint/run commands, directory layout, and pointers to `docs/`. OpenAI: ~100-line AGENTS.md "serves primarily as a map". Ralph: keep it "operational only", ~60 lines, because "a bloated AGENTS.md pollutes every future loop's context". Also commit `.gitignore`, `.editorconfig`, toolchain pins (`.nvmrc`/`global.json`/`.tool-versions`), committed lockfile and `.env.example`.
3. **`docker-compose.yml`** for DB and other backing services. Give every service a `healthcheck` and use `depends_on: {condition: service_healthy}`. Omit host ports (`ports: ["5432"]`) so Docker picks an ephemeral port, and discover it with `docker compose port db 5432`. Set `COMPOSE_PROJECT_NAME=ap-${TASK_ID}` per worktree. This prevents port conflicts and cross-task state bleed.
4. **`init.sh`** (idempotent, ≤ 2 min, exit≠0 on any failure): copy `.env.example`→`.env` with random dev secrets if missing; install deps from the lockfile (`npm ci`); `docker compose up -d --wait`; run migrations; seed deterministic fixture data; start the app on `PORT=$(free_port)` in the background with a PID file; `curl -fsS localhost:$PORT/health`; run one smoke e2e test. Anthropic's harness makes the initializer write `init.sh`, and every coding session starts by running it.
5. **Walking skeleton:** `/health` endpoint that queries the DB, one UI page that calls the API and renders the result, one Playwright e2e test that asserts the rendered value. Unit tests alone are insufficient. Anthropic's agents "marked features done without testing" until they were forced to test "as a human user would" with browser automation.
6. **CI workflow** (`.github/workflows/ci.yml`), triggers `pull_request`, `merge_group`, `push: branches [main]`. Jobs, all with stable names: `lint`, `typecheck`, `unit`, `build`, `e2e-smoke`, `test-integrity`, `secrets-scan` (gitleaks). **No `paths:` filters on required workflows.** Path-filtered skipped workflows leave required checks "Pending" forever. Use job-level `if:` instead (conditionally skipped jobs count as success). A merge queue needs the `merge_group` trigger or checks never report.
7. **Branch ruleset on `main`**, applied only after CI has run once so the check names exist: require PR, required checks = the jobs above, require linear history, block force-push and deletion, **no bypass actors**, required approvals = 0 (a bot can't approve its own PR, and the gates are the checks). Turn on secret-scanning push protection.
8. **PR-Agent workflow.** Set `github_action_config.auto_review/auto_describe/auto_improve: "true"`, `pr_actions: ["opened","reopened","ready_for_review","synchronize"]` (webhook action types, not tool names) and `if: github.event.sender.type != 'Bot'` guards. Since the autopilot's PRs come from an App, adjust that guard or PR-Agent will skip them. `config.fallback_models` must be another model from the same provider.
9. **`features.json`** generated from the PRD (§4.2). Commit it with every `passes: false`.

Rule: if any bootstrap step fails 3×, stop the run. A POC without a working skeleton won't converge.

---

## 4. Orchestrator (PRD → GitHub → queue)

### 4.1 Split
- Input: `PRD.md`. The story-splitter produces JSON (use `--json-schema`): epics → stories → sub-tasks. Each sub-task has `id`, `title`, `acceptance_criteria[]` (observable behaviour: "GET /api/todos returns 200 with [] on empty DB"), `depends_on[]`, `size` (S/M; L is forbidden and must be split further), `touches[]` (areas) and `e2e_steps[]`.
- Sizing: a sub-task must fit **one fresh context and one PR**. Target ≤ ~400 changed LOC and ≤ ~10 files. Anthropic's harness restricts to **one feature per session**. Ralph: "one item per loop". Anthropic's planner expanded a 1–4 sentence prompt into 10–16 features. A 24h POC should land roughly 25–60 sub-tasks. More than 80 means over-splitting, and the per-PR overhead (CI, review) will dominate.
- Auto-gate the split with the judge (§6): all PRD requirements are mapped to ≥1 sub-task, there are no cycles in `depends_on`, and every sub-task has ≥1 e2e-verifiable criterion.
- Creation must be idempotent. Put a hidden marker in each issue body, `<!-- autopilot:id=S3.2 run=<run_id> -->`. Before creating, `gh issue list --search "autopilot:id=S3.2 in:body"`. Use sub-issues/labels (`autopilot`, `epic:<n>`, `status:queued|in-progress|blocked|done`).

### 4.2 `features.json` (the source of truth for "done")
```json
[{"id":"S3.2","issue":123,"category":"functional",
  "description":"User can mark a todo complete",
  "steps":["Open /","Create todo 'x'","Click checkbox","Reload","Checkbox still checked"],
  "e2e_test":"e2e/todos-complete.spec.ts",
  "depends_on":["S3.1"],"passes":false,"attempts":0,"status":"queued"}]
```
Use JSON rather than Markdown. "The model is less likely to inappropriately change or overwrite JSON files." Only the runner flips `passes`, and only after the feature's e2e test passes on main. Agents are denied edits to this file (§2.2). The rule to include in prompts: "It is unacceptable to remove or edit tests because this could lead to missing or buggy functionality."

### 4.3 Scheduling
- Pick the next sub-task as `status==queued && all(depends_on).passes`, ordered by epic priority, then id.
- **Default to sequential** (one sub-task in flight). Parallelize only sub-tasks with disjoint `touches[]`, at most 2–3, each in its own worktree plus compose project, and only with a merge queue. Cursor's lesson: flat parallel agents with locking ran at the throughput of 2–3 agents and became "risk-averse". Factory uses "sequential execution with targeted parallelization".
- Every **5 merged sub-tasks** (or at each epic end), run a **milestone validation** step. A fresh evaluator runs the full e2e suite and clicks through the app with Playwright, then files follow-up sub-tasks for regressions ahead of new work. This is Factory's "every milestone ends with a validation phase" and Anthropic's evaluator agent.
- Re-plan trigger: 3 consecutive blocked sub-tasks, or a milestone validation producing more than 5 follow-ups. The planner then regenerates the remaining queue. The plan is disposable. Ralph: "regenerate rather than salvage".

### 4.4 Orchestrator context hygiene
- The Opus orchestrator does not live 24h. The runner invokes `/feature --autopilot --task S3.2` per sub-task, and the command reads `00-status.md` to know the step.
- Subagents return ≤ 1–2K-token summaries and write full artifacts to numbered files (context engineering post: "condensed, distilled summary … often 1,000-2,000 tokens"; multi-agent post: store outputs in the filesystem, not the lead's context).
- Give each subagent brief an objective, an output format, tools/sources and explicit boundaries (multi-agent post).

---

## 5. Planner + implementer

### 5.1 Plan file (`.process/<issue>-<slug>/03-plan.md`) required sections
`Goal` · `Acceptance criteria → test mapping` (table AC-id → test file/name) · `Files to modify` (exact paths, verified to exist) · `New files` · `allowed_test_files` · `Dependencies` (name@exact-version + reason, or "none") · `Out of scope` · `Steps` (numbered, each ending in a runnable check) · `Verification commands` (exact) · `Risks`. The ExecPlan pattern adds living sections the implementer appends to: `Progress` (checkbox + UTC timestamp), `Surprises & Discoveries` (Observation/Evidence), `Decision Log` (Decision/Rationale/Date). Plans must be "fully self-contained" and "produce a demonstrably working behavior, not merely code changes".
- The planner is read-only (`--permission-mode plan` or tools `Read,Grep,Glob`, plus the Sonnet repo-explorer). It must **search before asserting something is missing**. Ralph's recurring failure is duplicate implementations. Sign: "Before making changes search codebase (don't assume not implemented)".
- The runner emits a machine-readable `03-plan.json` (files, new_files, allowed_test_files, dependencies) that the hooks consume.

### 5.2 Implementer session protocol (put verbatim in the implement prompt)
1. `pwd`; `git status` must be clean; `git fetch && git switch -C ap/S3.2 origin/main`.
2. Read `00-status.md`, `03-plan.md`, the last 30 lines of `.process/_autopilot/progress.md` and `git log --oneline -15`.
3. Run `./init.sh`. **If smoke fails on fresh main, stop and write `BLOCKED: main-red` to `05-impl-notes.md`. Do not start the feature.** (Anthropic: start sessions by running init.sh and the basic e2e test before new work.)
4. Write or extend the failing test(s) for each AC first and run them to confirm they fail.
5. Implement plan steps in order. After each step, run its check, tick `Progress`, and commit locally (`git commit -m "S3.2: step 2 – add PATCH /todos/:id"`).
6. Run `scripts/verify.sh` (lint, typecheck, unit, build, e2e for touched features). Paste the **command and its tail output** into `05-impl-notes.md` as evidence. Evidence counts, assertions don't.
7. Append 3–5 lines to `progress.md`: what was done, what's next, gotchas. Put new operational learnings (a command, a quirk) in AGENTS.md, briefly.
8. Stop. Do not push and do not open a PR. The runner does both.

### 5.3 Anti-cheating rules (prompt + mechanical)
Prompt text:
- "Do not modify, skip, delete or weaken existing tests or assertions. Do not special-case test inputs. Do not add `.skip`, `.only`, `xit`, `@Disabled`, `[Ignore]`, `|| true`, `process.exit(0)` or `sys.exit(0)`."
- "Placeholders, stubs and TODO implementations are failures. Implement fully." (Ralph: "DO NOT IMPLEMENT PLACEHOLDER OR SIMPLE IMPLEMENTATIONS")
- **Give an explicit abort path:** "If the task is impossible as specified or a test contradicts the acceptance criteria, stop and write `BLOCKED: <reason + evidence>` to 05-impl-notes.md. Blocking is an acceptable outcome; cheating is not." ImpossibleBench: an abort option cut GPT-5 cheating from 54% to 9%. A strict "stop on flawed tests" prompt cut it from 92% to 1%.

Mechanical (`test-integrity` CI job plus a runner pre-PR check):
- `git diff origin/main --numstat -- '**/*.test.*' '**/*.spec.*' tests/ e2e/`: no deletions in pre-existing test files unless the file is in `allowed_test_files`.
- The count of test cases must not drop. Compare `--listTests`/`--list` output before and after.
- Grep added lines for skip/only/ignore markers, `exit(0)`, catch-all try/except around asserts, and `== ANY`/`__eq__` overrides in test helpers.
- Grep the diff for `TODO|FIXME|NotImplemented|throw new Error\("not implemented` in non-test code. Fail on any hit.
- Coverage on changed lines ≥ 70% (POC threshold).

### 5.4 Scope-creep and dependency control
- Changed files must be a subset of `plan.files ∪ plan.new_files ∪ allowed_test_files ∪ {lockfile, progress.md, AGENTS.md}`. Anything else means reject: revert those files and give the implementer one retry.
- Diff above 600 changed LOC (excluding lockfiles and generated files) means fail and return to the splitter to split.
- New dependencies only via the plan, with exact version pins. Run `npm ci`/`dotnet restore --locked-mode` in CI so an unlocked dependency fails.

### 5.5 Pre-PR runner checks (run by the harness, not the agent)
In order, stopping at the first failure: clean tree → scope check (§5.4) → test-integrity → secrets scan (`gitleaks detect --no-git --source .`) → `scripts/verify.sh` (full). Only then run the style-checker/security-reviewer/reviewer subagents (§7).

### 5.6 Harness-side verification (the real "done")
After each implementer exit, the runner itself runs `./init.sh && scripts/verify.sh` in a clean checkout of the branch. Exit 0 plus an evidence file present means the implementation is accepted. Anything else is a failed attempt, whatever the agent said.

### 5.7 Attempt ladder
| Attempt | Implementer | Context |
|---|---|---|
| 1 | OpenCode + cheap model | plan + task card |
| 2 | OpenCode + cheap model, fresh session | plan + task card + `NN-blockers.md` (failing test signature, what was tried) |
| 3 | Claude Sonnet, fresh | same + diff of attempt 2 as "an approach that did not work" |
| 4 (optional) | Claude Opus, fresh | same |
| then | mark `blocked`, label the issue, continue with independent tasks | |
- Between attempts: `git reset --hard origin/main` on a new branch. **Never continue a polluted session.** Best practices: after two failed corrections "a clean session with a better prompt almost always outperforms". Devin: "Starting fresh with a new agent and all of the instructions up front can often get to success much faster."
- Don't debug by retrying blindly. On attempt 2+, the prompt asks first for "3 most probable root causes with evidence, then fix the most likely". Devin notes agents struggle with complex debugging, and asking for probable causes helps.

---

## 6. LLM judge for auto-approving splits and plans (replaces the human gate)

### 6.1 Judge setup
- Use a separate process with a fresh context and `--json-schema`. It sees only the PRD excerpt, the story/sub-task, the plan and a repo file listing, never the planner's reasoning. The planner and judge are different prompts and preferably different models (e.g. Opus plans, Sonnet judges, or a different vendor via OpenCode) to reduce self-preference bias.
- Output: a **binary checklist plus an overall 0.0–1.0 score and a pass/fail**. A single call producing per-criterion 0–1 scores and a pass/fail was "the most consistent and aligned with human judgements" (multi-agent research post).
- Calibrate skepticism explicitly. Out of the box, "Claude is a poor QA agent": it finds issues and then "talk[s] itself into deciding they weren't a big deal". Include 2–3 few-shot examples of rejected plans. Tell it: "When unsure, fail the item; do not rationalize." Tell it to penalize length and not reward verbosity (verbosity bias).

### 6.2 Plan rubric (each item pass/fail plus a one-line evidence quote)
1. Every acceptance criterion maps to ≥1 named test, including ≥1 e2e test if user-facing.
2. Every "file to modify" exists in the file listing. New files follow existing layout conventions.
3. No work is listed outside the sub-task (compare to the story and the out-of-scope list).
4. Estimated diff ≤ 600 LOC and ≤ 10 files, otherwise fail with "split".
5. New dependencies are justified, pinned, and not duplicating an existing dependency.
6. No edits to protected paths (workflows, verify.sh, features.json, existing tests outside the allowlist).
7. DB migrations are additive or reversible, and the seed is updated if the schema changed.
8. Security touchpoints (authn/z, input validation, secrets, CORS) are identified when the task touches endpoints.
9. Verification commands are exact and runnable.
10. Steps are ordered so each step ends green (no "big bang" final step).
- **Hard-fail items:** 1, 3, 6, 9. Pass = all hard items pass and score ≥ 0.8.
- On fail: return `required_changes[]` to the planner. At most **2 revisions**. The third failure marks the sub-task `blocked:plan` and the run continues. The judge never edits the plan itself.

### 6.3 Decision logging (`.process/<issue>-<slug>/04-plan-judge.md`)
Frontmatter plus body:
```yaml
---
gate: plan            # split | plan | pr
verdict: pass         # pass | revise | blocked
score: 0.9
round: 1
judge_model: claude-sonnet-5
plan_sha: <git hash-object 03-plan.md>
prompt_version: judge-plan@3
ts: 2026-09-27T14:03:11Z
---
| # | Criterion | Pass | Evidence |
...
## Risks accepted
## Required changes (if revise)
```
- Also post the verdict as a comment on the GitHub issue. This gives an audit trail a human can skim after the run.
- Store `prompt_version`. When you tune the judge, compare old and new verdicts on the logged cases before rolling out. Anthropic tuned its evaluator by "reading evaluator logs, identifying judgment gaps versus human assessment" over several rounds.

---

## 7. Checks and review (per PR)

1. **Deterministic first, LLM second.** Linters, typecheck, tests, gitleaks and test-integrity run before any LLM reviewer spends tokens. OpenAI encodes "golden principles" as custom linters whose error messages carry remediation instructions. Do the same for recurring review findings: turn a finding into a lint rule and remove it from the reviewer prompt.
2. **Reviewer subagents run in fresh contexts.** They see the diff, `03-plan.md`, the ACs and the evidence file only. A fresh context "won't be biased toward code it just wrote". Prompt: "Report only gaps that affect correctness, security, or the stated acceptance criteria. Do not report style preferences or speculative hardening." The docs warn that reviewers asked for gaps always find some, and "chasing every finding leads to over-engineering".
3. Reviewer output schema: `findings[]: {severity: blocker|major|minor, file, line, claim, evidence, fix}`. Only `blocker`/`major` with evidence loop back to the implementer.
4. **PR-Agent comments:** the comment-collector gathers them, and the triager classifies each as `must-fix` / `nice` / `wrong` using `--json-schema`. Only `must-fix` goes back to the implementer. Log `wrong` items to `08-triage.md` for prompt tuning.
5. **At most 2 review→fix rounds.** After round 2, if blockers remain, mark the task `blocked:review`, close the PR (don't merge) and continue. Never let reviewer and implementer ping-pong. A PR→fix round is a fresh implementer session given the findings file.
6. **Security-reviewer** runs on every PR that touches `api/`, `auth/`, migrations, config or dependencies. Its blockers are hard-fail regardless of round count.
7. **UI sub-tasks:** the evaluator starts the app (`init.sh`), runs the e2e test and takes Playwright screenshots of changed pages into `.process/<task>/screens/`. Native `alert()` dialogs are invisible to Puppeteer-style tools (Anthropic), so tell the agents not to use them.

---

## 8. Merge (runner only)

1. Push with the App token: `git push -u origin ap/S3.2`. Create the PR with `gh pr create --title "S3.2: <title>" --body-file .process/<task>/09-pr-body.md --label autopilot`. The body includes `Closes #123`, the AC checklist, the evidence tail and the judge verdict link.
2. Before creating, check for an existing PR (idempotent resume): `gh pr list --head ap/S3.2 --json number,state`.
3. Wait for PR-Agent plus reviewers (§7), then:
   ```bash
   SHA=$(gh pr view $PR --json headRefOid -q .headRefOid)
   gh pr merge $PR --auto --squash --delete-branch --match-head-commit "$SHA"
   ```
   `--match-head-commit` prevents merging a head that changed after review.
4. Poll `gh pr view $PR --json state,mergeStateStatus,statusCheckRollup` every 30 s, with a 30 min cap. Branch on the state:
   - `BEHIND` (strict up-to-date rule): `gh pr update-branch $PR`. Or use a merge queue, which does this for you and needs the `merge_group` trigger.
   - `DIRTY` (conflict): **don't let an LLM resolve large conflicts.** Close the PR and re-run the implementer from fresh `origin/main` (tasks are small, so this is cheap). Allow one LLM-resolved rebase only if the conflict touches ≤ 2 files.
   - A check failed: fetch the logs with `gh run view --log-failed | tail -200` into `NN-ci-failure.md`, then run one fresh fix session. If it is still red, mark the task blocked.
   - A flake (the check passes on a single re-run with `gh run rerun --failed`): allow **one** rerun per PR and log the test in `.process/_autopilot/flaky.md`. After 3 flake strikes, the orchestrator (never the implementer) quarantines the test with an issue link, with at most 3 quarantined tests per run. OpenAI: "test flakes are often addressed with follow-up runs rather than blocking progress indefinitely".
5. **Post-merge:** wait for the `push: main` CI run on the squash SHA. If green, the runner flips `features.json[id].passes=true` (only when that feature's e2e test is in the green run), closes the issue, appends to progress.md and updates `00-status.md`.
6. **Red main means stop the line.** If main CI fails after a merge:
   ```bash
   git fetch origin && git switch -c ap/revert-$SHA origin/main
   git revert --no-edit $SHA && git push -u origin HEAD
   gh pr create --title "Revert S3.2 (main red)" --body "auto-revert: <failing job>" --label autopilot,revert
   gh pr merge --auto --squash --delete-branch
   ```
   Then requeue S3.2 with `attempts+1` and the failure log attached. All new work pauses until main is green. If the revert itself is red, **stop the run** and alert, because the base is broken.
7. Don't enable auto-merge on PRs that change `.github/workflows/**`, `scripts/verify.sh` or the ruleset. Those are Step-0-only changes and need a human in POC mode as well.

---

## 9. Recovery and resume

### 9.1 `00-status.md` (per task) — make it machine-parseable
```yaml
---
task: S3.2
issue: 123
state: implementing   # queued|splitting|planning|judging|implementing|verifying|reviewing|pr_open|merging|merged|blocked
step_file: 05-impl-notes.md
attempt: 2
model: opencode/<model>
branch: ap/S3.2
pr: null
last_session_id: 5b1c…
last_sha: 9f2e1ab
blocked_reason: null
updated: 2026-09-27T14:22:05Z
---
Human-readable 3-line summary.
```
Write it atomically (write to a tmp file, then `mv`) at **every** state transition. The run-level `.process/_autopilot/run.json` holds `run_id, started, deadline, budget_used, current_task, circuit: closed|open, consecutive_failures`.

### 9.2 Resume algorithm (on supervisor start)
1. Validate the run: the deadline hasn't passed and the budget isn't exhausted.
2. `git fetch`. Check that main CI on HEAD is green. If red, go to §8.6 first.
3. For each task with a non-terminal `state`, reconcile against GitHub truth (branch exists? PR exists/merged? checks state?), because GitHub wins over local files. Examples: a PR is merged but the state says `merging` → run post-merge steps. The branch exists with no PR and the state is `implementing` → discard the branch and restart the attempt, since partial implementer work isn't trusted.
4. Kill orphaned processes (dev servers via PID files; `docker compose -p ap-* down -v`) and remove stale worktrees (`git worktree prune`).
5. Continue scheduling (§4.3).

### 9.3 Failure → response table
| Failure | Detection | Response |
|---|---|---|
| Context rot / drift | Long step, repeated reads, contradictions | Fresh process per step. `CLAUDE_AUTOCOMPACT_PCT_OVERRIDE=70`. Hard `--max-turns`. |
| Looping | §1.3 detectors | SIGINT, count attempt, go to the next rung of the ladder |
| Declares done early | Harness verify fails | Failed attempt. The runner never trusts `result` text. |
| Test deletion / weakening | test-integrity job, path hook | Reject the PR and retry with an explicit warning. After 2 strikes, block. |
| Placeholders | Diff grep | Reject and retry |
| Scope creep | Changed-files ⊄ plan | Revert extras, retry once |
| Dependency hell | `npm ci` fails, hook denies install | Planner adds pinned dep via plan revision. Never `--force`/`--legacy-peer-deps` without a logged decision. |
| Port conflicts / stale services | init.sh health fails | Ephemeral ports, per-task compose project, PID-file cleanup at session start |
| Broken main | push-to-main CI red | Auto-revert (§8.6), pause queue |
| Merge conflicts | `mergeStateStatus=DIRTY` | Re-implement from fresh main |
| Flaky tests | Pass on 1 rerun | Log, then quarantine after 3 strikes (orchestrator only) |
| 429 / 529 | `system/api_retry` | `CLAUDE_CODE_RETRY_WATCHDOG=1`. On spend-limit 429: pause run until reset. GitHub secondary limits: ≥1 s between mutating `gh` calls and honour `retry-after`. |
| Token runaway | Ledger > caps | Per-step `--max-budget-usd`, run cap, stop at 100% |
| Secrets | gitleaks, push protection, `.env` read deny | Block the PR. Rotate if the secret was pushed. |
| Permission denials | `permission_denials` | Fix the allowlist or the prompt. Don't retry the same call. |
| Supervisor crash | Heartbeat stale 15 min | Watchdog restart, then resume algorithm |
| 3 consecutive failed tasks | run.json counter | Circuit opens: run milestone validation plus re-plan once. If the next 2 tasks also fail, stop the run and write the final report. |

### 9.4 End of run
The run stops when the deadline is reached, the budget is exhausted, the queue is empty or the circuit trips twice. It then:
- runs the full e2e suite on main;
- writes `.process/_autopilot/REPORT.md` covering features passing/total, blocked tasks with reasons, cost, time, flaky tests, judge overrides and the top 5 recurring failure signatures;
- tears down compose projects;
- leaves main green and tagged `autopilot-<run_id>`.

---

## 10. Prompt-craft rules for all agent prompts (autopilot variants)

- Put constraints **inside the procedural steps**, not in a trailing "notes" block. An unattended-agent post-mortem found appended "do not do X" notes were ignored.
- If an instruction keeps being skipped, emphasize that one line only (IMPORTANT). Emphasizing many lines makes none stand out (best practices). Ralph's "signs" approach: add a targeted line only after observing a specific failure, and remove it if it has no effect.
- Use absolute paths in all tool arguments and file references. Anthropic found models "would make mistakes with tools using relative filepaths".
- Replace every "ask the user" instruction in autopilot mode with "decide, record in Decision Log with rationale, continue". `--permission-prompts none` removes `AskUserQuestion`.
- Every prompt ends with the exact deliverable file and format the runner will parse.

---

## Sources

- Anthropic, Effective harnesses for long-running agents — https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents
- Anthropic, Harness design for long-running application development — https://www.anthropic.com/engineering/harness-design-long-running-apps
- Anthropic, Building a C compiler with a team of parallel Claudes — https://www.anthropic.com/engineering/building-c-compiler
- Anthropic, Building effective agents — https://www.anthropic.com/engineering/building-effective-agents
- Anthropic, Effective context engineering for AI agents — https://www.anthropic.com/engineering/effective-context-engineering-for-ai-agents
- Anthropic, Writing effective tools for agents — https://www.anthropic.com/engineering/writing-tools-for-agents
- Anthropic, How we built our multi-agent research system — https://www.anthropic.com/engineering/multi-agent-research-system
- Anthropic, Claude Code auto mode — https://anthropic.com/engineering/claude-code-auto-mode
- Anthropic, Emergent misalignment from reward hacking — https://www.anthropic.com/research/emergent-misalignment-reward-hacking
- Claude Code docs: Best practices — https://code.claude.com/docs/en/best-practices
- Claude Code docs: Headless / `claude -p` — https://code.claude.com/docs/en/headless
- Claude Code docs: CLI reference — https://code.claude.com/docs/en/cli-reference
- Claude Code docs: Hooks reference — https://code.claude.com/docs/en/hooks
- Claude Code docs: Hooks guide (Stop hook block cap) — https://code.claude.com/docs/en/hooks-guide
- Claude Code docs: Permission modes — https://code.claude.com/docs/en/permission-modes
- Claude Code docs: /goal — https://code.claude.com/docs/en/goal
- Claude Code docs: Subagents — https://code.claude.com/docs/en/sub-agents
- Claude Code docs: Environment variables — https://code.claude.com/docs/en/env-vars
- Claude Code docs: Model config / auto-compaction — https://code.claude.com/docs/en/model-config
- Claude Code docs: Agent SDK TypeScript (result subtypes) — https://code.claude.com/docs/en/agent-sdk/typescript
- Claude Code ralph-wiggum plugin README — https://github.com/anthropics/claude-code/tree/main/plugins/ralph-wiggum
- Claude API rate limits — https://platform.claude.com/docs/en/api/rate-limits
- Geoffrey Huntley, Ralph Wiggum as a software engineer — https://ghuntley.com/ralph/
- Clayton Farr, Ralph Playbook — https://github.com/ClaytonFarr/ralph-playbook
- paddo.dev, The Ralph Wiggum Playbook — https://paddo.dev/blog/ralph-wiggum-playbook/
- Cursor, Scaling long-running autonomous coding — https://cursor.com/blog/scaling-agents
- Factory, Introducing Missions — https://factory.com/news/missions
- Amp, Handoff (No More Compaction) — https://ampcode.com/news/handoff
- OpenAI, Harness engineering — https://openai.com/index/harness-engineering/
- OpenAI, Run long horizon tasks with Codex — https://developers.openai.com/blog/run-long-horizon-tasks-with-codex
- OpenAI Cookbook, Using PLANS.md (ExecPlans) — https://developers.openai.com/cookbook/articles/codex_exec_plans
- Kaushik Gopal, ExecPlans — https://kau.sh/blog/exec-plans/
- Cognition, Coding Agents 101 — https://devin.ai/agents101
- OpenHands, Stuck Detector — https://docs.openhands.dev/sdk/guides/agent-stuck-detector
- SWE-agent paper (ACI) — https://arxiv.org/html/2405.15793v1
- ImpossibleBench (test-exploitation) — https://arxiv.org/html/2510.20270v1
- Aider, Linting and testing — https://aider.chat/docs/usage/lint-test.html
- OpenCode CLI — https://opencode.ai/docs/cli/ ; permissions — https://opencode.ai/docs/permissions/
- Kiro specs — https://kiro.dev/docs/specs/
- GitHub spec-kit — https://github.com/github/spec-kit
- Google Jules docs — https://jules.google/docs/
- GitHub Copilot coding agent environment — https://docs.github.com/copilot/how-tos/agents/copilot-coding-agent/customizing-the-development-environment-for-copilot-coding-agent
- GitHub CLI `gh pr merge` — https://cli.github.com/manual/gh_pr_merge
- GitHub, Automatically merging a PR — https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/incorporating-changes-from-a-pull-request/automatically-merging-a-pull-request
- GitHub, About protected branches — https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches
- GitHub, Troubleshooting required status checks — https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/collaborating-on-repositories-with-code-quality-features/troubleshooting-required-status-checks
- GitHub, GITHUB_TOKEN (workflow triggering) — https://docs.github.com/en/actions/concepts/security/github_token
- GitHub, Push protection — https://docs.github.com/en/code-security/secret-scanning/introduction/about-push-protection
- PR-Agent on GitHub Actions with Claude (runbook) — https://gist.github.com/positonic/408eede7bbd263bf969f1fa5e7dc7677
- Docker Compose services reference — https://docs.docker.com/reference/compose-file/services/
- Walking skeleton (Code Climate) — https://codeclimate.com/legacy/kickstart-your-next-project-with-a-walking-skeleton
- LLM judge biases — https://www.sebastiansigl.com/blog/llm-judge-biases-and-how-to-fix-them/
- Unattended agent post-mortem — https://dev.to/cele71/i-left-an-ai-agent-running-unattended-for-a-day-here-is-everything-that-broke-1p0p
