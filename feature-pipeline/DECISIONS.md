# Team Feature Pipeline — decisions (updated 2026-09-27)

Diagram: https://claude.ai/artifact/DPNM8LKqdXkptacnmX2mjk · Dev guide: https://claude.ai/artifact/F1UKJ4wqc4dQpptkmBbRks
Package: feature-pipeline.zip v1.0.0 (unpublished; stays v1 until first team release).

## Scope
- One process for all areas: AI, Backend, Frontend, Fullstack, Mobile. Stacks: dotnet, react, node, flutter, kmp, python, python-flask, python-django. Auto-detected, never declared.
- GitHub for repos, issues, Actions. PR-Agent (self-hosted) for PR comments.

## Models
- Orchestrator + judgement (split, plan, review, triage, security, plan-judge): Opus 5.5 (medium; judge high).
- Cheap steps (explore, style check, collect): Sonnet medium.
- Code (implement, rework, fix, postman): OpenCode (`litellm/deepseek-flash#high`) by default; Claude via personal pipeline.local.yml.
- E2E: Agy (Antigravity) `Gemini 3.8 Flash (High)`, `--dangerously-skip-permissions`; browser via Playwright MCP if the CLI browser subagent is unavailable; no browser → BLOCKED, not FAIL.

## Flow
story → split → split gate → sub-issues → per sub-task: explore → (design gate, UI) → plan → plan gate → implement → postman → style → coverage → security → e2e → review (≤2) → draft PR → collect → triage → fix (≤2) → PR gate (dev + peer) → squash merge → next.
New repo: /product docs/PRD.md → assumptions, ADRs, epic + stories; story 1 = walking skeleton (momenta-greenfield-bootstrap).

## Config
- pipeline.yml: every stack has the same keys: tests.{unit,postman,e2e,coverage} + security.{packages,secrets,sast,prompt-injection}, each with on/off. fail_on: high; pre-existing findings never block.
- Missing stack folder: /feature searches by marker files and asks (pick · n=new/scaffold · d=remove). Never guesses.

## Guards (always on)
- Test integrity: editing/skipping/deleting existing tests not listed in the plan = blocking.
- BLOCKED escape hatch for every agent. Proof (commands + output) in implementer reports. New packages only if planned + registry-verified.

## Autopilot (POC only)
- Human gates are primary. Autopilot only from personal pipeline.local.yml (`autopilot.on`); ignored in pipeline.yml.
- Keys: `on`, `judge {model: opus, effort: medium}`, `hours: 72`, `max_attempts: 3`. No money cap (subscription). Merge always squash.
- plan-judge replaces dev at backlog/split/plan gates; PRs auto-merge when all green; retries: OpenCode → Claude Sonnet → Claude Opus, then skipped.
- Never stops on failure: blocked tasks skipped; red main → revert, then a "repair main" task first; usage limit → wait 20 min and resume (not an attempt). Ends on: all done · hours · nothing runnable · main unrepairable after max_attempts.
- Judge effort is recorded only; Claude Code can't set effort per subagent yet.
- Runner: ~/.claude/bin/autopilot.sh|ps1, fresh `claude -p` per sub-task, `--permission-mode bypassPermissions` + settings.local.json deny list. Disposable VM + repo-scoped token.

## Skills
- Vendored third-party skills in ~/.claude/skills are never edited.
- Team-written: stack skills + testing conventions (project), momenta-api-contract, momenta-dependency-policy, momenta-greenfield-bootstrap (global).
- /skill-scout (monthly skill discovery, proposal only) — proposed, not built.

## Open
- Unverified: agy headless browser; `gh issue create --parent/--blocked-by` and `gh pr checks --required` on installed gh.
