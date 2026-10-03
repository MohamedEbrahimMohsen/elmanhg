VERDICT: APPROVED

# Review r2 — Quiz bundle cleanup for budget headroom (#283, E19.S3)

## Blocking
None. Round-1 finding 1 is resolved.

## Non-blocking
- `web/src/test/tailwindCascade.ts:18-24`: the paths resolve against `process.cwd()`. That works under `npm test` and in CI (`working-directory: web`). Running `vitest --root web` from another directory fails with ENOENT on `src/styles/app.css` (I reproduced this from the repo root). Anchoring the paths to `import.meta.dirname` would make this robust.
- `web/src/test/tailwindCascade.ts:27`: the cascade model treats `@media (hover: hover)` as matching and handles only the selector shapes Tailwind emits here (classes, attributes, pseudo-classes, `:not()`). That is enough for this component, but it is not a general specificity engine.

## Verified
- Finding 1: `web/src/shared/ui/button.tsx:7` and `:17`. In the built `dist/assets/index-BerJDKqP.css`, the guarded hover rules can no longer match while the competing state holds:
  - `.hover\:not-disabled\:not-active\:bg-accent-hover:hover:not(:disabled):not(:active)` is at byte 43216.
  - `.hover\:not-disabled\:not-aria-pressed\:bg-soft:hover:not(:disabled):not([aria-pressed=true])` is at byte 43346.
  - These come before `.active\:bg-accent-pressed:active` (45308), `.active\:bg-soft:active` (45390) and `.aria-pressed\:bg-accent-soft[aria-pressed=true]` (45883). Those three are (0,2,0) and beat the (0,1,0) base fills.
- State precedence per variant:
  - Primary/accent: rest = accent, hover = accent.hover, hover+active = accent.pressed, disabled+hover = accent.
  - Secondary: hover = soft, toggled+hover = accent.soft, toggled+active = accent.soft (the aria-pressed rule comes later at equal specificity, as on origin/main), disabled+hover = surface, disabled toggled = accent.soft.
  - Danger: hover = danger.soft, disabled = surface.
  - Ghost: hover = accent.soft, disabled = transparent. Danger and ghost have no competing state.
  - `asChild` links keep the hover fill, because `:not(:disabled)` matches an `<a>`.
  - No `Button` consumer passes `hover:`, `bg-`, `aria-*` or `data-*` fill overrides, so the higher hover specificity shadows nothing.
- The cascade helper compiles the real `app.css` with Tailwind `compile()`. It keeps source order, counts specificity correctly for `:not()` contents (pinned by the specificity test), and picks the winner by specificity and then order. With the r1 classes, primary/accent pressed and toggled-secondary hovered/pressed would resolve to the hover fill, so 4 of the cases would fail. The test does constrain the code.
- `web/src/app/i18n.ts:92-94`: the re-init path has `.catch(() => undefined)`. `i18n.test.ts:83` checks that `changeLanguage` is never called and that the language and `lang` stay `ar`.
- `AuditLogPage.arabic.test.tsx`: removes every `en` bundle and renders `/admin/audit` with `lng: 'ar'`. It checks Arabic heading, label and button text and that no raw keys appear. Without `registerAuditLocales()`, the keys would render, so the test fails. It is meaningful.
- `npm run build` exits 0 ("Precompressed 292 files").
- `perf:budget` is all ok: entry 190/210, landing 199/220, lesson 221/240, quiz 236/255, admin-dashboard 214/233, admin-users 245/270, teacher-home 246/265. `budgets.json`, `bundleBudget.ts`, `vite.config.ts` and `src/styles` are unchanged against origin/main, so no budget changed.
- `typecheck` and `lint` (`--max-warnings=0`) are clean. `format:check --end-of-line auto` is clean. Both CI greps find nothing.
- Vitest (`npm test -- --run --coverage`): 297 files and 1738 tests passed, with no timeouts. Coverage: statements 95.42 %, branches 84.1 %, lines 95.55 %.
- Docs: `.claude/design-system.md:86` (pressed accent.pressed, aria-pressed accent.soft, disabled no hover fill) agrees with the code. No divergence.

## Test quality
- `button.test.tsx`: the cascade cases constrain the real compiled precedence and would catch the round-1 regression.
- `i18n.test.ts`: the re-init failure test constrains the `.catch` path. Without it, vitest reports an unhandled rejection.
- `AuditLogPage.arabic.test.tsx`: constrains page-level namespace registration for an Arabic first render.
