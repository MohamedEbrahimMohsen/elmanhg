# Plan: Bootstrap frontend app (React, TypeScript, Vite, RTL) (#55, E1.S2)

## Goal
After this ships, a developer can run `web/` (React 19 + Vite 8 + TS strict) and get an Arabic-first RTL SPA using the Glass tokens (generated from `.claude/design-system.md`) and self-hosted Readex Pro / Noto Sans Arabic fonts. It includes i18next (Arabic default, Arabic-Indic digits), TanStack Query, and an Orval client generated from the API's committed OpenAPI document. It also ships RHF + Zod base form components, and Student / Teacher / Admin shells. The shells are guarded by role against a stubbed in-memory session, with a dev sign-in page that #56 replaces. CI builds, lints, tests and drift-checks the web app, and fails when the API's OpenAPI document or the generated client is stale.

## Scope
**In:**
- All 5 story sub-tasks.
- Build-time OpenAPI document for `api/` (needed so Orval has a spec file).
- New `web-ci.yml`, plus a drift step in `api-ci.yml`.
- Docs sync (README, `docs/design-system.md`, `.claude/design-system.md`).

**Out:**
- Real sign-in/sign-up/OTP screens, auth headers, CSRF header, refresh (#56).
- Hiding navigation by permission (role-authorisation story).
- Every real screen: the shells host placeholder pages only.
- Landing page.
- Language-switch UI.
- Query/router devtools.
- `tw-animate-css`.
- Playwright E2E. There is no critical journey yet.

**Deferred:**
- **Browser-mode visual tests** (`toMatchScreenshot` at 390/1280 for Button/Input/TabBar, required by react-testing.md "Design-system component"). Screenshot baselines are OS-specific. This machine is Windows and CI is ubuntu-latest, so it cannot produce the baselines CI compares against. That needs a Linux baseline-generation job. Orchestrator: open an issue.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Router | TanStack Router v1, file-based (`src/routes/`), `@tanstack/router-plugin` with `autoCodeSplitting` | Skill §10: new app uses TanStack Router |
| 2 | Folder layout: §1 `src/components/ui` or §7 `src/shared/ui` | §7 new-app layout: `src/app`, `src/routes`, `src/features`, `src/shared/{ui,lib,api/generated,form,components,i18n}`, `src/styles`, `src/test` | §7 is the new-app layout. `components.json` aliases point at it |
| 3 | Run the shadcn CLI? | No. Commit `components.json` (style `radix-nova`, `rtl: true`, aliases → `@/shared/*`) so later `shadcn add` works. Hand-author `button`/`input`/`label`/`toaster` in the shadcn pattern (cva + `cn` + `radix-ui` Slot) on Glass tokens only | Verified: `shadcn@4.21.0 add` emits arbitrary values (`text-[0.8rem]`, `rounded-[min(...)]`), `dark:` variants and `h-8` buttons, all blocking per the design system. It also added the bogus deps `cn` and `next-themes` to package.json |
| 4 | TypeScript version | `typescript@6.0.3` | `typescript-eslint@8.70.1` peer is `<6.1.0`. Skill: typecheck with TS 6 |
| 5 | ESLint version | `eslint@9.39.5` + `@eslint/js@9.39.5`. The npm "deprecated" warning is accepted | `eslint-plugin-jsx-a11y@6.10.2` (latest) peers `eslint ≤9` |
| 6 | Vitest version | `vitest@4.1.11`, `@vitest/coverage-v8@4.1.11` | react-testing.md pins Vitest 4. Latest is 5.0.2 |
| 7 | ICU | `i18next-icu@2.4.4` + `intl-messageformat@11.2.15` | i18next-icu peer `intl-messageformat <12` |
| 8 | React Compiler wiring | `import babel from '@rolldown/plugin-babel'` (default export) and `babel({ presets: [reactCompilerPreset()] })`, placed before `react()` | Verified against the installed `@rolldown/plugin-babel@0.2.4` types. The skill §7 snippet (`{ babel }`, `babelConfig`) does not match the package |
| 9 | Axe assertions | `expect((await axe(container)).violations).toEqual([])`, using `axe` from `src/test/axe.ts` (`configureAxe` with `color-contrast` disabled) | Verified: `vitest-axe@0.1.0`'s `toHaveNoViolations` has no Vitest 4 typing (TS2339). Contrast is verified on tokens in design-system.md. jsdom has no canvas |
| 10 | Where Orval reads the spec | `Microsoft.Extensions.ApiDescription.Server@10.0.9` build-time generation writes `api/openapi/v1.json` on every `dotnet build`. The file is committed. api-ci fails if it is stale. Orval input is `../api/openapi/v1.json` | Verified in a scratch copy: Release build with no appsettings emits `{"openapi":"3.1.1",…,"paths":{}}` (LF, no trailing newline). A committed file lets CI run `gen:api` + drift check with no running API |
| 11 | Empty spec today | Keep full Orval config. Today it emits only `src/shared/api/generated/model/index.ts` | Verified. Also verified with a 1-operation sample spec: `tags-split` hooks, `.msw.ts` and `.zod.ts` generate and typecheck against the `http` mutator |
| 12 | Orval fetch return shape | `override.fetch.includeHttpResponseReturnType: false` | Hooks return the body `T`, and the mutator returns `Promise<T>` (verified generated signature `getX(id, options?: Parameters<typeof http>[1]): Promise<X>`) |
| 13 | `@faker-js/faker` now | Add `@faker-js/faker@10.6.0` (dev) | Orval MSW mocks import it. Verified that tsc fails without it as soon as the first endpoint exists |
| 14 | Server error shape | The API returns `{ code, message, data }` camelCase (`CoreExceptionMiddleware`). Validation failures are 422 with comma-joined codes (`ValidationBehaviourException`). There are no per-field errors. `ApiError { status, code, codes, message }`. Forms map codes to fields through a per-form `serverErrorFields` map. Unmapped codes go to `root.server` | Matches the real API. The skill's `fieldErrors` has no source |
| 15 | Arabic-Indic digits | i18next language code `ar`. i18next-icu `parseLngForICU: (lng) => numberLocale(lng, 'arabic-indic')` gives `ar-EG`. `format.ts` takes `digits: 'arabic-indic' \| 'latin'` (`ar-EG` / `ar-EG-u-nu-latn`) | Verified on Node 24.19: `Intl.NumberFormat('ar')` gives Latin `1,234.5`, while `ar-EG` gives `١٬٢٣٤٫٥`. ICU plural `few` gives `٣ أسئلة` |
| 16 | Language detection | None. Boot language is `ar`. `en` resources exist for every key (skill §2) but there is no switcher UI | PRD: Arabic UI. The prototype has no switcher |
| 17 | Stub session | Module store (`createSessionStore`, read with `useSyncExternalStore`), passed in router context. Memory only, never storage. Initial value comes from `VITE_DEV_SESSION_ROLE` (empty means signed out). `/login` is a dev sign-in page with 3 buttons (the equivalent of the prototype's role switcher), using the prototype seed users أحمد / أ. محمد / المدير. #56 replaces the page and the store source | Guards need a synchronous, always-current session in `beforeLoad`. React state lags a render behind |
| 18 | Guard mechanics | Each role layout route calls `requireRole(...)` in `beforeLoad`. `createAppRouter` subscribes to the store and calls `router.invalidate()` on change, so sign-in and sign-out re-run guards. `/login` `beforeLoad` redirects a signed-in user to `search.redirect ?? roleHome[role]` via `redirect({ href })`. The sign-in page only sets the store and never navigates | One navigation path, no race. Verified that `redirect({ href: '/admin/users?x=1' })` stays in-SPA with memory history |
| 19 | Open redirect | `loginSearchSchema.redirect` must start with `/` and not `//`. Otherwise `.catch(undefined)` drops it | Skill §19 |
| 20 | Mobile nav when a role has more than 4 destinations | Bottom TabBar has at most 4 items. The student (5) and admin (7) bars show 3 primary items plus "المزيد", which links to `/<role>/more` and lists the rest. The teacher bar shows its 3 items. Desktop (`lg` ≥ 900) TopTabs show every item in prototype order. Update both design-system docs | design-system says "4 items". The prototype NAV has 5/3/7. This adds a product-visible nav rule, so it gets a docs update and the dev should confirm it at the plan gate |
| 21 | Placeholder routes | One route file per prototype NAV destination, rendering `PlaceholderPage` (h1 = nav label + "under construction" line) | Typed `<Link to>` needs real routes. Later stories replace each file's component |
| 22 | TabBar label size | `text-micro` (12px) instead of the brief's 11px | There is no 11px token (rule 7). The `.claude/design-system.md` TabBar row is updated |
| 23 | Primary button hover | `hover:opacity-90` | The brief's `#000` is not a token |
| 24 | Fonts | `@fontsource/readex-pro` 500/600/700 and `@fontsource/noto-sans-arabic` 400/500/600, imported in `main.tsx` | Self-hosted per the design system. No Google Fonts link |
| 25 | Tokens | `web/scripts/tokens/generateTokensCss.ts` (pure) + `cli.ts` (`npm run gen:tokens`) parses the 5 tables of `.claude/design-system.md`. Composite tokens are split (see Contract 18). `bp.*` go into `@theme` as `--breakpoint-*`, because CSS vars do not work in media queries. `app.css` resets the Tailwind default colour/text/radius/shadow/font scales so only tokens exist | Skill §1: "one generated file, never hand-edit". Verified: after the reset, `bg-red-500` and `text-sm` generate nothing, while `bg-transparent`, `rounded-full` and `px-4.5` still work. `lg:` compiles to `(width>=900px)` |
| 26 | `tailwind-merge` config | `extendTailwindMerge({ extend: { theme: { text: [the 10 type names], shadow: ['1','2'] } } })` | Verified: the default `twMerge('text-ui text-text')` drops `text-ui` |
| 27 | CI placement | New `.github/workflows/web-ci.yml`, plus one step in `api-ci.yml` | Workflow `paths` filters are workflow-level. Merging the two would run the .NET job on every web change |
| 28 | API reachability in dev | The API has no CORS. The Vite dev server proxies `/api` to `API_PROXY_TARGET`. `VITE_API_BASE_URL` empty means same origin | Avoids an API change |
| 29 | `Accept-Language` source in `http` | `document.documentElement.lang` (set by `applyDocumentLanguage`), omitted when empty | `shared/` must not import `app/i18n`. The API localizes error messages (`UseCoreLocalization`) |
| 30 | Generated artefacts in git | Commit `src/routeTree.gen.ts`, `src/styles/tokens.css`, `src/shared/api/generated/**` and `api/openapi/v1.json`. `.gitattributes` forces LF on them. CI uses `git status --porcelain` drift checks | `tsc -b` runs before `vite build` on a fresh clone. `autocrlf=true` on this machine |
| 31 | Reuse-first | New, with no Morabh equivalent. Morabh (`D:\Personal\Morabh\repos\apis`) has no frontend and no build-time OpenAPI | Checked |
| 32 | Seed display names in `devSessions` | Literal Arabic names (أحمد, أ. محمد, المدير) | This is prototype seed data (`prototype/data.js`), not UI copy |

### Package set (all verified with `npm view`; exact pins, no `^`)
**dependencies:**
- `react@19.3.0`, `react-dom@19.3.0`
- `@tanstack/react-query@5.104.0`, `@tanstack/react-router@1.170.40`
- `react-hook-form@7.89.0`, `zod@4.6.5`, `@hookform/resolvers@5.9.1`
- `i18next@26.4.2`, `react-i18next@17.0.15`, `i18next-icu@2.4.4`, `intl-messageformat@11.2.15`
- `@fontsource/readex-pro@5.3.0`, `@fontsource/noto-sans-arabic@5.3.0`
- `lucide-react@1.48.0`
- `class-variance-authority@0.7.1`, `clsx@2.1.1`, `tailwind-merge@3.7.0`
- `radix-ui@1.6.7`, `sonner@2.0.8`

**devDependencies:**
- Build: `vite@8.3.1`, `@vitejs/plugin-react@6.1.1`, `@rolldown/plugin-babel@0.2.4`, `@babel/core@7.29.7`, `babel-plugin-react-compiler@1.0.0`, `@tanstack/router-plugin@1.168.41`
- TypeScript: `typescript@6.0.3`, `@types/react@19.3.0`, `@types/react-dom@19.3.0`, `@types/node@24.19.0`
- Tailwind: `tailwindcss@4.3.3`, `@tailwindcss/vite@4.3.3`
- Test: `vitest@4.1.11`, `@vitest/coverage-v8@4.1.11`, `@testing-library/react@16.3.3`, `@testing-library/dom@10.4.2`, `@testing-library/user-event@14.6.7`, `@testing-library/jest-dom@7.0.1`, `jsdom@30.1.1`, `msw@2.15.0`, `vitest-axe@0.1.0`
- API client: `orval@8.38.0`, `@faker-js/faker@10.6.0`
- Lint and format: `eslint@9.39.5`, `@eslint/js@9.39.5`, `typescript-eslint@8.70.1`, `eslint-plugin-react-hooks@7.1.1`, `eslint-plugin-jsx-a11y@6.10.2`, `@tanstack/eslint-plugin-query@5.104.0`, `globals@17.12.0`, `prettier@3.9.9`, `prettier-plugin-tailwindcss@0.8.1`

This full set installed cleanly in scratch with `npm audit`: 0 vulnerabilities. npm 11 prints `allow-scripts` warnings for `esbuild` and `msw` postinstall; these are expected and not failures. Do not approve the scripts.

## Existing code touched
| File | Change |
|------|--------|
| `api/Directory.Packages.props` | After the `Microsoft.OpenApi` line add `<PackageVersion Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.9" />` |
| `api/Elmanhg.Api/Elmanhg.Api.csproj` | First child of `<Project>`: `<PropertyGroup><OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../openapi</OpenApiDocumentsDirectory><OpenApiGenerateDocumentsOptions>--file-name v1</OpenApiGenerateDocumentsOptions></PropertyGroup>`. In the package ItemGroup, after `Microsoft.OpenApi`: `<PackageReference Include="Microsoft.Extensions.ApiDescription.Server"><PrivateAssets>all</PrivateAssets><IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets></PackageReference>` (same shape as the existing EF Design reference) |
| `.github/workflows/api-ci.yml` | After step `Build`, insert step `name: OpenAPI document is up to date`, `run: test -z "$(git status --porcelain api/openapi)"` |
| `README.md` | Folders table: add rows `web/` ("React 19 + Vite 8 SPA: TypeScript strict, Tailwind v4 Glass tokens, TanStack Router/Query, i18next Arabic RTL. API client generated by Orval.") and `api/openapi/` ("OpenAPI document regenerated on every `dotnet build`; commit it."). After "Run the backend locally" add section **Run the frontend locally** with the block: `cd web` · `cp .env.example .env.local` · `npm ci` · `npm run dev   # http://localhost:5173, /api proxied to the API` · `npm test -- --run` · `npm run gen:api   # after API changes (api/openapi/v1.json)` · `npm run gen:tokens   # after .claude/design-system.md changes` |
| `docs/design-system.md` | §5.7: replace the "Mobile: bottom tab bar, 4 items" sentence with: "Mobile: bottom tab bar, at most 4 items, icons 22 px stroke 1.8, active in `--text` 600, inactive `--text-2`. A role with more than 4 destinations shows its 3 primary destinations plus a fourth item "المزيد" that opens a list of the rest. Desktop (≥ 900 px): top bar with every destination as text tabs, active underlined 2 px `--accent`." §10: replace bullet 1 ("Tailwind config: map every token…") with: "Tailwind CSS v4, CSS-first: `web/src/styles/tokens.css` is generated from `.claude/design-system.md` by `npm run gen:tokens` (never hand-edited) and mapped to utilities with `@theme inline` in `web/src/styles/app.css`. There is no `tailwind.config.*`." Replace bullet 3 with: "The prototype in `prototype/` remains the reference for screen content and flow." |
| `.claude/design-system.md` | Components table, TabBar row, Notes: "at most 4 items; a role with more destinations shows 3 + "المزيد" (list of the rest); Lucide icons 22px stroke 1.8, label type.micro, surface + top hairline". Token → code mapping bullet 1 becomes: "`src/styles/tokens.css` is generated from the tables above by `npm --prefix web run gen:tokens`: every token becomes `--ds-<group>-<name>` on `:root` (dots → dashes). Composite values split: typography → `-size`, `-line`, `-size-desktop`, `-line-desktop`, `-weight`, `-tracking`; motion → `-duration`, `-easing`; `N mobile · M desktop` → base + `-desktop`. `bp.*` → `--breakpoint-*` inside `@theme`. No `.dark` block." Leave the `app.css` sample as is (the real file is stricter: vars instead of literals) |

## Files to create

Paths are relative to the repo root. `@/` = `web/src/`. Every TS/TSX file uses named exports only (route files export `Route`), no `any`, no `!`. All user-visible strings go through `t()`.

### A. Repo root and API
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `.gitattributes` | git | Exactly 4 lines: `api/openapi/*.json text eol=lf`, `web/src/shared/api/generated/** text eol=lf`, `web/src/routeTree.gen.ts text eol=lf`, `web/src/styles/tokens.css text eol=lf` |
| A2 | `api/openapi/v1.json` | generated | Produced by `dotnet build api/`. Never hand-written. Expected content today: `openapi 3.1.1`, title `Elmanhg.Api \| v1`, `paths: {}` |
| A3 | `.github/workflows/web-ci.yml` | CI | See Contract A3 below |

**Contract A3: `web-ci.yml`**
- `name: web-ci`.
- `on.pull_request.paths` and `on.push` (`branches: [main]`) paths: `['web/**', 'api/openapi/**', '.claude/design-system.md', '.gitattributes', '.github/workflows/web-ci.yml']`.
- `permissions: contents: read`.
- One job `build-test`, `runs-on: ubuntu-latest`, `defaults.run.working-directory: web`, `env: TZ: UTC`.
- Steps, in order:
  1. `actions/checkout@v4`
  2. `actions/setup-node@v4` with `node-version-file: web/.nvmrc`, `cache: npm`, `cache-dependency-path: web/package-lock.json`
  3. `Install`: `npm ci`
  4. `Design tokens are up to date`: `npm run gen:tokens && test -z "$(git status --porcelain src/styles/tokens.css)"`
  5. `API client is up to date`: `npm run gen:api && test -z "$(git status --porcelain src/shared/api/generated)"`
  6. `Typecheck`: `npm run typecheck`
  7. `Lint`: `npm run lint`
  8. `Format`: `npm run format:check`
  9. `No literal tokens or physical directions`: a `run: |` block containing two `if grep -rnE --include='*.ts' --include='*.tsx' --exclude-dir=generated '<pattern>' src; then exit 1; fi` lines:
     - pattern 1: `\b(ml|mr|pl|pr|left|right)-[0-9]|\btext-(left|right)\b`
     - pattern 2: `#[0-9a-fA-F]{3,8}\b|\[[0-9.]+(px|rem)\]|\b(bg|text|border|ring)-(slate|gray|zinc|neutral|red|orange|amber|yellow|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|pink|rose)-[0-9]{2,3}\b`
  10. `Test`: `npm test -- --run --coverage`
  11. `Build`: `npm run build`
  12. `Route tree is up to date`: `test -z "$(git status --porcelain src/routeTree.gen.ts)"`
  13. `Vulnerable packages`: `npm audit --audit-level=high`

### B. `web/` configuration
| # | Path | Contract |
|---|------|----------|
| B1 | `web/package.json` | See Contract B1 below |
| B2 | `web/package-lock.json` | Produced by `npm install` from B1. Committed |
| B3 | `web/.nvmrc` | `24` |
| B4 | `web/.env.example` | See Contract B4 below |
| B5 | `web/index.html` | `<!doctype html><html lang="ar" dir="rtl">`. Head: `<meta charset="UTF-8" />`, `<meta name="viewport" content="width=device-width, initial-scale=1" />`, `<title>المنهج</title>`. Body: `<div id="root"></div><noscript>يتطلب هذا التطبيق تفعيل JavaScript.</noscript><script type="module" src="/src/main.tsx"></script>` |
| B6 | `web/vite.config.ts` | See Contract B6 below |
| B7 | `web/tsconfig.json` | `{ "files": [], "references": [{ "path": "./tsconfig.app.json" }, { "path": "./tsconfig.node.json" }], "compilerOptions": { "paths": { "@/*": ["./src/*"] } } }`. The root `paths` is required, or `shadcn` cannot resolve aliases (verified) |
| B8 | `web/tsconfig.app.json` | `compilerOptions`: `tsBuildInfoFile ./node_modules/.tmp/tsconfig.app.tsbuildinfo`, `target ES2023`, `lib [ES2023, DOM, DOM.Iterable]`, `module ESNext`, `types ["vite/client"]`, `moduleResolution bundler`, `allowImportingTsExtensions true`, `resolveJsonModule true`, `verbatimModuleSyntax true`, `moduleDetection force`, `noEmit true`, `jsx react-jsx`, `strict true`, `noUncheckedIndexedAccess true`, `exactOptionalPropertyTypes true`, `noUnusedLocals true`, `noUnusedParameters true`, `noFallthroughCasesInSwitch true`, `skipLibCheck true`, `paths { "@/*": ["./src/*"] }`. `include ["src"]` |
| B9 | `web/tsconfig.node.json` | Same strict flags. Also `target ES2023`, `lib [ES2023]`, `module ESNext`, `types ["node"]`, `moduleResolution bundler`, `allowImportingTsExtensions true`, `erasableSyntaxOnly true`, `tsBuildInfoFile ./node_modules/.tmp/tsconfig.node.tsbuildinfo`. `include ["vite.config.ts", "orval.config.ts", "scripts"]` |
| B10 | `web/eslint.config.js` | See Contract B10 below |
| B11 | `web/.prettierrc.json` | `{ "singleQuote": true, "printWidth": 120, "plugins": ["prettier-plugin-tailwindcss"], "tailwindStylesheet": "./src/styles/app.css", "tailwindFunctions": ["cva", "cn"] }` |
| B12 | `web/.prettierignore` | Lines: `dist`, `coverage`, `node_modules`, `package-lock.json`, `src/shared/api/generated`, `src/routeTree.gen.ts`, `src/styles/tokens.css` |
| B13 | `web/orval.config.ts` | See Contract B13 below |
| B14 | `web/components.json` | `{ "$schema": "https://ui.shadcn.com/schema.json", "style": "radix-nova", "rsc": false, "tsx": true, "tailwind": { "config": "", "css": "src/styles/app.css", "baseColor": "neutral", "cssVariables": true, "prefix": "" }, "iconLibrary": "lucide", "rtl": true, "aliases": { "components": "@/shared/components", "utils": "@/shared/lib/utils", "ui": "@/shared/ui", "lib": "@/shared/lib", "hooks": "@/shared/hooks" }, "registries": {} }` |

**Contract B1: `web/package.json`**
- `"name": "elmanhg-web"`, `"private": true`, `"version": "0.0.0"`, `"type": "module"`, `"engines": { "node": ">=22.18.0" }`.
- scripts:
  - `dev`: `vite`
  - `build`: `tsc -b && vite build`
  - `preview`: `vite preview`
  - `typecheck`: `tsc -b`
  - `lint`: `eslint . --max-warnings=0`
  - `format`: `prettier --write .`
  - `format:check`: `prettier --check .`
  - `test`: `vitest`
  - `gen:api`: `orval --config orval.config.ts`
  - `gen:tokens`: `node scripts/tokens/cli.ts`
- dependencies and devDependencies: exactly the Package set above.

**Contract B4: `web/.env.example`** (lines in this order)
1. `# Copy to .env.local (gitignored). Every VITE_* value ships to the browser: never put secrets here.`
2. `# Absolute API origin; empty = same origin (dev server proxies /api).`
3. `VITE_API_BASE_URL=`
4. `# student | teacher | admin signs the stub session in at startup until real sign-in (#56). Empty = signed out.`
5. `VITE_DEV_SESSION_ROLE=student`
6. `# Dev-server proxy target for /api (read by vite.config.ts only, not shipped).`
7. `API_PROXY_TARGET=http://localhost:5080`

**Contract B6: `web/vite.config.ts`**
- First line: `/// <reference types="vitest/config" />`.
- Imports: `defineConfig`, `loadEnv` from `vite`; `react, { reactCompilerPreset }` from `@vitejs/plugin-react`; `babel` (default) from `@rolldown/plugin-babel`; `tailwindcss` from `@tailwindcss/vite`; `{ tanstackRouter }` from `@tanstack/router-plugin/vite`.
- `export default defineConfig(({ mode }) => { … })`. Inside:
  - `const proxyTarget = loadEnv(mode, import.meta.dirname, '').API_PROXY_TARGET`
  - `plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), babel({ presets: [reactCompilerPreset()] }), react(), tailwindcss()]`
  - `resolve: { tsconfigPaths: true }`
  - `server: { ...(proxyTarget ? { proxy: { '/api': { target: proxyTarget, changeOrigin: true } } } : {}) }`
  - `test`:
    - `environment: 'jsdom'`
    - `setupFiles: ['./src/test/setup.ts']`
    - `include: ['src/**/*.test.{ts,tsx}', 'scripts/**/*.test.ts']`
    - `coverage: { provider: 'v8', include: ['src/**', 'scripts/**'], exclude: ['src/shared/api/generated/**', 'src/routeTree.gen.ts', 'src/test/**', 'src/main.tsx', '**/*.test.{ts,tsx}', '**/*.d.ts'], thresholds: { 'src/features/**': { lines: 80, branches: 70 } } }`

**Contract B10: `web/eslint.config.js`**
- `export default tseslint.config(…)` with two blocks:
  1. `{ ignores: ['dist', 'coverage', 'src/shared/api/generated', 'src/routeTree.gen.ts'] }`
  2. `{ files: ['**/*.{ts,tsx}'], … }` with:
     - `extends: [js.configs.recommended, ...tseslint.configs.strictTypeChecked, ...tseslint.configs.stylisticTypeChecked, reactHooks.configs.flat.recommended, jsxA11y.flatConfigs.strict, ...pluginQuery.configs['flat/recommended']]`
     - `languageOptions: { globals: globals.browser, parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname } }`
     - `rules: { '@typescript-eslint/no-floating-promises': 'error', 'no-restricted-syntax': ['error', { selector: "JSXAttribute[name.name='dangerouslySetInnerHTML']", message: 'Raw HTML only through SafeHtml (DOMPurify).' }] }`
- This shape loaded and linted clean in scratch. Do **not** reference `react/*` rules: `eslint-plugin-react` is not installed.

**Contract B13: `web/orval.config.ts`**
- `import { defineConfig } from 'orval'; export default defineConfig({ api: {...}, apiZod: {...} })`.
- `api`:
  - `input: { target: '../api/openapi/v1.json' }`
  - `output`:
    - `mode: 'tags-split'`, `target: 'src/shared/api/generated'`, `schemas: 'src/shared/api/generated/model'`
    - `client: 'react-query'`, `httpClient: 'fetch'`, `clean: true`
    - `override: { mutator: { path: 'src/shared/lib/http.ts', name: 'http' }, fetch: { includeHttpResponseReturnType: false }, query: { useQuery: true, useSuspenseQuery: true, signal: true } }`
    - `mock: { generators: [{ type: 'msw' }] }`
- `apiZod`:
  - `input: { target: '../api/openapi/v1.json' }`
  - `output: { client: 'zod', mode: 'tags-split', target: 'src/shared/api/generated/zod', fileExtension: '.zod.ts' }`
- `clean` goes on `api` only: `api` runs first, and cleaning in `apiZod` would wipe its output.

### C. Token generator
| # | Path | Type | Contract |
|---|------|------|----------|
| C1 | `web/scripts/tokens/generateTokensCss.ts` | pure fn | See Contract C1 below |
| C2 | `web/scripts/tokens/cli.ts` | Node script | `import { generateTokensCss } from './generateTokensCss.ts'`. `webRoot = resolve(import.meta.dirname, '../..')`. Read `resolve(webRoot, '../.claude/design-system.md')` as utf8 and write `resolve(webRoot, 'src/styles/tokens.css')`. Runs under Node 24 type stripping (verified) |
| C3 | `web/scripts/tokens/generateTokensCss.test.ts` | test | First line `// @vitest-environment node`. See Test plan |
| C4 | `web/src/styles/tokens.css` | generated | Output of `npm run gen:tokens` over the real `.claude/design-system.md`. Never hand-edited |

**Contract C1: `generateTokensCss.ts`**

`export function generateTokensCss(markdown: string): string`

Parsing:
- Split on `/\r?\n/`.
- A **section** runs from a heading line to the next line starting with `#`.
- Table rows are lines starting with `|`. Skip the first row (header) and rows matching `/^\|\s*-/` (separator).
- Cells = `line.split('|').slice(1, -1).map(trim)`.
- Required sections, matched by heading prefix:
  - `### Colour`
  - `### Typography`
  - `### Spacing`
  - `### Radius`
  - `## Breakpoints`
- A missing section throws `new Error(\`design-system: section "${prefix}" not found\`)`.
- `name(token)` = `--ds-` + token with `.` replaced by `-`.

Per-section rules:
- **Colour**: `${cells[1]}: ${cells[2]};` (CSS var column + value, verbatim).
- **Typography** (cells: token, family, size, weight):
  - Strip `\s*\([^)]*\)` from the size and weight cells.
  - Match the size against `/^(\d+)\/(\d+)(?:\s*·\s*(\d+)\/(\d+))?(?:,\s*tracking\s+(\S+))?/`.
  - Emit `${n}-size: Apx`, `${n}-line: Bpx`.
  - If a desktop pair is present, also emit `${n}-size-desktop: Cpx`, `${n}-line-desktop: Dpx`.
  - Emit `${n}-weight:` (first integer of the weight cell).
  - If tracking is present, emit `${n}-tracking: X`.
- **Spacing** (token, value):
  - Strip parentheticals.
  - Match `/^(\d+)(?:\s*mobile\s*·\s*(\d+)\s*desktop)?/`.
  - Emit `${n}: Apx`, plus `${n}-desktop: Bpx` when present.
- **Radius** (token, value), by token prefix:
  - `radius.*`: first integer, emitted as px.
  - `shadow.*`: text before ` — `, trimmed.
  - `motion.*`: text before ` — `, split at the first space. Emit `${n}-duration: <first>` and `${n}-easing: <rest>`.
- **Breakpoints**: rows whose token starts with `bp.` and whose value matches `/^≥(\d+)$/`. `bp.base` and `layout.max` are skipped.

Output, joined with `\n`, ending with `\n`:
1. The line `/* Generated by web/scripts/tokens/cli.ts from .claude/design-system.md. Do not edit. */`
2. `:root {`
3. Every declaration as `  <var>: <value>;`, in section order Colour, Typography, Spacing, Radius, in row order.
4. `}`
5. A blank line.
6. `@theme {`
7. `  --breakpoint-*: initial;`
8. One `  --breakpoint-<name>: Npx;` per bp row.
9. `}`

### D. Styles and entry
| # | Path | Type | Contract |
|---|------|------|----------|
| D1 | `web/src/styles/app.css` | CSS | See Contract D1 below |
| D2 | `web/src/vite-env.d.ts` | types | `/// <reference types="vite/client" />` and `interface ImportMetaEnv { readonly VITE_API_BASE_URL?: string; readonly VITE_DEV_SESSION_ROLE?: string }` |
| D3 | `web/src/main.tsx` | entry | See Contract D3 below |

**Contract D1: `app.css`** (in this order)
1. `@import "tailwindcss";` and `@import "./tokens.css";`
2. `@theme { --color-*: initial; --shadow-*: initial; --font-*: initial; --text-*: initial; --radius-*: initial; }`
3. `@theme inline { … }` containing:
   - **Colours:**
     - `--color-{bg,surface,soft,text,text-muted,border,border-strong,accent,accent-soft,success,success-soft,danger,danger-soft,warning,warning-soft,v2,overlay}: var(--ds-color-<same>)`
     - `--color-background: var(--background)`, `--color-foreground: var(--foreground)`
     - `--color-primary: var(--primary)`, `--color-primary-foreground: var(--primary-foreground)`
     - `--color-ring: var(--ring)`, `--color-destructive: var(--destructive)`
     - `--color-card: var(--card)`, `--color-muted: var(--muted)`, `--color-muted-foreground: var(--muted-foreground)`, `--color-input: var(--input)`
   - **Fonts:** `--font-display: "Readex Pro", Tahoma, sans-serif`, `--font-sans: "Noto Sans Arabic", Tahoma, sans-serif`, `--font-mono: ui-monospace, monospace`
   - **Radius:** `--radius-{sm,md,lg,pill}: var(--ds-radius-<same>)`
   - **Shadows:** `--shadow-1: var(--ds-shadow-1)`, `--shadow-2: var(--ds-shadow-2)`
   - **Layout:** `--spacing: var(--ds-space-1)`, `--container-layout: var(--ds-layout-max)`
   - **Type scale:** for each of `display, h1, h2, h3, body, ui, caption, micro, stat, mono`:
     - `--text-<t>: var(--ds-type-<t>-size)`
     - `--text-<t>--line-height: var(--ds-type-<t>-line)`
     - where a tracking token exists (display, stat): `--text-<t>--letter-spacing: var(--ds-type-<t>-tracking)`
     - for `display, h1, h2` also `--text-<t>-desktop: var(--ds-type-<t>-size-desktop)` and `--text-<t>-desktop--line-height: var(--ds-type-<t>-line-desktop)`
   - **Motion:** `--default-transition-duration: var(--ds-motion-fast-duration)`, `--default-transition-timing-function: var(--ds-motion-fast-easing)`
4. `:root { --radius: var(--ds-radius-md); --background: var(--ds-color-bg); --foreground: var(--ds-color-text); --primary: var(--ds-color-text); --primary-foreground: var(--ds-color-surface); --ring: var(--ds-color-accent); --destructive: var(--ds-color-danger); --card: var(--ds-color-surface); --muted: var(--ds-color-soft); --muted-foreground: var(--ds-color-text-muted); --border: var(--ds-color-border-strong); --input: var(--ds-color-border-strong); }`
5. `@utility bg-aurora { background-image: var(--ds-gradient-aurora); }`
6. `@layer base { … }` containing:
   - `html { scroll-padding-top: calc(var(--ds-space-12) * 2); }`
   - `body { background-color: var(--ds-color-bg); color: var(--ds-color-text); font-family: var(--font-sans); font-size: var(--ds-type-body-size); line-height: var(--ds-type-body-line); }`
   - `h1, h2, h3 { font-family: var(--font-display); }`
   - `@media (prefers-reduced-motion: reduce) { *, *::before, *::after { transition-duration: 0s !important; animation-duration: 0s !important; } }`

No literal colour, size or radius appears anywhere except the font family names.

**Contract D3: `main.tsx`**
1. Import `@fontsource/readex-pro/{500,600,700}.css`, `@fontsource/noto-sans-arabic/{400,500,600}.css`, and `./styles/app.css`.
2. `initI18n()`.
3. `const queryClient = createQueryClient()`.
4. `const sessionStore = createSessionStore(env.VITE_DEV_SESSION_ROLE ? devSessions[env.VITE_DEV_SESSION_ROLE] : null)`.
5. `const router = createAppRouter({ queryClient, sessionStore })`.
6. `const rootElement = document.getElementById('root')`. If null, `throw new Error('Root element #root not found')`.
7. `createRoot(rootElement).render(<StrictMode><AppProviders queryClient={queryClient} sessionStore={sessionStore}><RouterProvider router={router} /></AppProviders></StrictMode>)`.

### E. `src/app/`
| # | Path | Contract |
|---|------|----------|
| E1 | `env.ts` | `const envSchema = z.object({ VITE_API_BASE_URL: z.union([z.literal(''), z.url()]).default(''), VITE_DEV_SESSION_ROLE: z.preprocess((value) => (value === '' ? undefined : value), z.enum(roles).optional()) })`. `export type Env = z.infer<typeof envSchema>`. `export function parseEnv(source: Record<string, unknown>): Env` returns `envSchema.parse(source)`. `export const env = parseEnv(import.meta.env)`. `roles` is imported from `@/features/session` |
| E2 | `i18n.ts` | See Contract E2 below |
| E3 | `queryClient.ts` | `const defaultStaleTimeMs = 30_000`. `export function createQueryClient(): QueryClient` returns `new QueryClient({ defaultOptions: { queries: { staleTime: defaultStaleTimeMs, retry: 1 } } })` |
| E4 | `routerContext.ts` | `export interface RouterContext { queryClient: QueryClient; sessionStore: SessionStore }` |
| E5 | `router.tsx` | See Contract E5 below |
| E6 | `providers.tsx` | `export interface AppProvidersProps { queryClient: QueryClient; sessionStore: SessionStore; children: ReactNode }`. `export function AppProviders(...)`: `const { i18n: instance } = useTranslation()`, then render `<I18nextProvider i18n={i18n}><Direction.DirectionProvider dir={instance.dir(instance.resolvedLanguage)}><QueryClientProvider client={queryClient}><SessionContext value={sessionStore}>{children}<Toaster /></SessionContext></QueryClientProvider></Direction.DirectionProvider></I18nextProvider>`. `Direction` comes from `radix-ui`. Use React 19 `<Context value>`, not `.Provider` |

**Contract E2: `i18n.ts`**
- Exports:
  - `supportedLanguages = ['ar', 'en'] as const`
  - `type Language = (typeof supportedLanguages)[number]`
  - `defaultLanguage: Language = 'ar'`
  - `i18n = i18next.createInstance()`
- `const resources = { ar: { common: commonAr, session: sessionLocales.ar, shell: shellLocales.ar }, en: { … same for en } }`.
- `export function applyDocumentLanguage(): void`: `const language = i18n.resolvedLanguage ?? defaultLanguage`, then set `document.documentElement.lang = language` and `.dir = i18n.dir(language)`.
- `export function initI18n(lng: Language = defaultLanguage): void`:
  1. If `i18n.isInitialized`, call `void i18n.changeLanguage(lng)` and return.
  2. `void i18n.use(new ICU({ parseLngForICU: (code) => numberLocale(code, 'arabic-indic') })).use(initReactI18next).init({ lng, fallbackLng: defaultLanguage, supportedLngs: [...supportedLanguages], ns: ['common', 'session', 'shell'], defaultNS: 'common', resources, initAsync: false, interpolation: { escapeValue: false } })`
  3. `i18n.on('languageChanged', applyDocumentLanguage)`
  4. `applyDocumentLanguage()`

**Contract E5: `router.tsx`**
- `export interface CreateAppRouterOptions { queryClient: QueryClient; sessionStore: SessionStore; history?: RouterHistory }`.
- `export function createAppRouter({ queryClient, sessionStore, history }: CreateAppRouterOptions)`:
  - `const router = createRouter({ routeTree, context: { queryClient, sessionStore }, defaultPreload: 'intent', defaultPreloadStaleTime: 0, defaultErrorComponent: RouteError, defaultNotFoundComponent: NotFound, ...(history ? { history } : {}) })`
  - `sessionStore.subscribe(() => { void router.invalidate(); })`
  - return `router`
- `declare module '@tanstack/react-router' { interface Register { router: ReturnType<typeof createAppRouter> } }`

### F. `src/shared/`
| # | Path | Contract |
|---|------|----------|
| F1 | `lib/utils.ts` | `const twMerge = extendTailwindMerge({ extend: { theme: { text: ['display','display-desktop','h1','h1-desktop','h2','h2-desktop','h3','body','ui','caption','micro','stat','mono'], shadow: ['1','2'] } } })`. `export function cn(...inputs: ClassValue[]): string` returns `twMerge(clsx(inputs))` |
| F2 | `lib/format.ts` | See Contract F2 below |
| F3 | `lib/apiError.ts` | See Contract F3 below |
| F4 | `lib/http.ts` | Orval mutator. See Contract F4 below |
| F5 | `ui/button.tsx` | See Contract F5 below |
| F6 | `ui/input.tsx` | `export type InputProps = ComponentProps<'input'>`. `export function Input({ className, ...props }: InputProps)` renders `<input data-slot="input" className={cn('h-11 w-full rounded-sm border border-border-strong bg-surface px-3 text-ui text-text placeholder:text-text-muted focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 aria-invalid:border-danger disabled:opacity-45', className)} {...props} />` |
| F7 | `ui/label.tsx` | `export type LabelProps = ComponentProps<'label'>`. `export function Label({ className, ...props }: LabelProps)` renders `<label className={cn('text-caption text-text-muted', className)} {...props} />` (native, with jsx-a11y `label-has-associated-control` satisfied by `htmlFor`) |
| F8 | `ui/toaster.tsx` | `export function Toaster()`: `const { i18n } = useTranslation()`. Render sonner `<Toaster dir={i18n.dir(i18n.resolvedLanguage)} position="top-center" toastOptions={{ classNames: { toast: 'rounded-lg border border-border bg-surface font-sans text-ui text-text shadow-2' } }} />`. Import `Toaster as Sonner` from `sonner` |
| F9 | `form/applyServerErrors.ts` | See Contract F9 below |
| F10 | `form/Form.tsx` | See Contract F10 below |
| F11 | `form/TextField.tsx` | See Contract F11 below |
| F12 | `form/SubmitButton.tsx` | `export interface SubmitButtonProps { children: ReactNode; variant?: 'primary' \| 'accent' }`. `export function SubmitButton({ children, variant = 'primary' })`: `const { isSubmitting } = useFormState()`, then `<Button type="submit" variant={variant} disabled={isSubmitting} aria-busy={isSubmitting}>{children}</Button>` |
| F13 | `form/FormRootError.tsx` | `export function FormRootError()`: `const { t } = useTranslation()`, `const { errors } = useFormState()`, `const message = errors.root?.server?.message`. If there is no message, return null. Otherwise `<p role="alert" className="rounded-md border border-danger bg-danger-soft px-3.5 py-3 text-ui text-danger">{t([message, 'errors.UNHANDLED_EXCEPTION'])}</p>` |
| F14 | `components/NotFound.tsx` | `export function NotFound()`: `const { t } = useTranslation()`. Render `<section className="mx-auto max-w-layout px-4 py-6"><div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1"><h1 className="font-display text-h2 font-bold">{t('notFound.body')}</h1><Link to="/" className="text-ui font-semibold text-accent focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2">{t('notFound.back')}</Link></div></section>` |
| F15 | `components/RouteError.tsx` | `export function RouteError({ error, reset }: ErrorComponentProps)`: `const { t } = useTranslation()`, `const router = useRouter()`, `const code = error instanceof ApiError ? error.code : unhandledErrorCode`. Render `<section role="alert" className="mx-auto max-w-layout px-4 py-6"><div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1"><h1 className="font-display text-h2 font-bold">{t('error.title')}</h1><p className="text-ui text-text-muted">{t([\`errors.${code}\`, 'errors.UNHANDLED_EXCEPTION'])}</p><Button variant="secondary" onClick={() => { reset(); void router.invalidate(); }}>{t('actions.retry')}</Button></div></section>` |
| F16 | `i18n/ar.json`, `i18n/en.json` | `common` namespace. Keys are in the i18n table |
| F17 | `api/generated/model/index.ts` | Generated by `npm run gen:api` (header comment only, today) |

**Contract F2: `format.ts`**
- `export type DigitStyle = 'arabic-indic' | 'latin'`.
- `export function numberLocale(lng: string, digits: DigitStyle): string`:
  - `lng` starts with `ar`: `ar-EG` when digits is `arabic-indic`, else `ar-EG-u-nu-latn`.
  - otherwise: `en-US`.
- `export function formatNumber(value: number, lng: string, digits: DigitStyle = 'arabic-indic', options?: Intl.NumberFormatOptions): string` returns `new Intl.NumberFormat(numberLocale(lng, digits), options).format(value)`.
- `export function formatDate(value: Date, lng: string, digits: DigitStyle = 'arabic-indic', options?: Intl.DateTimeFormatOptions): string`: same pattern with `Intl.DateTimeFormat`.

**Contract F3: `apiError.ts`**
- `export const unhandledErrorCode = 'UNHANDLED_EXCEPTION'`, with comment `// mirrors Core.Exceptions.ExceptionErrorCodes.UnhandledException`.
- `export const networkErrorCode = 'NETWORK_ERROR'`.
- `export class ApiError extends Error`:
  - `constructor(readonly status: number, readonly codes: readonly string[], message: string)` calls `super(message)` and sets `this.name = 'ApiError'`.
  - `get code(): string` returns `this.codes[0] ?? unhandledErrorCode`.

**Contract F4: `http.ts`**
- `export function resolveApiUrl(path: string): string` returns `new URL(path, env.VITE_API_BASE_URL === '' ? window.location.origin : env.VITE_API_BASE_URL).toString()`.
- `const errorBodySchema = z.object({ code: z.string().optional(), message: z.string().optional() })`.
- `export async function http<T>(url: string, init: RequestInit = {}): Promise<T>`:
  1. `const headers = new Headers(init.headers)`, then `headers.set('Accept', 'application/json')`.
  2. `const language = document.documentElement.lang`. If `language !== ''`, `headers.set('Accept-Language', language)`.
  3. `let response: Response`, then `try { response = await fetch(resolveApiUrl(url), { ...init, headers, credentials: 'include' }) }`. In the catch:
     - if `error instanceof Error && error.name === 'AbortError'`, rethrow;
     - otherwise `throw new ApiError(0, [networkErrorCode], error instanceof Error ? error.message : '')`.
  4. If `!response.ok`, `throw await toApiError(response)`.
  5. If `response.status === 204`, `return undefined as T`.
  6. `return (await response.json()) as T`.
- Private `async function toApiError(response: Response): Promise<ApiError>`:
  - Inside `try`, parse `await response.json()` with `errorBodySchema.safeParse`.
  - `codes` = `parsed.data.code.split(',').map(trim).filter(non-empty)`. If that is empty or the parse failed, use `[unhandledErrorCode]`.
  - Return `new ApiError(response.status, codes, parsed.data?.message ?? '')`.
  - `catch`: return `new ApiError(response.status, [unhandledErrorCode], '')`.

**Contract F5: `button.tsx`**
- `export const buttonVariants = cva('inline-flex shrink-0 items-center justify-center gap-2 rounded-full font-sans text-ui font-semibold whitespace-nowrap transition-colors focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-45', { variants, defaultVariants })`.
- `variants.variant`:
  - `primary`: `bg-text text-surface hover:opacity-90`
  - `accent`: `bg-accent text-surface hover:opacity-90`
  - `secondary`: `border border-border-strong bg-surface text-text hover:bg-soft`
  - `danger`: `border border-danger bg-surface text-danger hover:bg-danger-soft`
  - `ghost`: `bg-transparent text-accent hover:bg-accent-soft`
- `variants.size`:
  - `default`: `min-h-11 px-4.5`
  - `sm`: `min-h-9 px-3`
- `defaultVariants: { variant: 'primary', size: 'default' }`.
- `export type ButtonProps = ComponentProps<'button'> & VariantProps<typeof buttonVariants> & { asChild?: boolean }`.
- `export function Button({ className, variant, size, asChild = false, type, ...props }: ButtonProps)`:
  - `const Comp = asChild ? Slot.Root : 'button'` (`Slot` from `radix-ui`).
  - Render `<Comp data-slot="button" className={cn(buttonVariants({ variant, size }), className)} {...(asChild ? {} : { type: type ?? 'button' })} {...props} />`.

**Contract F9: `applyServerErrors.ts`**
- `export type ServerErrorFields<TValues extends FieldValues> = Partial<Record<string, Path<TValues>>>`.
- `export function applyServerErrors<TValues extends FieldValues>(form: Pick<UseFormReturn<TValues>, 'setError'>, error: unknown, fields: ServerErrorFields<TValues>): void`. Steps:
  1. `codes = error instanceof ApiError ? error.codes : [unhandledErrorCode]`.
  2. For each `code`: if `fields[code]` exists, call `form.setError(field, { type: 'server', message: \`errors.${code}\` }, { shouldFocus: !focused })` and set `focused = true`. Otherwise push the code to `unmatched`.
  3. If `unmatched[0]` exists, call `form.setError('root.server', { type: 'server', message: \`errors.${unmatched[0]}\` })`.

**Contract F10: `Form.tsx`**
- `export interface FormProps<TValues extends FieldValues, TTransformed extends FieldValues = TValues> { form: UseFormReturn<TValues, unknown, TTransformed>; onSubmit: (values: TTransformed) => Promise<void> | void; serverErrorFields?: ServerErrorFields<TValues>; children: ReactNode; className?: string }`.
- `export function Form<…>(...)` renders:
  - `<FormProvider {...form}>`
  - containing `<form noValidate className={cn('flex flex-col gap-3', className)} onSubmit={(event) => { void form.handleSubmit(submit)(event); }}>{children}</form>`.
- `submit = async (values) => { try { await onSubmit(values); } catch (error) { applyServerErrors(form, error, serverErrorFields ?? {}); } }`.
- Client-side focus on the first invalid field is RHF's default `shouldFocusError`. Do not disable it.

**Contract F11: `TextField.tsx`**
- `export interface TextFieldProps<TValues extends FieldValues> { name: Path<TValues>; label: string; type?: 'text' | 'email' | 'tel' | 'password'; autoComplete?: string; description?: string }`.
- `export function TextField<TValues extends FieldValues>(...)`. Steps:
  1. `const { t } = useTranslation()` and `const { field, fieldState } = useController<TValues>({ name })`.
  2. `const id = useId()`, `descriptionId = \`${id}-description\``, `errorId = \`${id}-error\``.
  3. `describedBy` = the ids of the description (if given) and the error (if any), space-joined, or `undefined` when there are none.
  4. `const value: unknown = field.value`.
  5. Render `<div className="flex flex-col gap-1.5">` containing:
     - `<Label htmlFor={id}>{label}</Label>`
     - `<Input id={id} type={type} autoComplete={autoComplete} name={field.name} value={typeof value === 'string' ? value : ''} onChange={field.onChange} onBlur={field.onBlur} ref={field.ref} disabled={field.disabled} aria-invalid={fieldState.invalid} aria-describedby={describedBy} />`
     - optional `<p id={descriptionId} className="text-caption text-text-muted">`
     - optional `<p id={errorId} className="text-caption text-danger">{t([fieldState.error.message, 'errors.UNHANDLED_EXCEPTION'])}</p>`

### G. `src/features/session/`
| # | Path | Contract |
|---|------|----------|
| G1 | `sessionStore.ts` | See Contract G1 below |
| G2 | `SessionContext.ts` | `export const SessionContext = createContext<SessionStore \| null>(null)` |
| G3 | `hooks/useSession.ts` | `export function useSessionStore(): SessionStore`: `use(SessionContext)`, and if null `throw new Error('useSessionStore must be used inside AppProviders')`. `export function useSession(): Session \| null`: `const store = useSessionStore()`, then `return useSyncExternalStore(store.subscribe, store.get)` |
| G4 | `hooks/useSignOut.ts` | `export function useSignOut(): () => void`. It uses `useSessionStore()` and `useQueryClient()`, and returns `() => { queryClient.clear(); store.set(null); }` |
| G5 | `devSessions.ts` | `export const devSessions: Record<Role, Session> = { student: { userId: 's1', displayName: 'أحمد', role: 'student' }, teacher: { userId: 't1', displayName: 'أ. محمد', role: 'teacher' }, admin: { userId: 'a1', displayName: 'المدير', role: 'admin' } }` |
| G6 | `guards.ts` | See Contract G6 below |
| G7 | `schemas/loginSearchSchema.ts` | `export const loginSearchSchema = z.object({ redirect: z.string().startsWith('/').refine((value) => !value.startsWith('//')).optional().catch(undefined) })`. `export type LoginSearch = z.infer<typeof loginSearchSchema>` |
| G8 | `pages/DevSignInPage.tsx` | See Contract G8 below |
| G9 | `i18n/ar.json`, `i18n/en.json` | `session` namespace. Keys are in the i18n table |
| G10 | `index.ts` | Barrel. Exports: `roles`, `Role`, `Session`, `SessionStore`, `createSessionStore`, `SessionContext`, `useSession`, `useSessionStore`, `useSignOut`, `devSessions`, `roleHome`, `requireRole`, `redirectSignedIn`, `redirectToHome`, `loginSearchSchema`, `DevSignInPage`. Also `export const sessionLocales = { ar, en }` (JSON imports) |

**Contract G1: `sessionStore.ts`**
- `export const roles = ['student', 'teacher', 'admin'] as const`.
- `export type Role = (typeof roles)[number]`.
- `export interface Session { userId: string; displayName: string; role: Role }`.
- `export interface SessionStore { get: () => Session | null; set: (session: Session | null) => void; subscribe: (listener: () => void) => () => void }`.
- `export function createSessionStore(initial: Session | null): SessionStore`:
  - A closure holds `current` and a `Set` of listeners.
  - `set` replaces `current` and calls every listener.
  - `subscribe` adds the listener and returns a remover.
  - Methods are arrow properties, so `useSyncExternalStore(store.subscribe, store.get)` works unbound.

**Contract G6: `guards.ts`**
- `export const roleHome = { student: '/student', teacher: '/teacher', admin: '/admin' } as const satisfies Record<Role, string>`.
- `export function requireRole(session: Session | null, role: Role, href: string): void`:
  - if `session === null`: `throw redirect({ to: '/login', search: { redirect: href } })`
  - if `session.role !== role`: `throw redirect({ to: roleHome[session.role] })`
- `export function redirectSignedIn(session: Session | null, redirectTo: string | undefined): void`:
  - if `session === null`: return
  - `throw redirectTo ? redirect({ href: redirectTo }) : redirect({ to: roleHome[session.role] })`
- `export function redirectToHome(session: Session | null): never`: throws `redirect({ to: '/login' })` when there is no session, else `redirect({ to: roleHome[session.role] })`.

**Contract G8: `DevSignInPage.tsx`**
- `export function DevSignInPage()`: `const { t } = useTranslation('session')`, `const store = useSessionStore()`.
- Render:
  - `<main id="main" className="mx-auto flex min-h-dvh max-w-layout flex-col justify-center gap-6 px-4">`
  - `<h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('signIn.title')}</h1>`
  - `<p className="text-caption text-text-muted">{t('signIn.devNotice')}</p>`
  - `<div className="flex flex-col gap-3">` containing, for each `role` in `roles`: `<Button key={role} variant={role === 'student' ? 'primary' : 'secondary'} onClick={() => { store.set(devSessions[role]); }}>{t(\`signIn.as.${role}\`)}</Button>`
- No navigation call: the router invalidation plus `redirectSignedIn` (Decision 18) moves the user on.

### H. `src/features/shell/`
| # | Path | Contract |
|---|------|----------|
| H1 | `navConfig.ts` | See Contract H1 below |
| H2 | `components/AppShell.tsx` | See Contract H2 below |
| H3 | `components/AppBar.tsx` | See Contract H3 below |
| H4 | `components/TopTabs.tsx` | See Contract H4 below |
| H5 | `components/TabBar.tsx` | See Contract H5 below |
| H6 | `pages/PlaceholderPage.tsx` | `export interface PlaceholderPageProps { titleKey: string }`. `export function PlaceholderPage({ titleKey })` uses `t` from `'shell'` and renders `<section className="flex flex-col gap-3"><h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t(titleKey)}</h1><p className="text-caption text-text-muted">{t('placeholder.body')}</p></section>` |
| H7 | `pages/MorePage.tsx` | See Contract H7 below |
| H8 | `i18n/ar.json`, `i18n/en.json` | `shell` namespace. Keys are in the i18n table |
| H9 | `index.ts` | Barrel: `AppShell`, `PlaceholderPage`, `MorePage`, and `export const shellLocales = { ar, en }` |

**Contract H1: `navConfig.ts`**
- `export type NavPath = NonNullable<LinkProps['to']>`.
- `export interface NavItem { key: string; to: NavPath; labelKey: string; icon: LucideIcon }`.
- `export interface RoleNav { items: readonly NavItem[]; tabBarKeys: readonly string[]; morePath: NavPath | null }`.
- `export const navIconStrokeWidth = 1.8`, with comment `// design-system: Lucide stroke 1.8`.
- `export const navByRole: Record<Role, RoleNav>`, holding the rows in the Nav table in that order.
- `export function tabBarItems(nav: RoleNav): readonly NavItem[]`: items whose key is in `tabBarKeys`, in `tabBarKeys` order.
- `export function overflowItems(nav: RoleNav): readonly NavItem[]`: items not in `tabBarKeys`, in `items` order.

**Contract H2: `AppShell.tsx`**
- `export interface AppShellProps { role: Role }`.
- `export function AppShell({ role }: AppShellProps)` renders:
  - `<div className="min-h-dvh bg-bg text-text">`
  - `<a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:start-4 focus:top-4 focus:z-20 focus:rounded-full focus:bg-surface focus:px-4 focus:py-2 focus:shadow-2">{t('skipToContent')}</a>`
  - `<AppBar role={role} />`
  - `<main id="main" tabIndex={-1} className="mx-auto max-w-layout px-4 pt-6 pb-24 lg:px-6 lg:pb-8"><Outlet /></main>`
  - `<TabBar role={role} />`

**Contract H3: `AppBar.tsx`**
- `AppBar({ role }: { role: Role })`, typed as `AppBarProps`.
- `const session = useSession()` and `const signOut = useSignOut()`.
- Render:
  - `<header className="sticky top-0 z-10 border-b border-border bg-surface">`
  - A row: `<div className="mx-auto flex min-h-14 max-w-layout items-center gap-3 px-4 lg:px-6">` containing, in order:
    - `<Link to={roleHome[role]} className="font-display text-h3 font-bold">{t('common:app.name')}</Link>`
    - `<span className="rounded-full bg-text px-2.5 py-0.5 text-micro text-surface">{t(\`role.${role}\`)}</span>`
    - `<span className="ms-auto text-caption text-text-muted">{session?.displayName}</span>`
    - `<Button variant="secondary" size="sm" onClick={signOut}><LogOut aria-hidden className="size-4" strokeWidth={navIconStrokeWidth} />{t('signOut')}</Button>`
  - `<TopTabs role={role} />` after the row.

**Contract H4: `TopTabs.tsx`**
- `<nav aria-label={t('nav.main')} className="hidden lg:block">`
- `<ul className="mx-auto flex max-w-layout gap-1 overflow-x-auto px-4 lg:px-6">`
- For each item of `navByRole[role].items`: `<li key={item.key}><Link to={item.to} activeOptions={{ exact: item.to === roleHome[role] }} className="inline-flex min-h-11 items-center border-b-2 border-transparent px-3.5 text-ui font-medium text-text-muted data-[status=active]:border-accent data-[status=active]:font-semibold data-[status=active]:text-text focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-ring">{t(item.labelKey)}</Link></li>`

**Contract H5: `TabBar.tsx`**
- `<nav aria-label={t('nav.tabBar')} className="fixed inset-x-0 bottom-0 z-10 border-t border-border bg-surface lg:hidden">`
- `<ul className="flex">`
- For each of `tabBarItems(nav)`, plus a More item when `nav.morePath` is set: `<li className="flex-1"><Link to=… activeOptions={{ exact: to === roleHome[role] }} className="flex min-h-14 flex-col items-center justify-center gap-1 text-micro text-text-muted data-[status=active]:font-semibold data-[status=active]:text-text focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-ring"><Icon aria-hidden className="size-5.5" strokeWidth={navIconStrokeWidth} /><span>{label}</span></Link></li>`
- The More item uses icon `Ellipsis` and `t('nav.more')`.

**Contract H7: `MorePage.tsx`**
- `export interface MorePageProps { role: Role }`.
- Render:
  - `<section className="flex flex-col gap-3"><h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('more.title')}</h1>`
  - `<ul className="flex flex-col gap-2">`
  - For each of `overflowItems(navByRole[role])`: `<li key><Link to={item.to} className="flex min-h-11 items-center gap-3 rounded-md border border-border bg-surface px-3.5 py-3 text-ui font-semibold text-text focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-ring"><Icon aria-hidden className="size-5.5" strokeWidth={navIconStrokeWidth} /><span className="flex-1">{t(item.labelKey)}</span><ChevronRight aria-hidden className="size-4 rtl:rotate-180" /></Link></li>`

**Nav table** (`labelKey` is in the `shell` namespace)
| Role | key | to | labelKey | icon | tab bar |
|---|---|---|---|---|---|
| student | home | `/student` | `nav.student.home` | House | yes |
| student | progress | `/student/progress` | `nav.student.progress` | ChartLine | yes |
| student | multiExam | `/student/multi-exam` | `nav.student.multiExam` | Layers | no |
| student | ask | `/student/ask` | `nav.student.ask` | MessageCircleQuestion | yes |
| student | subscription | `/student/subscription` | `nav.student.subscription` | CreditCard | no |
| teacher | queue | `/teacher` | `nav.teacher.queue` | ListChecks | yes |
| teacher | inbox | `/teacher/inbox` | `nav.teacher.inbox` | Inbox | yes |
| teacher | stats | `/teacher/stats` | `nav.teacher.stats` | ChartColumn | yes |
| admin | dashboard | `/admin` | `nav.admin.dashboard` | LayoutDashboard | yes |
| admin | content | `/admin/content` | `nav.admin.content` | BookOpen | yes |
| admin | questions | `/admin/questions` | `nav.admin.questions` | FileQuestion | yes |
| admin | blueprints | `/admin/blueprints` | `nav.admin.blueprints` | ClipboardList | no |
| admin | users | `/admin/users` | `nav.admin.users` | Users | no |
| admin | audit | `/admin/audit` | `nav.admin.audit` | ScrollText | no |
| admin | export | `/admin/export` | `nav.admin.export` | Download | no |

`morePath` values: student `/student/more`, teacher `null`, admin `/admin/more`. `tabBarKeys` = the "yes" rows, in table order.

### I. Routes (`web/src/routes/`, TanStack file routes, no logic beyond a guard call)
| # | Path | Contract |
|---|------|----------|
| I1 | `__root.tsx` | `export const Route = createRootRouteWithContext<RouterContext>()({ component: Outlet })` |
| I2 | `index.tsx` | `createFileRoute('/')({ beforeLoad: ({ context }) => { redirectToHome(context.sessionStore.get()); } })` |
| I3 | `login.tsx` | `createFileRoute('/login')({ validateSearch: loginSearchSchema, beforeLoad: ({ context, search }) => { redirectSignedIn(context.sessionStore.get(), search.redirect); }, component: DevSignInPage })` |
| I4 | `student/route.tsx` | `createFileRoute('/student')({ beforeLoad: ({ context, location }) => { requireRole(context.sessionStore.get(), 'student', location.href); }, component: () => <AppShell role="student" /> })` |
| I5–I10 | `student/index.tsx`, `student/progress.tsx`, `student/multi-exam.tsx`, `student/ask.tsx`, `student/subscription.tsx`, `student/more.tsx` | Each is `createFileRoute('<path>')({ component: () => <PlaceholderPage titleKey="<labelKey from Nav table>" /> })`. `more.tsx` renders `<MorePage role="student" />` instead |
| I11 | `teacher/route.tsx` | As I4, with `'/teacher'` and `'teacher'` |
| I12–I14 | `teacher/index.tsx`, `teacher/inbox.tsx`, `teacher/stats.tsx` | Placeholder, as I5 |
| I15 | `admin/route.tsx` | As I4, with `'/admin'` and `'admin'` |
| I16–I23 | `admin/index.tsx`, `admin/content.tsx`, `admin/questions.tsx`, `admin/blueprints.tsx`, `admin/users.tsx`, `admin/audit.tsx`, `admin/export.tsx`, `admin/more.tsx` | Placeholder, as I5. `more.tsx` renders `<MorePage role="admin" />` |
| I24 | `web/src/routeTree.gen.ts` | Generated by the router plugin (`npm run build` / `dev` / `test`). Committed |

Routes import only from `@/features/session`, `@/features/shell` and `@/app/routerContext` (type).

### J. Test infrastructure (`web/src/test/`)
| # | Path | Contract |
|---|------|----------|
| J1 | `setup.ts` | See Contract J1 below |
| J2 | `msw/server.ts` | `export const server = setupServer()`. There are no default handlers until Orval emits some |
| J3 | `axe.ts` | `export const axe = configureAxe({ rules: { 'color-contrast': { enabled: false } } })`, from `vitest-axe` |
| J4 | `renderWithProviders.tsx` | See Contract J4 below |

**Contract J1: `setup.ts`**
- `import '@testing-library/jest-dom/vitest'`.
- `initI18n('en')`.
- `Object.defineProperty(window, 'scrollTo', { value: () => undefined, writable: true })`.
- `beforeAll(() => { server.listen({ onUnhandledRequest: 'error' }); })`.
- `afterEach(() => { cleanup(); server.resetHandlers(); void i18n.changeLanguage('en'); })`.
- `afterAll(() => { server.close(); })`.

**Contract J4: `renderWithProviders.tsx`**
- `export function createTestQueryClient(): QueryClient` returns `new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } })`.
- `export function renderWithProviders(ui: ReactElement, { lng = 'en' }: { lng?: Language } = {})`:
  - `void i18n.changeLanguage(lng)`
  - render `ui` inside `AppProviders`, with a fresh client and `createSessionStore(null)`.
- `export function renderApp(path: string, { lng = 'en', session = null }: { lng?: Language; session?: Session | null } = {})`:
  - `void i18n.changeLanguage(lng)`
  - `createAppRouter({ queryClient, sessionStore: createSessionStore(session), history: createMemoryHistory({ initialEntries: [path] }) })`
  - render `<AppProviders …><RouterProvider router={router} /></AppProviders>`
  - return `{ ...renderResult, router }`

### i18n keys (both files always)
| ns | key | ar | en |
|---|---|---|---|
| common | `app.name` | المنهج | Elmanhg |
| common | `validation.required` | هذا الحقل مطلوب. | This field is required. |
| common | `errors.UNHANDLED_EXCEPTION` | حدث خطأ غير متوقع. حاول مرة أخرى. | Something went wrong. Please try again. |
| common | `errors.NETWORK_ERROR` | تعذّر الاتصال بالخادم. تحقّق من اتصالك وحاول مرة أخرى. | Could not reach the server. Check your connection and try again. |
| common | `errors.VALIDATION_FAILED` | بعض البيانات غير صحيحة. | Some fields are not valid. |
| common | `error.title` | حدث خطأ | Something went wrong |
| common | `actions.retry` | إعادة المحاولة | Retry |
| common | `notFound.body` | الصفحة غير موجودة أو غير متاحة. | This page does not exist or is not available. |
| common | `notFound.back` | العودة | Back |
| session | `signIn.title` | تسجيل الدخول | Sign in |
| session | `signIn.devNotice` | دخول تجريبي للتطوير، يُستبدل بتسجيل الدخول الحقيقي. | Development sign-in, to be replaced by real sign-in. |
| session | `signIn.as.student` | الدخول كطالب | Sign in as student |
| session | `signIn.as.teacher` | الدخول كمعلّم | Sign in as teacher |
| session | `signIn.as.admin` | الدخول كمدير | Sign in as admin |
| shell | `skipToContent` | تخطَّ إلى المحتوى | Skip to content |
| shell | `nav.main` | التنقل الرئيسي | Main navigation |
| shell | `nav.tabBar` | التنقل السفلي | Bottom navigation |
| shell | `nav.more` | المزيد | More |
| shell | `role.student` / `role.teacher` / `role.admin` | طالب / معلّم / مدير | Student / Teacher / Admin |
| shell | `signOut` | تسجيل الخروج | Sign out |
| shell | `more.title` | المزيد | More |
| shell | `placeholder.body` | هذه الشاشة قيد الإنشاء. | This screen is under construction. |
| shell | `nav.student.home` / `progress` / `multiExam` / `ask` / `subscription` | الرئيسية / تقدّمي / امتحان متعدد الوحدات / اسأل معلّم / الاشتراك | Home / My progress / Multi-unit exam / Ask a teacher / Subscription |
| shell | `nav.teacher.queue` / `inbox` / `stats` | قائمة المراجعة / أسئلة الطلاب / إحصائياتي | Review queue / Student questions / My stats |
| shell | `nav.admin.dashboard` / `content` / `questions` / `blueprints` / `users` / `audit` / `export` | لوحة المؤشرات / المحتوى / الأسئلة / نماذج الامتحانات / المستخدمون / سجل التدقيق / تصدير البيانات | Dashboard / Content / Questions / Exam blueprints / Users / Audit log / Data export |

Arabic nav labels are copied from `prototype/app.js` `NAV`, and the not-found copy from `notFound()`.

### Implementation order
1. Make the API edits, then run `dotnet build api/` and commit `api/openapi/v1.json`.
2. Write every web file except the generated ones.
3. `cd web && npm install` (this creates the lockfile).
4. `npm run gen:tokens`, then `npm run gen:api`.
5. `npm run build`. This generates `routeTree.gen.ts`, then typechecks and builds.
6. `npm run lint`, `npm run format:check`, `npm test -- --run --coverage`.

## Error codes
No new API error codes. Client-side codes `NETWORK_ERROR` and `UNHANDLED_EXCEPTION` (the latter mirrors `Core.Exceptions.ExceptionErrorCodes`) are defined in `apiError.ts` and translated as listed in the i18n table.

## Domain behaviour
None. No backend domain changes. The API change is build-time OpenAPI emission only.

## API surface
No new endpoints. The OpenAPI document is written to `api/openapi/v1.json` on build (it is still also served at `GET /openapi/v1.json` outside Production).

## Test plan
Tests follow react-testing.md:
- Queries by role or label.
- `const user = userEvent.setup()` before render; every interaction is awaited.
- Render through `renderWithProviders` / `renderApp`, English unless stated.
- MSW for HTTP.
- Axe via `src/test/axe.ts`, asserted with `expect(results.violations).toEqual([])`.

| # | Test class (file) | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | generateTokensCss (`scripts/tokens/generateTokensCss.test.ts`) | emits a colour variable from the CSS var column | fixture row `\| color.bg \| --ds-color-bg \| #F5F5F7 \| x \|` gives output containing `  --ds-color-bg: #F5F5F7;` |
| 2 | 〃 | emits mobile and desktop size and line height for a typography token | `type.h1 … 26/31 · 30/36` gives `--ds-type-h1-size: 26px;`, `--ds-type-h1-line: 31px;`, `--ds-type-h1-size-desktop: 30px;`, `--ds-type-h1-line-desktop: 36px;` |
| 3 | 〃 | ignores the parenthetical note in a typography cell | `16/27 (lesson text 16/29)` and weight `400 (question stem 600)` give `-size: 16px`, `-line: 27px`, `-weight: 400`, with no `29px` |
| 4 | 〃 | emits weight and tracking for a typography token | `36/38 · 44/46, tracking -0.02em` / `700` gives `--ds-type-display-tracking: -0.02em;` and `--ds-type-display-weight: 700;` |
| 5 | 〃 | emits spacing in px and a desktop variant for mobile/desktop pairs | `space.4 \| 16` gives `--ds-space-4: 16px;`; `layout.gutter \| 16 mobile · 24 desktop` gives both `--ds-layout-gutter: 16px;` and `--ds-layout-gutter-desktop: 24px;` |
| 6 | 〃 | emits radius px, shadow before the em dash, and motion duration and easing | `--ds-radius-sm: 10px;`; `--ds-shadow-1: 0 1px 2px rgba(0,0,0,.04), 0 8px 24px rgba(0,0,0,.06);`; `--ds-motion-fast-duration: 150ms;`; `--ds-motion-fast-easing: cubic-bezier(.2,.8,.2,1);` |
| 7 | 〃 | emits breakpoints in @theme with the default scale reset and skips bp.base | output contains `@theme {`, `--breakpoint-*: initial;`, `--breakpoint-md: 700px;`, and no `--breakpoint-base` |
| 8 | 〃 | throws when the Colour section is missing | fixture without `### Colour` makes `generateTokensCss` throw `/section "### Colour" not found/` |
| 9 | parseEnv (`src/app/env.test.ts`) | defaults the API base URL to same origin when unset | `parseEnv({}).VITE_API_BASE_URL === ''` |
| 10 | 〃 | accepts an absolute API base URL | `'https://api.example.com'` is kept |
| 11 | 〃 | rejects an API base URL that is not a URL | `parseEnv({ VITE_API_BASE_URL: 'not a url' })` throws |
| 12 | 〃 | accepts a known dev session role | `'teacher'` gives `'teacher'` |
| 13 | 〃 | treats an empty dev session role as unset | `''` gives `undefined` |
| 14 | 〃 | rejects an unknown dev session role | `'owner'` throws |
| 15 | i18n (`src/app/i18n.test.ts`) | sets html lang ar and dir rtl for Arabic | after `await i18n.changeLanguage('ar')`, `document.documentElement` has `lang="ar"` and `dir="rtl"` |
| 16 | 〃 | sets html lang en and dir ltr for English | `lang="en"`, `dir="ltr"` |
| 17 | 〃 | formats ICU numbers with Arabic-Indic digits in Arabic | `i18n.addResource('ar','common','test.count','{count, plural, few {# أسئلة} other {# سؤال}}')` then `i18n.t('test.count', { lng: 'ar', count: 3 }) === '٣ أسئلة'` |
| 18 | 〃 | falls back to Arabic and rtl for an unsupported language | `changeLanguage('fr')` gives `dir="rtl"`, and `t('app.name') === 'المنهج'` |
| 19 | formatNumber/formatDate (`src/shared/lib/format.test.ts`) | formats Arabic numbers with Arabic-Indic digits by default | `formatNumber(1234, 'ar') === '١٬٢٣٤'` |
| 20 | 〃 | formats Arabic numbers with Latin digits when latin is requested | `formatNumber(1234, 'ar', 'latin') === '1,234'` |
| 21 | 〃 | formats English numbers with Latin digits | `formatNumber(1234, 'en') === '1,234'` |
| 22 | 〃 | formats Arabic dates with Arabic-Indic digits | `formatDate(new Date(Date.UTC(2026, 8, 27)), 'ar', 'arabic-indic', { timeZone: 'UTC', year: 'numeric' }) === '٢٠٢٦'` |
| 23 | 〃 | uses en-US for an unknown language | `numberLocale('fr', 'arabic-indic') === 'en-US'` |
| 24 | cn (`src/shared/lib/utils.test.ts`) | keeps a font-size token next to a colour token | `cn('text-ui', 'text-text') === 'text-ui text-text'` |
| 25 | 〃 | lets the later colour win | `cn('bg-surface', 'bg-soft') === 'bg-soft'` |
| 26 | http (`src/shared/lib/http.test.ts`) | returns the parsed body on success | MSW `GET */api/probe` returns `{ value: 1 }`; `await http('/api/probe')` equals it |
| 27 | 〃 | throws ApiError with status and every code from the error body | a 422 `{ code: 'VALIDATION_FAILED,OTHER', message: 'm' }` rejects with an ApiError where `status 422`, `code 'VALIDATION_FAILED'`, `codes ['VALIDATION_FAILED','OTHER']` |
| 28 | 〃 | throws UNHANDLED_EXCEPTION when the error body is not JSON | a 500 text body gives `code 'UNHANDLED_EXCEPTION'`, `status 500` |
| 29 | 〃 | throws NETWORK_ERROR when the request fails | `HttpResponse.error()` gives `status 0`, `code 'NETWORK_ERROR'` |
| 30 | 〃 | sends Accept-Language from the document language | after `changeLanguage('ar')`, a handler echoing the `accept-language` header returns `'ar'` |
| 31 | 〃 | returns undefined for 204 | a 204 response resolves `undefined` |
| 32 | 〃 | rethrows an abort without wrapping it | an aborted signal rejects with `name 'AbortError'`, not an ApiError |
| 33 | Button (`src/shared/ui/button.test.tsx`) | defaults to type button so it never submits a form by accident | `getByRole('button', { name: 'Go' })` has `type="button"` |
| 34 | 〃 | renders its child as the element when asChild is set | `<Button asChild><a href="/x">Go</a></Button>` gives `getByRole('link', { name: 'Go' })` |
| 35 | Form base components (`src/shared/form/Form.test.tsx`, test-local `TestForm`: fields `name`, `email` with schema `z.string().min(1, { error: 'validation.required' })`, labels "Name"/"Email", submit "Save", success text "Saved") | shows the required error inline and marks the field invalid when submitted empty | two "This field is required." texts; the Name input has `aria-invalid="true"` and an `aria-describedby` pointing at the error text |
| 36 | 〃 | focuses the first invalid field on submit | after clicking Save, the Name input has focus |
| 37 | 〃 | disables submit while submitting | onSubmit returns a pending promise: Save is disabled; after it resolves, Save is enabled |
| 38 | 〃 | submits the parsed values when valid | after typing both fields and saving, "Saved" is shown |
| 39 | 〃 | shows a server error on the mapped field and focuses it | onSubmit rejects `new ApiError(422, ['VALIDATION_FAILED'], '')` with `serverErrorFields { VALIDATION_FAILED: 'email' }`: "Some fields are not valid." is shown and the Email input has focus |
| 40 | 〃 | shows an unmapped server error in the form alert | same rejection with an empty map gives `getByRole('alert')` with text "Some fields are not valid." |
| 41 | 〃 | shows the generic message for a non-API failure | onSubmit rejects `new Error('x')`: the alert reads "Something went wrong. Please try again." |
| 42 | 〃 | has no axe violations | after an empty submit, the axe violations list is `[]` |
| 43 | RouteError (`src/shared/components/RouteError.test.tsx`, ad-hoc router: a root plus `/` whose component throws `new ApiError(0, ['NETWORK_ERROR'], '')` while a module flag is set, `defaultErrorComponent: RouteError`) | shows the message for the error code and recovers on retry | the alert shows "Could not reach the server…"; clear the flag and click Retry, and the success heading is shown |
| 44 | 〃 | falls back to the generic message for an unknown code | throwing `ApiError(500, ['NOT_A_KNOWN_CODE'], '')` gives "Something went wrong. Please try again." |
| 45 | createSessionStore (`src/features/session/sessionStore.test.ts`) | returns the initial session | `createSessionStore(devSessions.admin).get()` equals `devSessions.admin` |
| 46 | 〃 | notifies subscribers on set and stops after unsubscribe | the listener sees `get()` = student after the first `set`; after the remover runs, a second `set` does not call it (assert via an array the listener pushes `get()` into) |
| 47 | loginSearchSchema (`src/features/session/schemas/loginSearchSchema.test.ts`) | keeps an internal redirect path | `{ redirect: '/admin/users' }` is kept |
| 48 | 〃 | accepts a missing redirect | `{}` gives `{ redirect: undefined }` |
| 49 | 〃 | drops an absolute external URL | `'https://evil.example'` gives `undefined` |
| 50 | 〃 | drops a protocol-relative URL | `'//evil.example'` gives `undefined` |
| 51 | DevSignInPage (`src/features/session/pages/DevSignInPage.test.tsx`) | lands on the teacher home after signing in as teacher | `renderApp('/login')`; click "Sign in as teacher"; heading "Review queue" is shown |
| 52 | 〃 | returns to the originally requested page after signing in | `renderApp('/admin/users')` (anonymous) shows heading "Sign in"; click "Sign in as admin"; heading "Users" is shown |
| 53 | 〃 | sends a signed-in visitor away from sign in to their home | `renderApp('/login', { session: devSessions.student })` shows heading "Home" |
| 54 | 〃 | has no axe violations | `/login` renders, and axe violations are `[]` |
| 55 | AppShell (`src/features/shell/components/AppShell.test.tsx`) | shows every student destination in the top tabs | links inside nav "Main navigation", in order: Home, My progress, Multi-unit exam, Ask a teacher, Subscription |
| 56 | 〃 | shows three student tabs and More in the bottom tab bar | nav "Bottom navigation" links: Home, My progress, Ask a teacher, More |
| 57 | 〃 | shows the three teacher tabs without More | teacher bottom nav links: Review queue, Student questions, My stats. `queryByRole('link', { name: 'More' })` inside it is null |
| 58 | 〃 | marks the current destination as the current page | at `/student/progress`, the "My progress" link in Main navigation has `aria-current="page"` and "Home" does not |
| 59 | 〃 | shows the signed-in display name and role | at `/admin`, the text "المدير" and the role badge text "Admin" are shown |
| 60 | 〃 | signs out to the sign-in page | click "Sign out"; heading "Sign in" is shown |
| 61 | 〃 | redirects a student who opens an admin page to the student home | `renderApp('/admin/users', { session: devSessions.student })` shows heading "Home" and no "Users" heading |
| 62 | 〃 | redirects a teacher who opens a student page to the teacher home | `/student/ask` as teacher shows heading "Review queue" |
| 63 | 〃 | redirects an anonymous visitor to sign in | `/teacher/inbox` with no session shows heading "Sign in" |
| 64 | 〃 | renders right-to-left with Arabic navigation labels in Arabic | `renderApp('/student', { lng: 'ar', session: student })`: `document.documentElement` has `dir="rtl"`; nav "التنقل الرئيسي" has link "الرئيسية" |
| 65 | 〃 | has no axe violations | `/student` as student gives axe violations `[]` |
| 66 | MorePage (`src/features/shell/pages/MorePage.test.tsx`) | lists the admin destinations that are not in the tab bar | at `/admin/more`: heading "More"; inside `main`, links Exam blueprints, Users, Audit log, Data export, in order |
| 67 | 〃 | opens a destination from the list | clicking "Users" in main shows heading "Users" |
| 68 | createAppRouter (`src/app/router.test.tsx`) | sends an anonymous visitor at / to sign in | `renderApp('/')` shows heading "Sign in" |
| 69 | 〃 | sends a signed-in admin at / to the admin home | `renderApp('/', { session: devSessions.admin })` shows heading "Dashboard" |
| 70 | 〃 | shows the not-found page with a link back for an unknown path | `renderApp('/nope')` shows heading "This page does not exist or is not available." and link "Back" with `href="/"` |

## Definition of done
- [ ] `api/Directory.Packages.props` and `Elmanhg.Api.csproj` carry ApiDescription.Server 10.0.9 plus the two properties. `dotnet build api/` regenerates `api/openapi/v1.json` with no git diff. `dotnet test api/` is green.
- [ ] `api-ci.yml` has the "OpenAPI document is up to date" step after Build. `web-ci.yml` matches Contract A3 step for step.
- [ ] `.gitattributes` has exactly the 4 LF rules.
- [ ] `web/package.json` dependency versions exactly equal the Package set (no `^`, nothing extra: no `cn`, `next-themes`, `tw-animate-css`, devtools or Playwright). `package-lock.json` is committed.
- [ ] No `tailwind.config.*`, no `@tailwind` directive, no `.dark`/`dark:` anywhere in `web/`.
- [ ] `npm run gen:tokens` and `npm run gen:api` produce no git diff. `src/styles/tokens.css` starts with the generated header comment.
- [ ] `npm run typecheck`, `npm run lint`, `npm run format:check`, `npm test -- --run --coverage` and `npm run build` all exit 0. Coverage on `src/features/**` is at least 80 lines / 70 branches.
- [ ] Both grep patterns from A3 step 9 return nothing over `web/src`.
- [ ] Exactly tests 1–70 exist, with those names, in those files.
- [ ] `<html lang="ar" dir="rtl">` at boot. `applyDocumentLanguage` runs on every language change, and `DirectionProvider` wraps the app.
- [ ] Arabic-Indic digits for `ar` through both `format.ts` and ICU messages (`parseLngForICU`).
- [ ] Fonts are imported from `@fontsource` only (no Google Fonts `<link>`). Inter, Roboto, Arial, Cairo and Tajawal appear nowhere.
- [ ] Every user-visible string is in both `ar.json` and `en.json` of its namespace, with the i18n-table values.
- [ ] Guards live only in `student/route.tsx`, `teacher/route.tsx`, `admin/route.tsx` (`requireRole`), `login.tsx` (`redirectSignedIn`) and `index.tsx` (`redirectToHome`).
- [ ] The session is never written to `localStorage`, `sessionStorage` or cookies.
- [ ] Sign-out calls `queryClient.clear()`.
- [ ] All HTTP goes through `src/shared/lib/http.ts`. No `fetch` appears elsewhere in `src/` except generated code.
- [ ] No `forwardRef`, `.Provider`, `useMemo`, `useCallback`, `React.memo`, `React.FC`, `any`, `!` non-null, `@ts-ignore`, `console.log` or default export (except config files) in `web/src`.
- [ ] Only logical direction utilities are used. The chevron in MorePage carries `rtl:rotate-180`.
- [ ] Every interactive element has a `focus-visible` ring. Primary targets are at least `min-h-11`, and tab bar items `min-h-14`.
- [ ] README, `docs/design-system.md` §5.7 and §10, and `.claude/design-system.md` (TabBar row + mapping bullet) are updated as specified. No doc is created outside `/docs` or the root README.
