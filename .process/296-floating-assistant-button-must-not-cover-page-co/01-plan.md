# Plan — [E19.S6] Floating assistant button must not cover page content (#296)

Small story; written by the implementer with orchestrator approval (no separate planner).

## Root cause
`AppShell` (`web/src/features/shell/components/AppShell.tsx`) gives `<main>` a fixed bottom padding of `pb-24 lg:pb-8`
(96 px / 32 px). That only clears the mobile tab bar. The dock (`AvatarDock`) is `fixed bottom-24 h-12 lg:bottom-6`, so
its top edge sits 144 px (mobile) / 72 px (desktop) above the viewport bottom. When the page is scrolled to the end, the
last 48 px (mobile) / 40 px (desktop) of content stays behind the pill, e.g. «درّب الآن» on `/student`.

## Fix
- `web/src/styles/app.css`: two utilities next to the existing `pb-safe-area` / `bottom-above-tab-bar` ones:
  - `pb-assistant-dock`: `calc(var(--ds-space-1) * 40 + env(safe-area-inset-bottom))` = dock offset 96 + height 48 + gap 16.
  - `pb-assistant-dock-desktop`: `calc(var(--ds-space-1) * 22 + env(safe-area-inset-bottom))` = offset 24 + height 48 + gap 16.
- `AppShell` gets `reserveAssistantSpace?: boolean`; when true `<main>` uses `pb-assistant-dock lg:pb-assistant-dock-desktop`
  instead of `pb-24 lg:pb-8` (picked conditionally, never both, so class order cannot decide).
- Visibility condition, single source: new hook `web/src/features/avatar/hooks/useAssistantDockShown.ts` returns
  `false` on `/student/assistant` and `/student/exam/$sessionId` (the two routes where the dock button is not shown),
  `true` otherwise. `AvatarDock` uses it in place of its own `takingExam` match; `StudentAvatarDock` stays as is (it
  still has to mount the panel on the exam route). Exported from the avatar barrel.
- `web/src/routes/student/route.tsx`: the component becomes a named `StudentLayout` that calls the hook and passes
  `reserveAssistantSpace` to `AppShell`. Space stays reserved while the panel is open (route-based, no layout jump).
- Dock look/position and panel unchanged. Teacher/admin shells unchanged (prop defaults to false).
- `docs/design-system.md` (assistant section): one sentence stating student pages reserve bottom space for the button.

## Files to create
- `web/src/features/avatar/hooks/useAssistantDockShown.ts`
- `web/src/features/shell/components/AppShell.assistantSpace.test.tsx` (AppShell.test.tsx is already 177 lines)

## Existing code touched
- `web/src/features/shell/components/AppShell.tsx`
- `web/src/features/avatar/components/AvatarDock.tsx`
- `web/src/features/avatar/index.ts`
- `web/src/routes/student/route.tsx`
- `web/src/styles/app.css`
- `docs/design-system.md`

## Test plan (`AppShell.assistantSpace.test.tsx`, describe `AppShell assistant space`)
- `it('reserves room below the content for the assistant button on student pages')` — `/student`, `main` has `pb-assistant-dock`, not `pb-24`.
- `it('reserves no assistant room on the full-page assistant')` — `/student/assistant`, `main` has `pb-24`, not `pb-assistant-dock`.
- `it('reserves no assistant room while an exam is being taken')` — `/student/exam/:id`, same.
- `it('reserves no assistant room for teachers')` — `/teacher`, same.
Existing `AvatarDock` tests cover the dock's own visibility (unchanged behaviour).

## Manual layout check
Production build + `vite preview` proxied to the demo API at :8080; at 375, 768, 1280 px on each student page, scroll to
the bottom and compare the last focusable element's `getBoundingClientRect().bottom` with the dock's `top`.
