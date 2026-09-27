# Status — #<issue> <slug>

step: <split | split-gate | explore | design-gate | plan | plan-gate | implement | postman | style-check | coverage | security | e2e | review | pr | collect | triage | pr-gate | merged | stopped>
waiting: <none | dev: approve split | dev: figma links | dev: figma exports | dev: approve plan | dev: PR review | dev: decision … | deadline (autopilot stopped at a step boundary; next process resumes here)>
round: <review round> · cycle: <triage cycle>
stacks: <detected list>
bootstrap: <none | stacks this task creates from scratch>
switches: <per stack: tests unit/postman/assert/e2e/coverage · security packages/secrets/sast/prompt-injection — as decided at the plan gate>
branch: feature/<issue>-<slug>
pr: <number or —>
updated: <date time>
autopilot: <off | on · judge <model>/<effort> · deadline <iso | none>>
attempt: <n>/<max_attempts> · model: <ladder entry for this attempt>
judge: <split: APPROVE r1 | REVISE r1 → APPROVE r2 · plan: … | —>
blocked: <none | reason>

## Sub-tasks (story runs only)
| Order | Issue | Title | Assignee | State |
|-------|-------|-------|----------|-------|
<!-- todo · in-progress · pr-open · merged · skipped (other dev) -->

## Log
- <date time> — <what happened, one line>

## Attempts (autopilot)
| Attempt | Model | Branch | Started | Ended | Outcome / trigger |
|---------|-------|--------|---------|-------|-------------------|

## Judge verdicts (autopilot)
| Gate | Round | Verdict | Hard fails | File |
|------|-------|---------|------------|------|

## Autopilot decisions
<!-- - <UTC ts> · <point §> · chose <x> · over <y> · because <priority # / evidence> · undo: <how> -->
