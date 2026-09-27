# Docs–Implementation Sync Rule

Every review of changes in this repo (human, /code-review, or any reviewer agent) MUST
check `/docs` consistency against the change being reviewed — using this distinction:

## NOT a violation — incompleteness

The docs describe the target; the code lags behind. That is the normal state of an
in-progress plan. Examples:

- The plan lists 10 features and only 6 exist.
- `docs/plugin-spec.md` describes the team merge rules but team compilation isn't built yet.
- A phase is half-done.

Never flag missing implementation as a docs problem, and never "fix" docs by deleting
the not-yet-built parts.

## A violation — divergence

The change alters WHAT the product does or HOW it is designed, and the doc that
describes that area still says the old thing. If the implementation and the doc give
two different answers to the same question, the change is incomplete until the doc is
updated in the same change. Examples of divergence triggers:

- Business logic or product behavior changes (e.g. the upload-only pivot: composer flow
  changed → plugin-spec, implementation-plan, security-scan, design-prompt all had to move).
- Scope changes: a feature added, dropped, replaced, or deferred.
- Architecture or data-model changes: new/removed Azure resources, tables, pipeline steps.
- Policy changes: limits, security-scan rules or tiers, naming/format contracts
  (plugin layout, marketplace.json shape, URL schemes).
- Constitution-level rule changes.

## Doc ownership map (what to check per change area)

| Change touches… | Doc that must agree |
|---|---|
| Scope, phases, feature list, business rules, roles, plans/pricing, grading, mastery, exams | `docs/PRD.md` |
| Engineering rules, style, config policy, stack | `docs/constitution.md` |
| Colours, type, spacing, components, UI rules | `docs/design-system.md` **and** `.claude/design-system.md` (tokens) — both must agree |
| Any UI page's content, flow, or states | `docs/claude-design-prompt.md` §4–§6 and `docs/prototype.md` |
| Epics / stories / sub-tasks | `docs/backlog.json` |
| How to run the system, what is faked, run results | `docs/implementation-report.md` (created at the end of the autopilot run) |

`.claude/design-system.md` is the one allowed exception to "docs live only in `/docs`": the pipeline reads tokens from it.

## Reviewer output

When divergence is found, report it as a blocking finding naming BOTH sides: the code
change and the stale doc section. When only incompleteness is found, say nothing about
docs. All docs live in `/docs` only (plus root README.md) — a doc created anywhere else
is itself a violation.
