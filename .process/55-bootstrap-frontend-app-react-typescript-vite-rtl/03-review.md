VERDICT: CHANGES_REQUESTED

# Review — Bootstrap frontend app (React, TypeScript, Vite, RTL) (#55, E1.S2)

## Blocking

### 1. Test 34 (`asChild`) is vacuous: it passes with `asChild` completely broken
**Where:** `web/src/shared/ui/button.test.tsx:13-21` (code under test `web/src/shared/ui/button.tsx:29`)
**Rule:** react-testing.md "Every test must … Fail if the production line under test is removed (non-vacuous)"; reviewer Review order #5
**Problem:** The test only asserts that `getByRole("link", { name: "Go" })` has `href="/x"`. If `asChild` is ignored, `Button` renders `<button type="button"><a href="/x">Go</a></button>`. The nested anchor is still a link named "Go" with that href, so the test still passes.
**Failure:** Change `button.tsx:29` to `const Comp = "button";`. The page then renders an interactive `<a>` nested inside a `<button>` (invalid, and an a11y defect), and test 34 stays green.
**Fix:** Also assert that no button is rendered: `expect(screen.queryByRole("button")).toBeNull()`. Alternatively, assert that the link carries `data-slot="button"`.

### 2. Docs divergence: the design brief still describes the old tab-bar rule
**Where:** code `web/src/features/shell/navConfig.ts:49-72` (3 tab-bar keys per role plus `morePath`), `web/src/features/shell/components/TabBar.tsx:14-17,30` (a synthetic "More" item, label `text-micro` = 12px). Doc `docs/claude-design-prompt.md:102`, § 2.4 "Components and their states", paragraph **Navigation**.
**Rule:** `.claude/rules/docs-sync.md`: divergence, component/UI rule (ownership row "Any UI page content, flow, or components"). Plan Decisions 20 and 22 call this a product-visible nav rule that needs a docs update.
**Problem:** § 2.4 of the brief says it is a copy of `docs/design-system.md`. It still reads: "bottom tab bar … **4 items per role** … **label 11 px** … Desktop: top bar, **same items** as text tabs". The change updated `docs/design-system.md` §5.7 and `.claude/design-system.md` (TabBar row) to "at most 4 items, 3 + المزيد, label type.micro, desktop shows every destination". It did not update this copy, so the two docs now answer the same question differently.
**Failure:** Ask "how many items does the admin bottom bar show, and at what label size?" The code and `docs/design-system.md` say 3 + "المزيد" at 12px. `docs/claude-design-prompt.md` § 2.4 says 4 at 11px.
**Fix:** In `docs/claude-design-prompt.md` § 2.4 **Navigation**, replace the mobile/desktop sentences with the new §5.7 wording from `docs/design-system.md`: at most 4 items, 3 primary + "المزيد" opening the rest, label 12 px (micro), desktop ≥ 900 px shows every destination.

## Non-blocking
- `web/src/features/session/hooks/useSession.ts:8`: the `throw` in `useSessionStore` (used outside `AppProviders`) has no test. It is a programmer-error guard with no user-visible outcome, but Review order #6 asks for one.
- `web/src/shared/lib/http.ts:14-19`: the `parsed.success === false` branch has no test. This is a JSON error body with a non-string `code`, e.g. `{ "code": 5 }`, which should become `UNHANDLED_EXCEPTION`. Only the non-JSON path (catch) is tested.
- `web/src/features/session/hooks/useSignOut.ts:9`: `queryClient.clear()` (a DoD item) is not observable in any test. This can wait until the first real query exists.
- `web/src/shared/lib/http.ts:2` → `web/src/app/env.ts:2`: the note in the implementation report is confirmed. The chain is shared → app → feature, so any future Orval-generated module pulls in the whole `features/session` barrel, including `DevSignInPage`. Consider moving `roles` somewhere leaf-level, or reading the env for `http` without the feature import.
- `web/src/features/session/schemas/loginSearchSchema.ts:7`: a redirect of slash + backslash + host (e.g. `/%5Cevil.example` decoded) passes the `/` + not-`//` check, and browsers treat that prefix like `//`. The navigation stays in-SPA today (`redirect({ href })`), but rejecting a leading slash-backslash is cheap hardening.
- `web/src/styles/app.css:110`: `scroll-padding-top` is 96px, but the desktop sticky header is about 101px (`min-h-14` row + `min-h-11` TopTabs + border). A focused target can be clipped by a few px (WCAG 2.4.11 only requires "not entirely hidden", so this passes).
- `web/src/features/shell/components/AppBar.tsx:24`: the role badge has no `font-semibold`. The design-system Badge is type.micro, weight 600, and `text-micro` sets only size and line height. The plan contract omitted it too.
- `web/vite.config.ts:14` (Deviation 2): React Compiler is off when mode is `test`. Accepted as declared: dev and prod builds still compile, and the compiler lint rules (`reactHooks.configs.flat.recommended`) still run. The trade-off is that tests exercise uncompiled components. Revisit when browser-mode tests land.

## Deviations judged
1. **Vitest `projects` split (`vite.config.ts:22-34`)**: accepted. The root cause is real: `setupFiles` touches `document` in a node environment. Both projects use `extends: true`. The coverage config is unchanged at the root, and C3 keeps its `// @vitest-environment node` line.
2. **Compiler off in tests**: accepted as non-blocking (see above).
3. **Two extra ESLint rules (`eslint.config.js:26-30`)**: accepted. `only-throw-error` allows exactly `@tanstack/router-core` `Redirect`, and lint passes, so the allow-list resolves. `aria-role ignoreNonDOM` affects only component props, not DOM `role`.
4. **TextField destructuring (`TextField.tsx:23-26`)**: accepted. It passes the same values, and rendered output is unchanged.
5. **Label `htmlFor` explicit (`label.tsx:6-7`)**: accepted. `LabelProps` is unchanged.
6. **No `Promise.withResolvers` (`Form.test.tsx:81-84`)**: accepted. It is ES2024 and the lib is ES2023.
7. **Pretty-printed `v1.json`**: accepted. I confirmed my `dotnet build api/` left `api/openapi/v1.json` byte-identical (sha1 `94f26c34…` before and after). The file is LF with no trailing newline.

## Verified
- **Reviewer re-runs:**
  - `npm --prefix web run build`: exit 0, `✓ built`.
  - `npm --prefix web test -- --run`: 15 files, 70 tests passed, exit 0.
  - `dotnet build api/`: Build succeeded, 0 warnings, 0 errors, exit 0.
  - `npm run lint`: exit 0.
  - `npm run format:check`: clean.
  - `vitest run --coverage`: exit 0. Branches 78.08% (253/324), lines 90.43% (312/345), which matches the report. The `src/features/**` threshold is met.
  - `dotnet test api/`: 14/14 passed.
  - `npm audit --audit-level=high`: 0 vulnerabilities.
- **Drift:**
  - `routeTree.gen.ts` and `v1.json` are unchanged after build (sha1 compared).
  - `npm run gen:tokens` reproduces `tokens.css` byte-identically.
  - `npm run gen:api` reproduces `generated/model/index.ts` with the same content, header only, LF. Regeneration touched its mtime only.
- **CI and root files:**
  - `.gitattributes` has exactly the 4 LF rules.
  - `web-ci.yml` matches Contract A3 step for step, including both grep patterns.
  - `api-ci.yml` has the drift step right after Build.
- **Packages and config:**
  - `package.json` scripts and every dependency pin match the Package set exactly (no caret ranges, no extras).
  - tsconfig, orval, components.json, prettier and .env.example match B-contracts. `.env.local` is gitignored (`.gitignore:41`).
- **Files:** every file in *Files to create* A–J exists, plus the 15 test files. Nothing else was added under `web/`. No `tailwind.config.*` exists.
- **Design system:**
  - `tokens.css` output conforms to Contract C1.
  - `app.css` resets the default scales and maps only `var(--ds-*)`. The only literals are font family names.
  - No hex, px, rem, arbitrary value, palette colour or `style=` exists outside `tokens.css`. Both A3 step-9 greps are clean.
- **Code bans:** none of `forwardRef`, `.Provider`, `useMemo`/`useCallback`/`memo`, `any`, non-null `!`, `@ts-ignore`, `console.log`, default exports, `dark:` or storage APIs appear in `web/src`. `fetch` appears only in `shared/lib/http.ts`.
- **Guards and session:**
  - Guards appear only in the 3 role `route.tsx` files, `login.tsx` and `index.tsx`.
  - The session store is memory-only.
  - Sign-out clears the query client.
  - The redirect schema blocks absolute and protocol-relative URLs.
- **i18n and RTL:**
  - `<html lang="ar" dir="rtl">` is set at boot.
  - `applyDocumentLanguage` is wired to `languageChanged`.
  - `Direction.DirectionProvider` wraps the app.
  - `ar-EG` digits come through both `format.ts` and ICU `parseLngForICU`.
  - Fonts come from `@fontsource` only.
  - Every i18n key in the plan table exists in both `ar` and `en` with the specified values.
  - The MorePage chevron has `rtl:rotate-180`.
- **Nav config:** it matches the Nav table in the plan (keys, paths, icons, tab-bar keys, `morePath`).
- **Docs:**
  - README, `docs/design-system.md` §5.7/§10 and `.claude/design-system.md` (TabBar row and mapping bullet) are updated as specified.
  - `docs/constitution.md` §5 agrees.
- **Postman:** there are no new, changed or removed endpoints, so `postman/elmanhg.postman_collection.json` correctly needs no change.

## Test quality
- **generateTokensCss (C3):** constrains the parser well. Each rule has a distinct fixture, and the parenthetical test asserts that `29px` is absent.
- **parseEnv:** good. It covers valid, empty and invalid inputs for both variables.
- **i18n:** good. The ICU test fails without `parseLngForICU` (Node `ar` gives Latin digits).
- **format / cn:**
  - The `cn` "keeps a font-size token" test fails without the `extendTailwindMerge` config, which is good.
  - "lets the later colour win" would also pass with stock `twMerge`. That is acceptable as a sanity check.
- **http:** good. Each branch removal (Accept-Language, 204, abort passthrough, comma split) flips a test.
- **Button:**
  - The type-default test constrains the code.
  - **The asChild test does not (Blocking #1).**
- **Form base components:** strong. They check `aria-describedby` through `toHaveAccessibleDescription`, focus on the first invalid field, focus on the server-mapped field, the disabled-while-pending state, and the root alert fallback.
- **RouteError:** good. It covers code-to-message mapping, unknown-code fallback and retry recovery.
- **createSessionStore / loginSearchSchema:** constrain the code.
- **DevSignInPage:** good. The redirect-return test would fail if `search.redirect` were dropped, because the user would land on "Dashboard" rather than "Users".
- **AppShell / MorePage:** good. They assert order, More presence and absence, `aria-current` with exact matching, the three guard directions, the RTL render and axe.
- **createAppRouter:** good.
