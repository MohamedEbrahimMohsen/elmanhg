VERDICT: CHANGES_REQUESTED

# Review — [E19.S6] Floating assistant button must not cover page content (#296)

## Blocking

### 1. `.claude/design-system.md` does not carry the new reserved-space rule that `docs/design-system.md` now states
**Where:** `.claude/design-system.md:105` (AssistantFab row), against `docs/design-system.md` §5.10 Avatar panel (line 238, the new sentence)
**Rule:** `.claude/rules/docs-sync.md` ownership map: "Colours, type, spacing, components, UI rules → `docs/design-system.md` **and** `.claude/design-system.md` — both must agree"; the pipeline reads component specs from `.claude/design-system.md`.
**Problem:** The change adds a component spacing rule (student pages that show the button keep room under `<main>` = offset + height + gap + safe-area; the full-page assistant and the exam page do not). It was written only into `docs/design-system.md` §5.10. The pipeline copy's AssistantFab row still says just "fixed bottom inline-start", and the new `pb-assistant-dock` / `pb-assistant-dock-desktop` utilities (`web/src/styles/app.css:128-134`) appear nowhere in it. Compare the neighbouring AssistantPage row, which does name its utility (`h-assistant`).
**Failure:** A planner or reviewer working from `.claude/design-system.md` gets a FAB spec with no reserved space, while `docs/design-system.md` says there is one. The two copies now give different specs for the same component, and `pb-assistant-dock` looks like an undocumented utility.
**Fix:** Add one clause to the AssistantFab row at `.claude/design-system.md:105`, e.g. "; student `<main>` reserves offset + 48 + gap 16 + safe-area below content where the FAB shows (`pb-assistant-dock`, `lg:pb-assistant-dock-desktop`); none on `/student/assistant` or the exam route".

## Non-blocking
- `web/src/features/avatar/components/StudentAvatarDock.tsx:5` still runs its own `/student/assistant` `useMatch`. That matches the plan ("stays as is") and the behaviour is the same. It could become `useAssistantDockShown`'s assistant half later, though it must still mount on the exam route.
- `web/src/routes/student/route.tsx:8-16`: `StudentLayout` calls one hook inside a route file. This is composition, not business logic, and it is in line with the many `Route.useParams()` route components. A `StudentShell` component in a feature would be cleaner (skill rule at SKILL.md:20).
- `web/src/features/shell/components/AppShell.assistantSpace.test.tsx`: no case for `/student/assistant/$conversationId`. The hook does cover it, because that route is a child of the `/student/assistant` match, the same as `StudentAvatarDock` relies on.

## Verified
- Arithmetic, from the actual classes: the dock is `bottom-24 h-12 lg:bottom-6` (`AvatarDock.tsx:10`) and `--spacing` = `--ds-space-1` = 4px (`app.css:64`, `tokens.css:66`). Mobile: 96 + 48 + 16 = 160 = 4 × 40. Desktop: 24 + 48 + 16 = 88 = 4 × 22. Both add `env(safe-area-inset-bottom)`. `lg` = 900px (`tokens.css:101`), so 768 uses the mobile values. The 16px clearance in the report is correct.
- Built CSS cascade: `.pb-assistant-dock{` sits at offset 20544 and `lg:pb-assistant-dock-desktop` at 33060, inside `@media (width>=900px)` (32225). The desktop override wins at lg.
- Single source: `useAssistantDockShown` (`hooks/useAssistantDockShown.ts:3-7`) drives both the dock button (`AvatarDock.tsx:15,23`) and `reserveAssistantSpace` (`routes/student/route.tsx:8,12`). The dock class string and the panel are unchanged in the diff.
- Unchanged pages: `AppShell` defaults `reserveAssistantSpace = false` and keeps `pb-24 lg:pb-8` (`AppShell.tsx:16,34`). Only one pair is ever applied. Teacher and admin pass no prop. Assistant and exam get `false` from the hook.
- Plan files: both files to create exist. The six touched files match the plan. Nothing extra. The deviation (sibling test file, no DOM measurement) is disclosed honestly.
- Rerun here: `npm run typecheck` clean, `npm run lint` clean, `npx vitest run` 307 files / 1821 tests passed with no timeouts, `npm run build` succeeded (298 precompressed), `perf:budget` all 8 ok with the same numbers as reported, both CI greps clean, no `routeTree.gen.ts` or `tokens.css` drift.
- Docs: no other owning doc gives a conflicting answer (`docs/avatar.md:206`, `claude-design-prompt.md`, `prototype.md` say nothing about the dock's bottom spacing).

## Test quality
`AppShell.assistantSpace.test.tsx`: all 4 tests constrain the implementation.
- `/student` fails if the route stops passing the prop.
- `/student/assistant` fails if the hook drops the assistant match.
- `/student/exam/:id` fails if the hook drops the exam match.
- `/teacher` fails if the AppShell default flips.

The tests assert classes, not pixels; jsdom has no layout, which the report states openly.
