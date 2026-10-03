VERDICT: APPROVED

# Review r2: Indigo calm theme and header/alignment fixes (#276, E19.S1)

## Blocking
None.

## Round-1 finding 1: resolved
- `web/src/features/avatar/components/AvatarDock.tsx:15,23`: `useMatch({ from: '/student/exam/$sessionId', shouldThrow: false })` hides only the dock button. The route id exists in `routes/student/exam.$sessionId.tsx:4` and `routeTree.gen.ts:975`. On every other student route the match is `undefined`, so the dock still renders. `AvatarDock.test.tsx:18` (dock on `/student` opens the panel) still passes.
- The exam bar's Ask button still works during an exam. `AskAvatarButton` calls `open()`, which sets `state.isOpen` and mounts the panel (lines 17-19, 35-46). The existing test `ExamPage.test.tsx:102` (the panel opens with the exam refusal) passes. When the panel closes, the dock stays hidden on the exam route, which is intended.
- `AvatarDock.test.tsx:27` constrains the change: it would find the "Assistant" button if the `takingExam` guard were removed.
- Declared deviation (route match, not the `examInProgress` status signal): the reasoning holds. The status query lives only in the lazy panel.

## r2 non-blocking items: verified
- More menu `focusout` (`TopNavMore.tsx:34-46`): it closes only when `relatedTarget` is outside the root, and the listener is removed in cleanup. The test at `TopNavMore.test.tsx:66` (shift+Tab) would fail without the listener. The More active-state test is at `:77`.
- Tap-target exception: `docs/design-system.md` §5.7 (lines 177, 179) and `.claude/design-system.md:94,160` agree (36 px only at ≥ lg, 44 px sign-out below lg). Code matches: `AppBar.tsx:32` uses `size="sm"` plus `max-lg:min-h-11`.
- Gutter fix (`NotFound.tsx:10`, `RouteError.tsx:20`): in the built CSS, `:where(:is(main)) .in-\[main\]\:px-0` comes after `.px-4`, and `.lg\:in-\[main\]\:px-0` comes after `.lg\:px-6`, at equal specificity. So the gutter is removed inside the shell `main` at every width and kept at root level with no shell.
- Contrast `toThrow` case is present.

## Non-blocking
- `docs/design-system.md` §5.7 line 179 and `.claude/design-system.md:95` list the More menu's close triggers as Esc, outside click and choosing an item. Focus-out is now also a trigger. The behaviour is a superset and contradicts nothing; add it on the next docs pass.

## Verified (rerun by me in the worktree)
- `npm run typecheck`: exit 0. `npm run lint`: exit 0.
- `npx vitest run --maxWorkers=4`: 283 files and 1673 tests passed, no timeouts.
- `npm run build`: ok.
- `npm run perf:budget`: entry 209/210, landing 218/220, lesson 240/240, quiz 255/255, admin-dashboard 229/230, admin-users 263/270, teacher-home 257/265, all ok. `git diff origin/main -- web/scripts/perf` is empty, so no limit was raised.

## Test quality
- `AvatarDock.test.tsx`: both tests constrain the code (dock opens the panel; dock is absent on the exam route while the exam bar's Ask is present).
- `TopNavMore.test.tsx`: the new focus-out and active-state tests are behavioural and not vacuous.
