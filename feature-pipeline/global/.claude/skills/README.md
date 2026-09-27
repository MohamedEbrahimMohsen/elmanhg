# Vendored skills — who uses what

Sources: dietrichgebert/ponytail · ayghri/i-have-adhd · juliusbrussee/caveman · obra/superpowers · garrytan/gstack · nextlevelbuilder/ui-ux-pro-max-skill.
Copied as-is (ui-styling without its 5 MB canvas-fonts folder). Update by re-copying the folder; never edit in place.

| Skill | Orchestrator | Splitter | Explorer | Planner | Style-check | Reviewer | Collector | Triager | Implement | Rework | Fix |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| caveman | x | x | x | x | x | x | x | x | | | |
| i-have-adhd | x | | | | | | | | | | |
| caveman-commit | x | | | | | | | | | | |
| gstack-careful | x | | | | | | | | | | |
| verification-before-completion | x | | | | | x | | | x | x | x |
| ponytail | | x | | x | | | | x | x | | |
| writing-plans | | x | | x | | | | | | | |
| caveman-explore | | | x | | | | | | | | |
| lean-build | | | | x | | | | | x | | |
| gstack-plan-eng-review | | | | x | | | | | | | |
| caveman-review | | | | | x | x | | | | | |
| gstack-review | | | | | | x | | | | | |
| ponytail-review | | | | | | x | | | | | |
| verify-and-stop | | | | | | x (verify mode) | | | | | |
| receiving-code-review | | | | | | | | x | | x | |
| test-driven-development | | | | | | | | | x | | |
| surgical-patch | | | | | | | | | | x | x |
| systematic-debugging | | | | | | | | | | x | |
| ui-ux-pro-max (UI stacks only) | | | | x | | x | | | x | | |
| ui-styling (UI stacks only) | | | | | | | | | x | | |
| investigate-first | reserved: use when a build fails with an unclear cause before rework | | | | | | | | | | |

Security-reviewer agent reads: gstack-careful · verification-before-completion · caveman. E2E runs in Agy (Antigravity), outside Claude; it reads the prompt in `templates/e2e-agy.md`, not these skills.

Per-stack style skills (`<repo>/.claude/skills/<stack>-feature/`) are project files, not vendored here; the agents get their paths from `pipeline.yml` `stacks.<name>.style`.

Not vendored, on purpose: gstack `ship`, `land-and-deploy`, `qa`, `browse`, `design-*` (need the gstack runtime and a browser; our pipeline lands via GitHub + human review), superpowers `subagent-driven-development` / `executing-plans` / `using-git-worktrees` (our orchestrator already does this), caveman `caveman-setup` / `-stats` / `-learn` / `-compress` (Caveman Cloud tooling), ponytail `-audit` / `-debt` / `-gain` (repo-wide, not per-PR), ui-ux `brand` / `banner` / `slides` / `design` (not code).

## Team-written skills (ours, `momenta-*`)

These are written by the team, not vendored. Edit them like any other package file, then `bump.sh`.

| Skill | Read by | When |
|---|---|---|
| momenta-api-contract | planner · implementer (API stacks) · reviewer | any task that adds or changes an HTTP endpoint or client |
| momenta-dependency-policy | planner · implementer · security-reviewer | any task that adds, removes, or upgrades a package |
| momenta-greenfield-bootstrap | planner · implementer | story 1 of a new repo (walking skeleton), and any stack marked `n (new)` in §1a0 |

Agents added in this release: `plan-judge` (autopilot only; replaces the dev at split/plan gates) reads writing-plans · ponytail · verification-before-completion · caveman. `/product` (PRD → epic/stories) reads writing-plans · ponytail · caveman · momenta-api-contract.
