# 06 — Frontend, Mobile & E2E: Team Defaults (researched Sept 2026)

Scope: production-grade defaults for agent-built apps. Stacks: **react** (web), **flutter** (mobile), **kmp** (Kotlin Multiplatform + Compose Multiplatform). Plus the **E2E agent step (Agy)**.
Rule zero for all UI stacks: **design-system.md tokens only** — no literal colors, spacing, radii, font sizes, durations in feature code. Figma frame is the visual source of truth; tokens are the code source of truth.

---

## 0. Version baseline (verified Sept 2026) — correct the brief where it is stale

| Area | Brief said | Actual current (Sept 2026) | Team default |
|---|---|---|---|
| React | 19.x | 19.2 (`<Activity>`, `useEffectEvent`, `cacheSignal`) | React 19.2 + React Compiler 1.0 |
| Vite | 7 | **Vite 8.3** (Rolldown single bundler, Oxc). Vite 7.3 still gets fixes | Vite 8; Node 22.12+ |
| @vitejs/plugin-react | — | **v6 dropped Babel** (Oxc). Compiler via `@rolldown/plugin-babel` + `reactCompilerPreset()` | yes |
| TypeScript | 5.x | TS 6.0 (Mar 2026, last JS-based), **TS 7.0 native Go (3 Aug 2026)**. typescript-eslint **not yet compatible with TS 7** (needs 7.1 API) | Compile/typecheck with TS 6.0 (or TS 7 `tsc` for CI speed + `@typescript/typescript6` for ESLint). Revisit at TS 7.1 |
| Tailwind | v4 | 4.3 (full logical-property utilities, scrollbar utils) | v4 CSS-first `@theme` |
| shadcn/ui | — | CLI v4 (Mar 2026); `npx shadcn create` (Dec 2025); **RTL support (Jan 2026)**; **Base UI default (Jul 2026)**; `cn` moved to its own npm package (Sep 2026) | yes |
| TanStack Query/Router | v5 / v1 | Query v5, Router v1 | yes |
| Zod | 4 | Zod 4 (`z.email()`, `error:` param) + `@hookform/resolvers` v5 | yes |
| Orval | — | **v8**: ESM only, Node ≥ 22.18, **fetch default** (not axios), mock `generators[]` | yes |
| Vitest | 3 | **Vitest 4** (browser mode stable, `toMatchScreenshot` visual regression, Playwright traces) | Vitest 4 |
| Flutter / Dart | 3.3x | **Flutter 3.47**, Dart 3.12+. `material_ui`/`cupertino_ui` standalone packages 1.0 (opt-in; in-SDK libs deprecated from Nov release). iOS min 15. Impeller default Android & desktop | Flutter 3.47 stable |
| go_router | — | **18.0.1** (requires Flutter 3.44 / Dart 3.12, migrated to `material_ui`). URLs case-sensitive since 15 | 18.x + `go_router_builder` |
| Riverpod | 3 | 3.x (unified `Ref`, auto-retry, `Ref.mounted`, mutations/offline experimental, legacy providers moved) | Riverpod 3 + generator |
| freezed | — | **4.0.2** (Sep 2026). 3.x removed `when/map`; classes must be `sealed`/`abstract` | freezed 4 |
| very_good_analysis | — | **11.0.0** | yes |
| flutter_secure_storage | — | **11.2.0**; v11 removed `encryptedSharedPreferences` (must migrate via v10) | yes |
| Kotlin | 2.x | **Kotlin 2.4.0** (Jun 2026): stable context parameters, explicit backing fields, stable UUID | 2.4.x |
| Compose Multiplatform | — | **1.12.1**; Navigation 3 CMP **1.1.1 stable** (since CMP 1.10); Hot Reload stable | yes |
| KMP structure | composeApp | **New default (May 2026, AGP 9)**: `shared` KMP library + `androidApp`/`iosApp`/`desktopApp`/`webApp` app modules; `com.android.kotlin.multiplatform.library` plugin | yes |
| Ktor | — | **3.6.0** (18 Sep 2026) | yes |
| Mokkery | — | 3.4.2 (compiler-plugin KMP mocking) | yes (MockK = JVM/Android only) |
| Agy (Antigravity CLI) | — | `agy` 1.1.x; headless `-p`, `--output-format json`, `--json-schema`. **Built-in Browser Subagent is an IDE feature; Google's codelab says it is "not yet supported in the terminal-first Antigravity CLI"** → attach Playwright MCP / playwright-cli skill / Chrome DevTools MCP | see §4 |

---

## 1. REACT (web frontend)

### 1.1 Scaffold

```bash
npm create vite@latest app -- --template react-ts        # Vite 8
cd app
npm i react@^19.2 react-dom@^19.2 @tanstack/react-query @tanstack/react-router \
      react-hook-form zod @hookform/resolvers i18next react-i18next i18next-browser-languagedetector
npm i -D @tanstack/router-plugin @tanstack/react-query-devtools @tanstack/eslint-plugin-query \
      tailwindcss @tailwindcss/vite @rolldown/plugin-babel babel-plugin-react-compiler \
      vitest @vitest/browser-playwright @testing-library/react @testing-library/user-event \
      @testing-library/jest-dom jsdom msw orval @playwright/test \
      eslint @eslint/js typescript-eslint eslint-plugin-react-hooks eslint-plugin-jsx-a11y prettier
npx shadcn@latest init        # Tailwind v4 + Base UI default; or `npx shadcn create`
```

`vite.config.ts` (Vite 8 + plugin-react v6 — the old `react({ babel: {...} })` form **no longer works**):

```ts
import { defineConfig } from 'vite';
import react, { reactCompilerPreset } from '@vitejs/plugin-react';
import { babel } from '@rolldown/plugin-babel';
import tailwindcss from '@tailwindcss/vite';
import { tanstackRouter } from '@tanstack/router-plugin/vite';

export default defineConfig({
  plugins: [
    tanstackRouter({ target: 'react', autoCodeSplitting: true }), // must precede react()
    babel({ include: /\.[jt]sx?$/, babelConfig: reactCompilerPreset() }), // compiler before react()
    react(),
    tailwindcss(),
  ],
  resolve: { tsconfigPaths: true }, // Vite 8 native tsconfig paths
});
```

### 1.2 Project layout (feature-sliced, colocated)

```
src/
  app/            # providers.tsx, router.tsx, queryClient.ts, i18n.ts, env.ts
  routes/         # TanStack file routes ONLY (thin: loader + component import)
    __root.tsx  _auth.tsx  _app/invoices/index.tsx  _app/invoices/$id.tsx
  features/
    invoices/
      api/        # queryOptions factories, mutations wrapping generated hooks
      components/ # InvoiceTable.tsx, InvoiceForm.tsx
      hooks/      # feature hooks (no fetching in components)
      schemas/    # zod schemas (form + domain)
      i18n/       # en.json, ar.json (namespace "invoices")
      __tests__/  # *.test.tsx next to feature
      index.ts    # public surface; other features import only from here
  shared/
    ui/           # shadcn components (owned code), design-system wrappers
    lib/          # cn, formatters (Intl), http client (orval mutator)
    api/generated/ # Orval output — never hand-edit, git-tracked or CI-generated
  styles/
    tokens.css    # generated from design-system.md / Figma variables
    app.css       # @import "tailwindcss"; @import "./tokens.css";
```

Rules: features never deep-import each other (`features/x/index.ts` only). `routes/` has no business logic. Enforce with `eslint-plugin-boundaries` or `no-restricted-imports`.

### 1.3 Theming from tokens (Tailwind v4 CSS-first)

No `tailwind.config.js`. Tokens live in CSS:

```css
/* styles/tokens.css — generated from design-system.md; do not hand-edit */
:root {
  --ds-color-bg: oklch(1 0 0);
  --ds-color-fg: oklch(0.21 0.02 260);
  --ds-color-primary: oklch(0.55 0.2 260);
  --ds-space-4: 1rem;
  --ds-radius-md: 0.5rem;
}
.dark { --ds-color-bg: oklch(0.18 0.02 260); --ds-color-fg: oklch(0.97 0 0); }

/* app.css */
@import "tailwindcss";
@import "tw-animate-css";
@import "./tokens.css";
@custom-variant dark (&:where(.dark, .dark *));
@theme inline {
  --color-background: var(--ds-color-bg);
  --color-foreground: var(--ds-color-fg);
  --color-primary: var(--ds-color-primary);
  --radius-md: var(--ds-radius-md);
  --spacing: 0.25rem; /* base unit; p-4 = 1rem */
}
```

- DO use only semantic utilities: `bg-background text-foreground bg-primary rounded-md p-4`.
- DON'T write `bg-[#1e40af]`, `p-[13px]`, `text-blue-600`, inline `style={{color:'#...'}}`. Add a lint check: `grep -rE '#[0-9a-fA-F]{3,8}|\[[0-9]+px\]' src/features` fails CI.
- Missing token → add to design-system.md + tokens.css first, never inline.

### 1.4 Routing (TanStack Router, file-based, typed)

```ts
// app/router.tsx
export const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, retry: 1, throwOnError: false } },
});
export const router = createRouter({
  routeTree, context: { queryClient, auth: undefined! },
  defaultPreload: 'intent', defaultPreloadStaleTime: 0, // let Query own caching
  defaultErrorComponent: RouteError, defaultNotFoundComponent: NotFound,
});
declare module '@tanstack/react-router' { interface Register { router: typeof router } }

// routes/_app/invoices/$id.tsx
export const Route = createFileRoute('/_app/invoices/$id')({
  loader: ({ context, params }) => context.queryClient.ensureQueryData(invoiceQuery(params.id)),
  pendingComponent: InvoiceSkeleton,
  component: InvoicePage,
});
function InvoicePage() {
  const { id } = Route.useParams();
  const { data } = useSuspenseQuery(invoiceQuery(id)); // never undefined
  ...
}
```

- Auth guard in layout route `_app.tsx` `beforeLoad: ({context, location}) => { if (!context.auth.user) throw redirect({ to: '/login', search: { redirect: location.href } }) }`.
- Validate search params with Zod (`validateSearch: zodValidator(schema)` / Standard Schema) — filters, pagination, sort live in URL.

### 1.5 Data fetching & state

- **Server state = TanStack Query only.** Client/UI state = `useState`/`useReducer`; cross-tree UI state = context or Zustand (small). Never copy query data into `useState`.
- Use `queryOptions()` factories per feature; key convention `[feature, scope, params]`:
```ts
export const invoiceKeys = { all: ['invoices'] as const,
  list: (f: Filters) => [...invoiceKeys.all, 'list', f] as const,
  detail: (id: string) => [...invoiceKeys.all, 'detail', id] as const };
export const invoiceQuery = (id: string) => queryOptions({
  queryKey: invoiceKeys.detail(id), queryFn: ({ signal }) => getInvoice(id, { signal }) });
```
- Mutations: `useMutation({ mutationFn, onSuccess: () => qc.invalidateQueries({ queryKey: invoiceKeys.all }) })`. Optimistic updates via `onMutate` + rollback, or React 19 `useOptimistic` for local-only UI.
- v5 names: `isPending` (not `isLoading` for "no data yet"), `gcTime` (not `cacheTime`), `placeholderData: keepPreviousData` (not `keepPreviousData: true`), `throwOnError` (not `useErrorBoundary`). No `onSuccess/onError` on `useQuery`.
- React 19.2 `useEffectEvent` for effect callbacks needing latest props; `<Activity mode="hidden">` to keep tab state.

### 1.6 API client generation (OpenAPI → Orval v8)

```js
// orval.config.mjs  (v8 is ESM-only; Node >= 22.18)
import { defineConfig } from 'orval';
export default defineConfig({
  api: {
    input: { target: '../contracts/openapi.yaml' },
    output: {
      mode: 'tags-split', target: 'src/shared/api/generated', schemas: 'src/shared/api/generated/model',
      client: 'react-query', httpClient: 'fetch',          // fetch is v8 default
      override: { mutator: { path: 'src/shared/lib/http.ts', name: 'http' },
                  query: { useQuery: true, useSuspenseQuery: true, signal: true } },
      mock: { generators: [{ type: 'msw' }] },             // v8 generators array; delay default false
    },
  },
  apiZod: { input: '../contracts/openapi.yaml',
            output: { client: 'zod', mode: 'tags-split', target: 'src/shared/api/generated/zod' } },
});
```
`npm run gen:api` = `orval`. CI: regenerate and `git diff --exit-code`. Alternative lighter stack: `openapi-typescript` + `openapi-fetch` + `openapi-react-query`.

Mutator (`shared/lib/http.ts`): base URL from env, `credentials: 'include'`, attaches CSRF header, maps non-2xx to a typed `ApiError { status, code, message, fieldErrors }` (ProblemDetails), handles 401 → single-flight refresh → retry once → else redirect to login.

### 1.7 Forms & validation (RHF + Zod 4)

```ts
const schema = z.object({
  email: z.email({ error: t('validation.email') }),          // Zod 4: top-level formats, `error` not `message`
  amount: z.coerce.number().positive(),
  dueDate: z.iso.date(),
});
type FormValues = z.infer<typeof schema>;
const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: {...}, mode: 'onBlur' });
```
- Use shadcn `Form`/`Field` components: every input has `<label>` (FormLabel), description, `FormMessage` wired via `aria-describedby` + `aria-invalid`.
- Map server `fieldErrors` → `form.setError(field, { message })`; focus first invalid field (`shouldFocusError: true`).
- Disable submit only while `isSubmitting`; never disable for "invalid" (WCAG: users must discover errors).
- Zod 4 gotchas: `z.record(k, v)` needs 2 args; `.strict()` → `z.strictObject`; `error.flatten()` → `z.treeifyError`; `z.nativeEnum` → `z.enum`.

### 1.8 Error / loading / empty states (mandatory triad per data view)

- Loading: skeleton matching final layout (no spinners for >300 ms content areas; avoid CLS). Route `pendingComponent` + Suspense.
- Error: route `errorComponent` + `QueryErrorResetBoundary`; message from i18n by `ApiError.code`; "Retry" button calls `reset()`. Never show raw stack/JSON.
- Empty: illustrated empty state with primary CTA (per Figma). Distinguish "no data" vs "no results for filter" (offer clear filters).
- Toasts (sonner) only for transient mutation outcomes; `role="status"` politeness. Errors that block a form go inline.

### 1.9 Auth tokens

- DEFAULT: **BFF / backend-set `HttpOnly; Secure; SameSite=Lax|Strict` cookies**; SPA never sees tokens. Add CSRF token header for state-changing requests.
- If bearer tokens are unavoidable: access token **in memory only** (module variable), refresh token in HttpOnly cookie; silent refresh single-flight. **Never localStorage/sessionStorage** for tokens (XSS-readable).
- Clear Query cache on logout: `queryClient.clear()`.

### 1.10 i18n + RTL (Arabic)

- i18next namespaces per feature; keys, not English strings, in code. ICU plurals via `i18next-icu` for Arabic's 6 plural forms (zero/one/two/few/many/other).
- On language change: `document.documentElement.lang = lng; document.documentElement.dir = i18n.dir(lng);`
- **Logical utilities only**: `ms-*/me-*/ps-*/pe-*`, `start-*/end-*`, `text-start/end`, `border-s/e`, `rounded-s/e` (Tailwind 4.3 completes logical props). Ban `ml-/mr-/pl-/pr-/left-/right-/text-left/text-right` via lint (e.g. `eslint-plugin-tailwindcss` custom or grep).
- Mirror directional icons: `rtl:rotate-180` on chevrons/arrows; don't mirror logos, media controls, checkmarks.
- Format with `Intl.NumberFormat/DateTimeFormat(locale)`; decide Arabic-Indic vs Latin digits per product (`ar-EG` vs `ar-EG-u-nu-latn`). Fonts: Arabic-capable font token (e.g. IBM Plex Sans Arabic / Noto) with larger line-height.
- shadcn: use its RTL support (Jan 2026) + `DirectionProvider` for Radix/Base UI portals.

### 1.11 Accessibility (WCAG 2.2 AA — enforceable checklist)

- Semantic HTML first: `<button>` for actions, `<a href>` for navigation, `<nav> <main> <header>` landmarks, one `<h1>` per page, ordered headings.
- **2.4.7/2.4.11/2.4.13**: visible focus ring via `focus-visible:ring-2 ring-ring ring-offset-2` token; **focus not obscured** by sticky headers (`scroll-padding-top` = header height token).
- **2.5.8 Target Size (Minimum)**: ≥ 24×24 CSS px (team default 44×44 for primary touch targets, `min-h-11 min-w-11`).
- **2.5.7 Dragging**: every drag (sortable lists, sliders) has a click/keyboard alternative.
- **3.3.7 Redundant Entry**: don't ask twice in a flow; prefill. **3.3.8 Accessible Authentication**: allow paste + password managers (`autocomplete="current-password"`, `one-time-code`), no cognitive puzzles.
- **3.2.6 Consistent Help**: help link in the same place on every page.
- 1.4.3 contrast 4.5:1 (3:1 large/UI components 1.4.11) — verify tokens once in design-system.md.
- Dialogs: use shadcn/Base UI primitives (focus trap, `Esc`, return focus). Never build custom modals from `div`.
- Live regions for async results (`aria-live="polite"`); `aria-busy` on loading regions.
- Images: `alt` meaningful or `alt=""` decorative. Icon-only buttons: `aria-label` (i18n).
- Respect `prefers-reduced-motion` (`motion-safe:` variants).
- Tooling: `eslint-plugin-jsx-a11y` strict, `@axe-core/playwright` in E2E, `vitest-axe`/`jest-axe` in component tests.

### 1.12 Responsive

Mobile-first; breakpoints are tokens (`--breakpoint-md` in `@theme`). Required verification widths: **390** (mobile) and **1280** (desktop); also sanity 768. Use container queries (`@container`, `@md:`) for components reused in different widths. No horizontal scroll at 320px (WCAG 1.4.10 reflow). Tables → card list under `md`.

### 1.13 Performance

- React Compiler 1.0 on: **don't add `useMemo`/`useCallback`/`React.memo` by default**. Keep existing ones (compiler preserves them; `preserve-manual-memoization` rule). Use manual memo only as an escape hatch with a comment + profiler evidence, or when a value feeds a non-React API needing referential stability.
- Route-level code splitting via `autoCodeSplitting`; `lazy()` heavy widgets (charts, editors).
- Virtualize lists > 100 rows: `@tanstack/react-virtual`.
- Images: explicit `width/height` (CLS), `loading="lazy"` below fold, `fetchpriority="high"` for LCP image, AVIF/WebP via `<picture>`, responsive `srcset`.
- Budget: LCP < 2.5s, INP < 200ms, CLS < 0.1 on mid-tier mobile; `rollup-plugin-visualizer`-equivalent for Rolldown to inspect chunks; fail CI if main chunk > agreed KB.

### 1.14 Security

- Never `dangerouslySetInnerHTML` except with `DOMPurify.sanitize()` in one audited `SafeHtml` component; ESLint `react/no-danger` = error elsewhere.
- No `javascript:` URLs; validate user-supplied URLs (`new URL()` + protocol allowlist).
- CSP header from hosting (no `unsafe-inline` scripts); Trusted Types if feasible.
- Env: only `VITE_*` vars reach the client — **treat them as public**. No secrets in frontend. Validate env at boot with Zod (`app/env.ts`), fail fast.
- `npm audit`/Renovate; lockfile committed; `npm ci` in CI.

### 1.15 Lint / format

```js
// eslint.config.mjs
import js from '@eslint/js';
import { defineConfig, globalIgnores } from 'eslint/config';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import pluginQuery from '@tanstack/eslint-plugin-query';

export default defineConfig([
  globalIgnores(['dist', 'src/shared/api/generated', 'src/routeTree.gen.ts']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, tseslint.configs.strictTypeChecked, tseslint.configs.stylisticTypeChecked,
              reactHooks.configs.flat.recommended /* includes compiler rules */, jsxA11y.flatConfigs.strict,
              pluginQuery.configs['flat/recommended']],
    languageOptions: { parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname } },
    rules: { '@typescript-eslint/no-floating-promises': 'error', 'react/no-danger': 'off' },
  },
]);
```
eslint-plugin-react-hooks (v6/7) recommended now includes compiler rules: `purity`, `refs`, `immutability`, `set-state-in-effect`, `set-state-in-render`, `static-components`, `use-memo`, `incompatible-library`, `error-boundaries`. Treat as errors.
Formatting: Prettier (+ `prettier-plugin-tailwindcss` for class order). Biome 2 is acceptable for format + fast lint, but it does not replace typed `strictTypeChecked` rules — if used, run Biome for format and ESLint for typed rules.
`tsconfig`: `"strict": true, "noUncheckedIndexedAccess": true, "exactOptionalPropertyTypes": true, "verbatimModuleSyntax": true, "noEmit": true`.

### 1.16 Build / CI

```bash
npm ci
npm run gen:api && git diff --exit-code src/shared/api/generated
npx tsc -b --noEmit
npx eslint . --max-warnings=0
npx prettier --check .
npx vitest run --coverage
npx vite build
npx playwright install --with-deps chromium && npx playwright test
```

### 1.17 Testing conventions (React)

- **Unit** (pure fns, zod schemas, formatters): Vitest, node env.
- **Component/integration** (the bulk): Vitest + Testing Library + user-event + MSW. Render the feature with real providers (`renderWithProviders`: fresh `QueryClient({ defaultOptions:{queries:{retry:false, gcTime: Infinity}} })`, router memory history, i18n `lng: 'en'` and one `ar` RTL test per screen).
- **Query priority**: `getByRole` (with `name`) → `getByLabelText` → `getByPlaceholderText` → `getByText` → `getByDisplayValue` → `getByAltText` → `getByTitle` → `getByTestId` (last resort). Use `findBy*` for async; `queryBy*` only to assert absence.
- **user-event**: `const user = userEvent.setup()` before render; `await user.click(...)`. Never `fireEvent` for user interactions.
- **MSW 2**:
```ts
// test/msw/server.ts
export const server = setupServer(...getInvoicesMock()); // orval-generated handlers as defaults
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
// per-test override
server.use(http.get('*/api/invoices', () => HttpResponse.json({ title: 'Boom' }, { status: 500 })));
```
  Assert on UI outcome, not "request was called" (MSW best practice: avoid request assertions).
- Every data screen tests: loading → success, empty, error+retry, validation errors, RTL render snapshot of layout direction (`dir="rtl"`).
- **A11y in component tests**: `expect(await axe(container)).toHaveNoViolations()`.
- **Vitest 4 browser mode** (Playwright provider) for components needing real layout/CSS; visual regression with `await expect(page.getByRole('main')).toMatchScreenshot()` for design-system components at 390/1280.
- **E2E (Playwright, deterministic)**: critical journeys only; `getByRole` locators; web-first assertions `await expect(locator).toBeVisible()`; auth via setup project + `storageState` (`playwright/.auth` gitignored); `trace: 'on-first-retry'`; `retries: 2` in CI only; `@axe-core/playwright` scan per page.
- Coverage: `vitest run --coverage` (v8 provider); thresholds lines 80 / branches 70 on `features/**`, exclude `generated/`.
- **Flakiness rules**: no `setTimeout`/`sleep`/`waitForTimeout`; no reliance on test order; fake timers only with `vi.useFakeTimers({ shouldAdvanceTime: true })`; freeze time/locale/timezone (`TZ=UTC`); a test that flakes twice is quarantined (`test.fixme` + ticket) within 24h, never silently retried forever.

### 1.18 React DO / DON'T (+ common LLM mistakes)

1. DO fetch via TanStack Query/route loaders. DON'T `useEffect(() => { fetch().then(setData) }, [])`.
2. DO derive values during render. DON'T mirror props/query data into state with `useEffect` (`set-state-in-effect` lint).
3. DON'T use `forwardRef` in new code — React 19: `ref` is a regular prop. DON'T use `Context.Provider` — render `<Context value={...}>`.
4. DON'T use `ReactDOM.render`/`hydrate`, `defaultProps` on function components, string refs, `propTypes` — removed in 19.
5. DO use `useActionState`/`useFormStatus`/`useOptimistic` only where they fit; for RHF forms keep RHF's `handleSubmit`. DON'T mix both on one form.
6. DON'T sprinkle `useMemo/useCallback/memo` "for performance" — the compiler does it.
7. DON'T write Tailwind v3 config (`tailwind.config.js` `theme.extend`, `@tailwind base;` directives, `tailwindcss-animate`). DO `@import "tailwindcss"` + `@theme`, `tw-animate-css`.
8. DON'T use v3 names renamed in v4: `shadow-sm`→`shadow-xs`, `rounded`→`rounded-sm` semantics shifted, `outline-none`→`outline-hidden`, `bg-opacity-*` → `bg-black/50`, `flex-shrink-0`→`shrink-0`.
9. DON'T use Zod 3 API (`z.string().email()`, `{ message }`, `required_error`). DO `z.email()`, `{ error }`.
10. DON'T use TanStack Query v4 API (positional args, `isLoading`, `cacheTime`, `onSuccess` on queries, `keepPreviousData: true`).
11. DON'T use React Router v6 idioms (`useNavigate` from react-router, `<Routes>`) in a TanStack Router app.
12. DON'T configure plugin-react with `babel:` on Vite 8 — use `@rolldown/plugin-babel` + `reactCompilerPreset()`.
13. DON'T hand-write API types/fetchers for endpoints in the OpenAPI spec. DON'T edit `generated/`.
14. DON'T put tokens in localStorage. DON'T expose secrets via `VITE_`.
15. DON'T use `ml-4`, `left-0`, `text-left` — use logical `ms-4`, `start-0`, `text-start`.
16. DON'T hardcode user-facing strings; DON'T concatenate translated fragments (breaks Arabic word order) — use interpolation.
17. DON'T use `div onClick` — use `<button>`. DON'T remove focus outlines without `focus-visible` replacement.
18. DON'T use array index as `key` for dynamic lists.
19. DON'T use `any`/`as` casts to silence types; DO parse unknown input with Zod.
20. DON'T use `getByTestId` first or `container.querySelector` in tests; DON'T `act()`-wrap user-event calls manually.
21. DON'T mock `fetch`/axios with `vi.mock` — use MSW.
22. DON'T use CRA/`react-scripts`, Jest + babel-jest for a Vite app, Enzyme.
23. DO render every data view's loading/empty/error states; LLMs routinely only build the happy path.
24. DO pass `signal` to fetchers for cancellation.

---

## 2. FLUTTER (mobile)

### 2.1 Scaffold

```bash
flutter --version                      # 3.47.x stable, Dart 3.12+
flutter create --org com.acme --platforms=android,ios,web --empty app   # or: very_good create flutter_app app
cd app
flutter pub add flutter_riverpod riverpod_annotation go_router dio freezed_annotation json_annotation \
  flutter_secure_storage intl flutter_localizations --sdk=flutter # (localizations is sdk dep)
flutter pub add dev:build_runner dev:riverpod_generator dev:freezed dev:json_serializable \
  dev:go_router_builder dev:very_good_analysis dev:mocktail dev:alchemist dev:riverpod_lint
dart run build_runner watch -d
```
Bloc alternative (team-approved, pick one per app, never both): `flutter_bloc` 9.x + `bloc_test`. Default = **Riverpod 3 with codegen**.

Material: opt into standalone `material_ui` package (1.0 in Flutter 3.47; in-SDK Material slated for deprecation). go_router 18 already depends on it.

### 2.2 Layout (feature-first, layered)

```
lib/
  main.dart / main_dev.dart / main_prod.dart      # flavors
  app/            app.dart (MaterialApp.router), router.dart, bootstrap.dart
  core/
    design_system/ tokens.g.dart (generated), theme.dart, app_spacing.dart, widgets/ (DS components)
    network/       dio_client.dart, auth_interceptor.dart, api_exception.dart
    storage/       secure_storage.dart
    l10n/          arb/app_en.arb, app_ar.arb (+ generated)
  features/
    invoices/
      data/        invoices_repository.dart, dto/ (generated or freezed), invoices_api.dart
      domain/      invoice.dart (freezed), invoice_status.dart
      application/ invoices_controller.dart (@riverpod Notifier/AsyncNotifier)
      presentation/ invoices_page.dart, widgets/
  gen/            openapi client output (if generated)
test/  (mirrors lib/)   integration_test/   test/goldens/
```

### 2.3 State & data fetching (Riverpod 3)

```dart
@riverpod
InvoicesRepository invoicesRepository(Ref ref) => InvoicesRepository(ref.watch(dioProvider));

@riverpod
Future<List<Invoice>> invoices(Ref ref, {required InvoiceFilter filter}) =>
    ref.watch(invoicesRepositoryProvider).list(filter, cancelToken: ref.cancelToken());

@riverpod
class InvoiceEditor extends _$InvoiceEditor {
  @override
  FutureOr<Invoice?> build(String id) => ref.watch(invoicesRepositoryProvider).get(id);
  Future<void> save(Invoice draft) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() => ref.read(invoicesRepositoryProvider).update(draft));
    if (!ref.mounted) return;                       // Riverpod 3
    ref.invalidate(invoicesProvider);
  }
}
```
- UI: `switch (ref.watch(invoicesProvider(filter: f))) { AsyncData(:final value) => ..., AsyncError(:final error) => ErrorView(...), _ => const InvoicesSkeleton() }` — Dart 3 patterns, exhaustive.
- Riverpod 3 retries failed providers automatically (exponential backoff 200ms→6.4s); disable per provider (`retry: (_, __) => null`) for non-idempotent/4xx cases.
- `StateProvider`, `StateNotifierProvider`, `ChangeNotifierProvider` are **legacy** (`package:riverpod/legacy.dart`) — don't use.
- `ref.watch` in build, `ref.read` in callbacks, `ref.listen` for side effects (snackbars, navigation).

### 2.4 Models (freezed 4 + json_serializable)

```dart
@freezed
sealed class Invoice with _$Invoice {
  const factory Invoice({required String id, required Money total, @Default(InvoiceStatus.draft) InvoiceStatus status}) = _Invoice;
  factory Invoice.fromJson(Map<String, dynamic> json) => _$InvoiceFromJson(json);
}
```
Unions: `sealed` + Dart `switch` patterns (no `when/map` — removed in freezed 3). Keep DTOs (API) separate from domain models when shapes diverge.

### 2.5 Routing (go_router 18 + go_router_builder)

```dart
@TypedGoRoute<InvoiceRoute>(path: '/invoices/:id')
class InvoiceRoute extends GoRouteData with $InvoiceRoute {
  const InvoiceRoute({required this.id});
  final String id;
  @override Widget build(BuildContext c, GoRouterState s) => InvoicePage(id: id);
}
// navigation: const InvoiceRoute(id: '42').go(context);
final routerProvider = Provider((ref) => GoRouter(
  refreshListenable: ref.watch(authListenableProvider),
  redirect: (ctx, state) => authRedirect(ref, state),   // single central guard
  routes: $appRoutes,
));
```
- `StatefulShellRoute.indexedStack` for bottom tabs with preserved state; each navigator has a top-level `GlobalKey<NavigatorState>`.
- Path params for IDs, query params for filters, `extra` only for transient objects (not deep-linkable).
- URLs are case-sensitive (≥ v15). Test redirects in unit tests.

### 2.6 API client generation

- Preferred: `openapi-generator` `dart-dio` (built_value or json_serializable variant) into `lib/gen/api` via `openapi_generator` build_runner annotation, or `swagger_parser`/`openapi_retrofit_generator` (retrofit + freezed/json_serializable). Choose one; CI regenerates and diffs.
```bash
npx @openapitools/openapi-generator-cli generate -i ../contracts/openapi.yaml -g dart-dio \
  -o packages/api_client --additional-properties=serializationLibrary=json_serializable,pubName=api_client
```
- Put the generated client in a local package (`packages/api_client`) so analyzer exclusions and regeneration are isolated. Repositories wrap it and map DTO → domain + `DioException` → `ApiException` (sealed: `Unauthorized`, `Validation(fieldErrors)`, `NotFound`, `Network`, `Server`).

### 2.7 Auth tokens

- Store refresh/access tokens with `flutter_secure_storage` 11 (Keychain / Android Keystore-backed). Do not pass removed `encryptedSharedPreferences`. iOS: `KeychainAccessibility.first_unlock_this_device` for tokens (no iCloud sync).
- Never SharedPreferences for tokens. Cache access token in memory.
- Dio `QueuedInterceptorsWrapper` for single-flight refresh on 401; on refresh failure → clear storage, invalidate `authProvider`, router redirect to `/login`.
- Optional biometric gate (`requireBiometricsPerOperation` Android flag in 11.2).

### 2.8 Certificate pinning / network security

- Pin SPKI hashes (primary + backup) in Dio: `IOHttpClientAdapter(createHttpClient: () => HttpClient(context: SecurityContext(withTrustedRoots: false)..setTrustedCertificatesBytes(pem)))` or a vetted package (`http_certificate_pinning`). Keep pins remotely rotatable; include backup pin; ship kill-switch via config.
- Android `network_security_config.xml`: `cleartextTrafficPermitted="false"`. iOS ATS on.
- Obfuscate release: `flutter build appbundle --obfuscate --split-debug-info=build/symbols`.
- Don't log tokens/PII (`LogInterceptor` only in dev flavor, `requestHeader: false`).

### 2.9 Env config

`--dart-define-from-file=env/dev.json` (+ flavors via `--flavor dev -t lib/main_dev.dart`). Read via `const String.fromEnvironment('API_BASE_URL')` in one `Env` class. Anything in the binary is public — no secrets.

### 2.10 Theming from design tokens

- Generate `tokens.g.dart` from design-system.md (colors, spacing, radii, typography, durations). Expose via `ThemeExtension<AppTokens>` + `ColorScheme`/`TextTheme` built from tokens.
```dart
extension TokensX on BuildContext { AppTokens get tokens => Theme.of(this).extension<AppTokens>()!; }
Padding(padding: EdgeInsetsDirectional.all(context.tokens.space4), child: ...)
```
- DON'T `Color(0xFF...)`, `Colors.blue`, `EdgeInsets.all(13)`, `TextStyle(fontSize: 17)` in features. Lint via custom analyzer plugin (Flutter 3.44+ supports custom analyzer plugins) or DCM `avoid-hardcoded-colors`, plus CI grep.
- Dark theme = second token set; `ThemeMode.system`.

### 2.11 i18n + RTL

```yaml
# pubspec.yaml
flutter: { generate: true }
# l10n.yaml
arb-dir: lib/core/l10n/arb
template-arb-file: app_en.arb
output-localization-file: app_localizations.dart
nullable-getter: false
```
- `MaterialApp.router(localizationsDelegates: AppLocalizations.localizationsDelegates, supportedLocales: AppLocalizations.supportedLocales)`; access via `context.l10n.x` extension.
- Arabic plurals in ARB with `zero/one/two/few/many/other`.
- RTL: Flutter mirrors automatically from locale — so use **directional** APIs: `EdgeInsetsDirectional`, `AlignmentDirectional`, `PositionedDirectional`, `BorderRadiusDirectional`, `TextAlign.start/end`. Ban `EdgeInsets.only(left:` / `Alignment.centerLeft` in features. Directional icons: `Icons.adaptive.arrow_back` or `Transform.flip(flipX: Directionality.of(context) == TextDirection.rtl)`.
- Dates/numbers via `intl` `DateFormat.yMMMd(locale)`, `NumberFormat.currency(locale:)`.

### 2.12 Accessibility (Flutter)

- Every tappable ≥ 48×48 dp (Android) / 44×44 pt (iOS); Material widgets do it — custom `GestureDetector` must be wrapped (`ConstrainedBox(minWidth:48,minHeight:48)`) or, better, use `InkWell`/buttons.
- `Semantics(label:, button: true, header: true, ...)` for custom widgets; `MergeSemantics` for label+value rows; `ExcludeSemantics` for decorative; `Image(semanticLabel:)` or `excludeFromSemantics: true`.
- Text scaling: never fix heights on text containers; test with `MediaQuery(textScaler: TextScaler.linear(2.0))`.
- Focus/keyboard (web/desktop/hardware keyboards): `FocusTraversalGroup`, `Shortcuts/Actions`, visible focus from theme `focusColor` token.
- Contrast from tokens (verified once). Announce async results with `SemanticsService.announce` (or live region `Semantics(liveRegion: true)`).
- **Flutter web**: semantics DOM is off by default. For the Agy browser E2E and screen readers, call `SemanticsBinding.instance.ensureSemantics()` when `kIsWeb` (at least in dev/e2e flavor) so labels/roles appear as ARIA in the DOM; use `Semantics(identifier: 'invoice-save')` for stable automation hooks (rendered as `flt-semantics-identifier`).

### 2.13 Performance

- `const` constructors everywhere possible (lint enforces). Split widgets instead of helper methods returning widgets.
- Lists: `ListView.builder`/`SliverList.builder`, `itemExtent`/`prototypeItem` when fixed; never `Column` of 100+ children or `shrinkWrap: true` inside scroll views.
- Images: `cached_network_image` with `memCacheWidth` sized to display; precache hero images.
- `ref.watch(provider.select((s) => s.field))` to narrow rebuilds.
- Heavy JSON parsing → `compute()`/`Isolate.run`.
- Profile in `--profile` mode with DevTools; Impeller default on Android/iOS/desktop.

### 2.14 Lint / format

```yaml
# analysis_options.yaml
include: package:very_good_analysis/analysis_options.yaml   # v11
analyzer:
  exclude: ["**/*.g.dart", "**/*.freezed.dart", "lib/gen/**", "packages/api_client/**"]
  errors: { invalid_annotation_target: ignore }
  language: { strict-casts: true, strict-inference: true, strict-raw-types: true }
plugins:
  riverpod_lint: ^3.0.0
```
Commands: `dart format --set-exit-if-changed .` · `dart analyze --fatal-infos` · `dart fix --apply`.

### 2.15 Build / CI

```bash
flutter pub get
dart run build_runner build -d
dart format --set-exit-if-changed . && dart analyze --fatal-infos
flutter test --coverage --test-randomize-ordering-seed random
flutter test --tags golden            # on pinned Linux runner only
flutter build web --release --dart-define-from-file=env/e2e.json   # for Agy browser E2E
flutter build appbundle --flavor prod --obfuscate --split-debug-info=build/symbols
flutter build ipa --flavor prod
```

### 2.16 Testing conventions (Flutter)

- **Unit**: repositories, controllers (`ProviderContainer.test()` auto-disposes in Riverpod 3), mappers, validators. Mocks: `mocktail` (no codegen).
```dart
test('loads invoices', () async {
  final repo = MockInvoicesRepository();
  when(() => repo.list(any(), cancelToken: any(named: 'cancelToken'))).thenAnswer((_) async => [fakeInvoice]);
  final c = ProviderContainer.test(overrides: [invoicesRepositoryProvider.overrideWithValue(repo)]);
  expect(await c.read(invoicesProvider(filter: InvoiceFilter.all).future), [fakeInvoice]);
});
```
- **Widget tests** (bulk): `pumpApp` helper wrapping `ProviderScope(overrides:)`, `MaterialApp` with l10n delegates + theme tokens; `find.bySemanticsLabel`, `find.text(l10n.x)`, `find.byType`; `find.byKey` last resort. Always test loading/data/empty/error. Use `tester.pumpAndSettle()` only when no infinite animations (skeleton shimmers → `pump(duration)`).
- **A11y** in widget tests: `meetsGuideline(androidTapTargetGuideline)`, `iOSTapTargetGuideline`, `labeledTapTargetGuideline`, `textContrastGuideline` with `tester.ensureSemantics()`; dispose handle.
- **RTL test**: pump each screen with `locale: Locale('ar')` and assert no overflow (`tester.takeException()` is null) + golden.
- **Golden tests**: `alchemist` (CI goldens with Ahem-font rendering are platform-agnostic; platform goldens only for local review). Scenarios: light/dark × en/ar × 390-wide phone. Tag `@Tags(['golden'])`; update with `flutter test --update-goldens --tags golden` only in a dedicated commit.
- **Bloc** (if used): `blocTest<Cubit, State>(build:, act:, expect: () => [...])`.
- **Integration/E2E native**: `integration_test` + **Patrol** for native dialogs/permissions; or Maestro flows (works with Flutter via semantics) — used by agents through Maestro MCP.
- Coverage: `flutter test --coverage && lcov --remove coverage/lcov.info '**/*.g.dart' '**/*.freezed.dart' 'lib/gen/*' -o coverage/lcov.info`; gate 80% on `features/*/application` + `data`.
- Flakiness: no real network (Dio mocked via repository override or `http_mock_adapter`), fixed clock via `clock` package `withClock`, fixed locale, random test order in CI, no `Future.delayed` in tests.

### 2.17 Flutter DO / DON'T (+ LLM mistakes)

1. DON'T use `StateNotifier`, `StateProvider`, `ChangeNotifierProvider`, Riverpod 2 `AutoDisposeNotifier`/`FamilyNotifier`/`XxxRef` types — Riverpod 3 unified `Notifier` and `Ref`.
2. DON'T call freezed `.when()/.map()` — use `switch` patterns. DON'T declare freezed classes without `sealed`/`abstract`.
3. DON'T use `WillPopScope` (use `PopScope` + `onPopInvokedWithResult`), `MaterialStateProperty` (use `WidgetStateProperty`), `textScaleFactor` (use `textScaler`), `Color.withOpacity` (use `withValues(alpha:)`), `ButtonBar`, `ThemeData.useMaterial3: false`.
4. DON'T use `Navigator.push(MaterialPageRoute(...))` for app routes — go_router typed routes. DON'T pass domain objects via `extra` for deep-linkable screens.
5. DON'T store tokens in `SharedPreferences`; DON'T use `encryptedSharedPreferences` (removed in secure_storage 11).
6. DON'T hardcode `Color(0xFF…)`, `EdgeInsets.all(16)` in features — tokens.
7. DON'T use `EdgeInsets.only(left:)`/`Alignment.centerLeft`/`TextAlign.left` — directional versions.
8. DON'T hardcode strings — ARB + `context.l10n`.
9. DON'T use `setState` for app/server state; DON'T create providers inside `build`.
10. DON'T call `ref.read` in `build` to subscribe; DON'T `ref.watch` inside callbacks.
11. DON'T use `BuildContext` across async gaps without `if (!context.mounted) return;` (and `ref.mounted` in notifiers).
12. DON'T write helper methods returning widgets (`Widget _buildHeader()`) — extract `const` widget classes.
13. DON'T put `ListView(shrinkWrap: true)` inside `SingleChildScrollView` for long lists.
14. DON'T use `GestureDetector` for buttons (no semantics, small target) — use buttons/`InkWell` with `Semantics`.
15. DON'T use `print` — `dart:developer` `log` or a logger behind a flavor flag (`avoid_print` lint).
16. DON'T commit generated files inconsistently — pick "commit generated" (team default: commit `*.g.dart`/`*.freezed.dart` for reproducible reviews) and CI-verify with `build_runner build` + `git diff --exit-code`.
17. DON'T use `http` package ad-hoc per feature — one Dio client with interceptors.
18. DON'T forget `ensureSemantics()` on web E2E builds — the Agy browser agent sees only a canvas otherwise.
19. DON'T mix Riverpod and Bloc in one app.
20. DON'T use `dynamic` JSON maps in UI — typed models only.
21. DON'T use CocoaPods-only plugins for new iOS work — SwiftPM is default (3.44+).
22. DO provide loading/empty/error for each `AsyncValue`; LLMs often render only `AsyncData`.

---

## 3. KMP (Kotlin Multiplatform + Compose Multiplatform)

### 3.1 Scaffold

- Generate via **kmp.jetbrains.com** (or IDE KMP wizard): targets Android + iOS (+ web wasmJs for Agy browser E2E, + desktop optional). New default structure (AGP 9):
```
shared/                      # KMP library: com.android.kotlin.multiplatform.library + compose
  src/commonMain/kotlin/com/acme/
    app/        App.kt, navigation (Nav3), di/AppModule.kt
    core/       designsystem/ (Tokens.kt generated, AppTheme.kt, components/), network/, storage/, result/
    feature/invoices/
      data/     InvoicesApi.kt, InvoicesRepositoryImpl.kt, dto/
      domain/   Invoice.kt, InvoicesRepository.kt, usecase/
      presentation/ InvoicesScreen.kt, InvoicesViewModel.kt, InvoicesUiState.kt
  src/commonMain/composeResources/  values/strings.xml, values-ar/strings.xml, drawable/, font/
  src/commonMain/sqldelight/com/acme/db/*.sq
  src/androidMain/ src/iosMain/ src/wasmJsMain/
  src/commonTest/ src/androidHostTest/ ...
androidApp/   iosApp/ (Xcode)   webApp/   desktopApp/
gradle/libs.versions.toml
```
Mixed native/shared UI → split `sharedLogic` + `sharedUI`.

Core deps (version catalog): Kotlin 2.4.x, CMP 1.12.x, kotlinx-coroutines, kotlinx-serialization-json, kotlinx-datetime, Ktor 3.6 (`ktor-client-core`, `-content-negotiation`, `-serialization-kotlinx-json`, `-auth`, `-logging`, engines `-okhttp` android / `-darwin` iOS / `-js` wasm), Koin 4.1+ (`koin-compose-viewmodel`), `org.jetbrains.androidx.lifecycle:lifecycle-viewmodel-compose`, Navigation 3 (`org.jetbrains.androidx.navigation3:navigation3-ui` 1.1.1), SQLDelight 2.x (or Room KMP), Coil 3 (images), Mokkery, Turbine, kotlin-test.

### 3.2 Architecture, state & data

- MVVM + UDF: `ViewModel` exposes `StateFlow<UiState>`; UI sends events via functions; one-shot effects via `Channel<Effect>(BUFFERED).receiveAsFlow()`.
```kotlin
@Immutable data class InvoicesUiState(val items: List<InvoiceUi> = emptyList(), val isLoading: Boolean = true,
                                       val error: UiText? = null) { val isEmpty get() = !isLoading && error == null && items.isEmpty() }
class InvoicesViewModel(private val repo: InvoicesRepository) : ViewModel() {
  private val _state = MutableStateFlow(InvoicesUiState()); val state = _state.asStateFlow()
  init { refresh() }
  fun refresh() = viewModelScope.launch {
    _state.update { it.copy(isLoading = true, error = null) }
    repo.list().fold(onSuccess = { list -> _state.update { it.copy(isLoading = false, items = list.map(::toUi)) } },
                     onFailure = { e -> _state.update { it.copy(isLoading = false, error = e.toUiText()) } })
  }
}
@Composable fun InvoicesRoute(vm: InvoicesViewModel = koinViewModel()) {
  val state by vm.state.collectAsStateWithLifecycle(); InvoicesScreen(state, onRetry = vm::refresh)
}
```
- Screens are stateless (`InvoicesScreen(state, callbacks)`) → previewable & testable; `Route` composable wires VM.
- Repositories return `Result<T>`/sealed `AppResult`; never throw into UI. Cancellation: rethrow `CancellationException`.
- Offline cache: SQLDelight queries as `Flow` (`asFlow().mapToList(Dispatchers.IO)`), repository = single source of truth.
- Dispatchers injected (`CoroutineDispatcher` via Koin) for testability.

### 3.3 DI (Koin 4.1+)

```kotlin
val invoicesModule = module {
  singleOf(::InvoicesRepositoryImpl) bind InvoicesRepository::class
  viewModelOf(::InvoicesViewModel)
}
@Composable fun App() = KoinApplication(application = { modules(coreModule, platformModule(), invoicesModule) }) { AppTheme { AppNav() } }
```
Platform specifics via `expect fun platformModule(): Module`. Run `verify()` (koin-test) on modules in a JVM test.

### 3.4 Navigation (Navigation 3 — CMP default for new apps)

```kotlin
@Serializable sealed interface Route : NavKey
@Serializable data object InvoicesList : Route
@Serializable data class InvoiceDetail(val id: String) : Route

private val navConfig = SavedStateConfiguration { serializersModule = SerializersModule {
  polymorphic(NavKey::class) { subclassesOfSealed<Route>() } } }   // required on iOS/web (no reflection)

@Composable fun AppNav() {
  val backStack = rememberNavBackStack(navConfig, InvoicesList)
  NavDisplay(backStack = backStack, onBack = { backStack.removeLastOrNull() },
    entryProvider = entryProvider {
      entry<InvoicesList> { InvoicesRoute(onOpen = { backStack.add(InvoiceDetail(it)) }) }
      entry<InvoiceDetail> { key -> InvoiceDetailRoute(key.id) }
    })
}
```
Auth gating: root decides between `AuthFlow` and `MainFlow` back stacks from `authState` flow. Web deep links: `navigation3-browser` binding. (Existing apps on `navigation-compose` type-safe routes may stay; migration is a rewrite.)

### 3.5 Networking, API generation, auth

```kotlin
fun httpClient(engine: HttpClientEngine, tokens: TokenStore, env: Env) = HttpClient(engine) {
  expectSuccess = true
  install(ContentNegotiation) { json(Json { ignoreUnknownKeys = true; explicitNulls = false }) }
  install(HttpTimeout) { requestTimeoutMillis = 15_000 }
  install(Logging) { level = if (env.debug) LogLevel.HEADERS else LogLevel.NONE; sanitizeHeader { it == HttpHeaders.Authorization } }
  install(Auth) { bearer {
    loadTokens { tokens.load() }
    refreshTokens { tokens.refresh(client, oldTokens) /* uses markAsRefreshTokenRequest() */ }
    sendWithoutRequest { it.url.host == env.apiHost }
  } }
  defaultRequest { url(env.baseUrl); header("Accept-Language", currentLanguageTag()) }
}
```
- On logout: clear storage **and** `client.authProvider<BearerAuthProvider>()?.clearToken()`.
- Map `ClientRequestException`/`ServerResponseException`/`IOException` → sealed `ApiError`.
- **OpenAPI generation**: the stock `openapi-generator -g kotlin --library multiplatform` templates are dated (docs list Ktor 1.6.7 / serialization 1.2). Team default: generate **models + Ktor client** with a KMP-native generator (`openapi-kmp-gen`, `openapi2ktor`, or Fabrikt for kotlinx.serialization models) into `:shared` `build/generated` via a Gradle task wired to `compileKotlinMetadata`; or generate models only and hand-write thin `InvoicesApi` with Ktor. Either way, CI regenerates and fails on drift.
- **Secure storage**: Android Keystore-backed (EncryptedFile/DataStore + Tink or `KSafe`), iOS Keychain (`kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly`). Expose `expect interface SecureStore`. `multiplatform-settings` default = **plaintext** → never for tokens.
- **Certificate pinning**: Android `OkHttp` engine `config { certificatePinner(CertificatePinner.Builder().add(host, "sha256/…primary", "sha256/…backup").build()) }`; iOS Darwin engine `handleChallenge(CertificatePinner.Builder().add(host, "sha256/…").build())`. Backup pin mandatory.

### 3.6 Theming from tokens

- Generate `Tokens.kt` (object with `Color`, `Dp`, `TextUnit`, `Shape`) from design-system.md. Build `MaterialTheme(colorScheme = lightScheme(tokens), typography = appTypography(tokens), shapes = ...)` and a `LocalAppTokens` `staticCompositionLocalOf` for non-M3 tokens (spacing, elevation, durations).
```kotlin
object AppTheme { val tokens: AppTokens @Composable get() = LocalAppTokens.current }
Modifier.padding(AppTheme.tokens.space.md)
```
- DON'T `Color(0xFF…)`, `16.dp`, `14.sp` literals in `feature/**` — detekt custom rule / `compose-rules` + CI grep.
- Fonts via `composeResources/font` (`Font(Res.font.plex_arabic)`).

### 3.7 i18n + RTL

- Strings in `composeResources/values/strings.xml` & `values-ar/strings.xml`; `stringResource(Res.string.x)`; plurals via `<plurals>` + `pluralStringResource` (Arabic needs zero/one/two/few/many/other). In ViewModels, use `UiText` wrapper (`StringResource` + args), resolve in UI; suspend `getString()` when needed outside composition.
- RTL comes from `LocalLayoutDirection` (set by platform locale). Compose padding `start/end` are already directional — **never** use `absolutePadding`/`Alignment.Absolute*` unless intentional. Mirror directional icons with `Icons.AutoMirrored.*` or `Modifier.graphicsLayer { scaleX = if (rtl) -1f else 1f }`.
- Force Arabic in tests/previews: `CompositionLocalProvider(LocalLayoutDirection provides LayoutDirection.Rtl)`.
- Dates/numbers: `kotlinx-datetime` + platform formatters (`expect fun formatMoney(...)`).

### 3.8 Accessibility (Compose)

- Touch targets ≥ 48dp: Material 3 components enforce via `minimumInteractiveComponentSize()`; custom clickables add it explicitly.
- `contentDescription` for meaningful icons (from strings), `null` for decorative.
- `Modifier.semantics(mergeDescendants = true)` for list rows; `semantics { heading() }` on section titles; `Role.Button/Checkbox` on custom clickables (`Modifier.clickable(role = Role.Button, onClickLabel = …)`); `stateDescription` for toggles; `liveRegion = LiveRegionMode.Polite` for async status (CMP 1.12 adds iOS LiveRegion support).
- `testTag` on key nodes for tests; for web E2E ensure the semantics tree is exposed (CMP web a11y) and prefer text/role targets.
- Font scaling: use `sp` tokens; never fixed-height text containers.
- Keyboard/focus (web/desktop): `Modifier.focusable()`, `onKeyEvent`, visible focus indication from tokens.

### 3.9 Performance

- Stable UI models: `@Immutable`/`@Stable`, `ImmutableList` (kotlinx.collections.immutable) — strong skipping is default but unstable collections still hurt.
- `LazyColumn(items, key = { it.id }, contentType = …)`; never `Column(verticalScroll)` for long lists.
- `derivedStateOf` for scroll-derived state; lambda-based modifiers (`Modifier.offset { }`) for animated values; `remember` expensive calculations.
- Images: Coil 3 `AsyncImage` with size constraints and placeholders; cache configured once.
- Enable Compose compiler reports in CI for regressions; Baseline Profiles for Android release.

### 3.10 Lint / format

- **ktlint** (via `org.jlleitschuh.gradle.ktlint` or Spotless) + **detekt** with `io.nlopez.compose.rules:detekt` (0.6.x) and `:ktlint` compose rules. `.editorconfig`: `ktlint_code_style = ktlint_official`, `ktlint_function_naming_ignore_when_annotated_with = Composable`.
- Compiler: `allWarningsAsErrors = true` in CI, `explicitApi()` for library modules.
```bash
./gradlew ktlintCheck detekt
```

### 3.11 Build / CI

```bash
./gradlew :shared:allTests                      # all targets' tests (commonTest runs per target)
./gradlew :shared:jvmTest :shared:iosSimulatorArm64Test :shared:wasmJsTest
./gradlew :androidApp:assembleRelease :androidApp:bundleRelease
./gradlew :webApp:wasmJsBrowserDistribution     # static build for Agy browser E2E
xcodebuild -project iosApp/iosApp.xcodeproj -scheme iosApp -sdk iphonesimulator build
./gradlew koverXmlReport koverVerify            # coverage
```
Use Gradle configuration cache + build cache; JDK 21 toolchain; macOS runner only for iOS jobs.

### 3.12 Testing conventions (KMP)

- **Common unit tests** (`commonTest`, `kotlin.test`): domain, mappers, ViewModels, repositories.
```kotlin
class InvoicesViewModelTest {
  private val repo = mock<InvoicesRepository> { everySuspend { list() } returns Result.success(listOf(fake)) } // Mokkery
  @BeforeTest fun setUp() = Dispatchers.setMain(StandardTestDispatcher())
  @AfterTest fun tearDown() = Dispatchers.resetMain()
  @Test fun emitsLoadingThenData() = runTest {
    val vm = InvoicesViewModel(repo)
    vm.state.test {                                  // Turbine
      assertTrue(awaitItem().isLoading)
      advanceUntilIdle()
      assertEquals(1, awaitItem().items.size)
      cancelAndIgnoreRemainingEvents()
    }
    verifySuspend { repo.list() }
  }
}
```
- Mocking: **Mokkery** (compiler plugin, all targets; mocks interfaces/open classes — enable all-open for tested classes or depend on interfaces). MockK only in `androidHostTest`/`jvmTest`. Prefer **hand-written fakes** for repositories (simplest, multiplatform).
- HTTP: Ktor `MockEngine` with JSON fixtures in `commonTest/resources`; test error mapping for 401/422/500/timeouts.
- DB: SQLDelight in-memory driver per platform (`JdbcSqliteDriver(IN_MEMORY)` on JVM, `NativeSqliteDriver` inMemory on iOS) via `expect fun testDriver()`.
- **Compose UI tests from common code**: `org.jetbrains.compose.ui:ui-test` in `commonTest`; `@OptIn(ExperimentalTestApi::class) fun t() = runComposeUiTest { setContent { InvoicesScreen(state, {}) }; onNodeWithText("…").assertIsDisplayed(); onNodeWithTag("retry").performClick() }` (use `androidx.compose.ui.test.v2.runComposeUiTest`). Runs on jvm, iosSimulatorArm64, wasmJs; Android via `connectedAndroidTest`.
- Finder priority: `onNodeWithText`/`onNodeWithContentDescription` (user-visible) → `hasRole`/semantics matchers → `onNodeWithTag` (stable fallback). Assert a11y: `assertHasClickAction`, `assertContentDescriptionEquals`, `assertHeightIsAtLeast(48.dp)`.
- **Screenshot tests**: Roborazzi (JVM/Robolectric on Android; also desktop/iOS support) or Paparazzi for Android; auto-generate from `@Preview`s with ComposablePreviewScanner. Matrix: light/dark × en/ar(RTL) × phone 390dp. Record only on the pinned CI image (`./gradlew recordRoborazziDebug`, verify with `verifyRoborazziDebug`).
- Coverage: Kover (`koverVerify` min 80% line on `feature/*/domain|data|presentation` VMs).
- Flakiness: `runTest` + injected `TestDispatcher` (never `Dispatchers.IO` hardcoded), `kotlinx-datetime` `Clock` injected, no `delay()` waits in tests (use `advanceTimeBy`), Turbine `cancelAndIgnoreRemainingEvents()`, `awaitItem()` with explicit expectations.

### 3.13 KMP DO / DON'T (+ LLM mistakes)

1. DON'T use `kotlin-android-extensions`, `kapt` for new code (use KSP), `LiveData` in shared code (use `StateFlow`).
2. DON'T use `GlobalScope`/`runBlocking` in app code; DON'T swallow `CancellationException` in `catch (e: Exception)`.
3. DON'T use Gson/Moshi/Jackson in shared code — kotlinx.serialization only. DON'T use Retrofit/OkHttp APIs in `commonMain`.
4. DON'T use `java.*` (`java.util.Date`, `UUID.randomUUID`, `String.format`) in `commonMain` — `kotlinx-datetime`, `kotlin.uuid.Uuid` (stable in 2.4), platform formatters.
5. DON'T use the old `composeApp` single-module template for new projects on AGP 9 — `shared` + app modules; `com.android.kotlin.multiplatform.library` plugin.
6. DON'T use `android.R`/`R.string` in shared UI — `Res.string` from compose resources.
7. DON'T use `collectAsState()` for VM flows on Android lifecycle — `collectAsStateWithLifecycle()`.
8. DON'T pass `ViewModel` down the tree; pass state + lambdas. DON'T call `koinInject()` deep inside leaf composables.
9. DON'T use string-based nav routes (`"invoice/{id}"`) — typed `@Serializable` keys; register polymorphic serializers for iOS/web.
10. DON'T hardcode `Color(0xFF…)`/`dp`/`sp` literals in features; DON'T use `MaterialTheme.colorScheme.primary` inline where a semantic token exists.
11. DON'T use `absolutePadding`/`Arrangement.Absolute` unless mirroring must be disabled.
12. DON'T use `Icons.Filled.ArrowBack` (deprecated) — `Icons.AutoMirrored.Filled.ArrowBack`.
13. DON'T use MockK in `commonTest` (JVM only) — Mokkery or fakes.
14. DON'T `Thread.sleep`/real delays in tests; DON'T forget `Dispatchers.setMain`.
15. DON'T store tokens in `multiplatform-settings`/`NSUserDefaults`/`SharedPreferences`.
16. DON'T mutate `List` inside UI state; use immutable copies (`update { it.copy(...) }`).
17. DON'T create `HttpClient` per request — one per app via Koin, closed on shutdown.
18. DON'T use `LaunchedEffect(Unit)` to load data owned by VM — load in VM `init`/intent.
19. DON'T use context receivers (`context(Foo)` old syntax) — Kotlin 2.4 has stable **context parameters** (`context(foo: Foo)`); use sparingly.
20. DON'T generate an API client with openapi-generator `multiplatform` library without checking its Ktor/serialization versions.
21. DO expose previews (`@Preview` in commonMain works since CMP 1.10 unified preview) for every screen state: loading/empty/error/data, LTR and RTL.

---

## 4. E2E AGENT TESTING (Agy = Google Antigravity CLI + Gemini)

### 4.1 What Agy actually supports (Sept 2026)

- Binary `agy`. Interactive TUI by default; **headless**: `agy -p "<prompt>"` with `--output-format text|json|stream-json`, `--json-schema <schema>` (structured output), `--print-timeout` (default 5m — raise it for E2E, e.g. `30m`), `--model`, `--effort low|medium|high`, `--mode default|accept-edits|plan`, `-c/--continue`, `--conversation <id>`, `--sandbox`, `--dangerously-skip-permissions` (only inside disposable CI containers), `--disable-slash-commands`, `--add-dir`.
- JSON envelope: `conversation_id, status (SUCCESS|ERROR|CANCELED|INTERRUPTED|INVALID|…), response, error, duration_seconds, num_turns, structured_output, usage`. Non-zero exit on failure. Parse with `jq`.
- Headless permission model: tools needing approval are **soft-denied** (notice on stderr); workspace file writes auto-allowed; shell commands default "Ask" → denied. Pre-authorize in `~/.gemini/antigravity-cli/settings.json`: `{"permissions":{"allow":["command(npx)","command(curl)","write_file(e2e-artifacts/)"]}}`; `toolPermission` setting (`request-review|strict|always-proceed|proceed-in-sandbox`).
- Customization: `AGENTS.md`/`GEMINI.md` (workspace/global rules), `.agents/skills/<name>/SKILL.md`, hooks, MCP via `agy mcp add|list|enable|disable|remove` (also `~/.gemini/config/mcp_config.json`), `agy inspect` to see loaded config/skills/MCP.
- **Browser**: The **Browser Subagent** (Chrome via CDP/Playwright; separate Chrome profile; screenshots + action recordings saved as artifacts; URL allowlist/denylist) is documented for the **Antigravity IDE**. Google's own codelab states the built-in browser agent "is not yet supported in the terminal-first Antigravity CLI"; the CLI changelog references a built-in Chrome DevTools MCP server (v1.1.11). Known Linux bug: browser subagent fails to download Playwright driver 1.57 (issues #629/#638).
  → **Team default: give Agy a deterministic browser via MCP/skill, don't rely on the implicit subagent**:
  - **Playwright MCP**: `agy mcp add playwright -- npx -y @playwright/mcp@latest --headless --isolated --viewport-size=390x844 --output-dir=e2e-artifacts --caps=vision` (flags: `--headless`, `--isolated`, `--viewport-size`, `--output-dir`, `--storage-state`, `--allowed-hosts`, `--save-session`, `--device`). Uses accessibility snapshots (cheap, label-based) + screenshots.
  - **playwright-cli + skill** (token-efficient): `npm i -g @playwright/cli@latest && playwright-cli install --skills`; commands `open <url>`, `snapshot`, `click <ref>`, `fill <ref> <text>`, `resize <w> <h>`, `screenshot --filename=f`, `console`, `requests`, `state-save/state-load`, `-s=<session>`.
  - **Chrome DevTools MCP**: `navigate_page`, `resize_page`, `emulate`, `take_screenshot`, `take_snapshot`, `list_console_messages`, `list_network_requests`, `performance_*`; flags `--headless --isolated --viewport=1280x800`. Best for console/network/perf evidence.

Canonical CI invocation:
```bash
agy -p "$(cat e2e/run-prompt.md) Plan: e2e/plans/invoices.md BaseURL: $E2E_BASE_URL" \
  --output-format json --json-schema e2e/report.schema.json --print-timeout 30m --effort high \
  > e2e-artifacts/agy-result.json
jq -e '.status=="SUCCESS" and (.structured_output.summary.fail==0)' e2e-artifacts/agy-result.json
```

### 4.2 Environment & test data (before the agent starts)

- App runs from a **production build** (`vite preview`, `flutter build web` served statically, `wasmJsBrowserDistribution`) against a seeded backend or MSW-in-browser mock mode (`VITE_API_MOCK=1`). Never against production.
- Seed via API/SQL script (`e2e/seed.sh`) that creates **namespaced disposable data** (`e2e-<runId>-invoice-1`), fixed users per role (`e2e_admin@…`, `e2e_viewer@…`), passwords from CI secrets. Reset between scenarios or give each scenario its own namespace.
- Pre-authenticate: save Playwright `storageState` per role (`state-save auth/admin.json`) in a setup step; scenarios load it instead of logging in (except the login scenario itself).
- Freeze time-dependent data (seed fixed dates) and locale (`en` and `ar` runs explicitly).
- Flutter web: e2e flavor calls `SemanticsBinding.instance.ensureSemantics()` and sets `Semantics(identifier:)` on key controls; otherwise the agent sees a canvas and must fall back to pixel-guessing (flaky). Same for CMP wasm: ensure semantics/a11y tree is enabled and verify with a snapshot first.

### 4.3 Writing scenarios for an AI browser agent (plan format)

Each scenario = one user journey, atomic numbered steps, each with an **observable expected result**. Use visible labels/roles (what a user reads), never CSS selectors. Include viewport, locale, role, data, and forbidden actions.

```markdown
### SC-INV-03 Create invoice — validation then success
- Priority: P0 | Role: admin (storageState auth/admin.json) | Locale: en, ar | Viewports: 390x844, 1280x800
- Data: customer "E2E-{runId} Acme" exists (seeded). No invoices for it.
- Figma: <frame link> "Invoices / Create / Default" and "/ Error"
- Forbidden: do not change settings, do not delete data not created by this scenario.
Steps:
1. Go to {BaseURL}/invoices. EXPECT heading "Invoices" (h1) visible; empty state text "No invoices yet" and button "Create invoice".
2. Click button "Create invoice". EXPECT dialog titled "New invoice"; focus is inside the dialog on field "Customer".
3. Click "Save" without input. EXPECT inline errors under "Customer" ("Customer is required") and "Amount"; dialog stays open.
4. Select customer "E2E-{runId} Acme"; type "150.00" in "Amount"; click "Save".
   EXPECT dialog closes; toast "Invoice created"; table row with customer "E2E-{runId} Acme" and amount "$150.00", status "Draft".
5. Press Tab from page top until "Create invoice" is focused. EXPECT visible focus ring.
Visual checks: after steps 1, 3, 4 screenshot and compare to Figma frame: layout order, token colors (primary button), spacing rhythm, no overflow/clipping, no horizontal scroll at 390.
RTL (ar): EXPECT dir="rtl", nav on the right, chevrons mirrored, numbers formatted per locale.
Pass criteria: all EXPECTs observed at both viewports.
```

Rules:
- One verb per step; one assertion group per step. Max ~12 steps per scenario; split longer journeys.
- Expected results must be **checkable text/role/state** ("toast 'Invoice created'", "button 'Save' disabled"), not "works correctly".
- Reference elements by accessible name exactly as in i18n (`en.json`) — the implementer must not rename labels without updating the plan.
- Provide `data-testid`/`Semantics(identifier)`/`testTag` **only** as a secondary hint for ambiguous elements.
- Include negative paths (validation, 403 page for viewer role, network error state via mock toggle `?mock=error` in e2e builds).
- Include a11y quick checks per scenario: keyboard reachability, focus visible, dialog `Esc` closes, images have alt, target size visibly ≥ 24px (44 for primary).

### 4.4 Screenshots & visual checks

- Viewports: **390×844** (mobile) and **1280×800** (desktop); resize **before** navigation (`resize 390 844`), full-page screenshots plus element screenshots for components.
- Naming: `e2e-artifacts/<runId>/<scenarioId>/<step>-<slug>-<viewport>-<locale>-<theme>.png` → `SC-INV-03/04-after-save-390-en-light.png`.
- Visual comparison to Figma: the agent reports **specific deltas** (element, expected token/spacing, observed) not "looks good". Deterministic pixel diffs belong to Playwright `toHaveScreenshot`/Vitest `toMatchScreenshot`/goldens, not the agent. The agent's job: layout, hierarchy, copy, state, overflow, RTL mirroring, obvious token misuse.
- Stabilize before capture: wait until skeletons/spinners gone and network idle; disable animations in e2e build (`prefers-reduced-motion` emulate or CSS `*{animation:none!important;transition:none!important}` in e2e flavor).
- Also capture console errors and failed network requests per scenario (`console`, `requests` / `list_console_messages`); any uncaught error = FAIL unless allowlisted.

### 4.5 Playwright practices to copy into agent runs

- **Locators**: role + accessible name > label > text > test id. Never XPath/CSS chains.
- **Web-first assertions**: "wait until visible (≤ 10 s) then check", never check-immediately. Tell the agent: "Before declaring a step failed, wait up to 10 s for the expected state and re-snapshot once."
- **Isolation**: fresh browser context per scenario (`--isolated`), own data namespace, no dependency on previous scenario's state.
- **Auth state**: setup step saves storageState; scenarios reuse it.
- **Soft assertions**: record all failed EXPECTs in a step, continue to the end of the scenario unless navigation is impossible (then BLOCKED for remaining steps).
- **Tracing**: keep screenshots + console + network logs for the first retry (like `trace: 'on-first-retry'`).
- **Parallelism**: parallel only across isolated scenarios; serial for stateful flows sharing data.

### 4.6 Result classification & reporting

Statuses per step and scenario:
- **PASS** — every EXPECT observed with evidence (screenshot path / snapshot excerpt).
- **FAIL** — app state contradicts an EXPECT (wrong text, missing element after 10 s wait, console error, visual defect vs Figma). Must include: first failing step, expected vs observed, screenshot, console/network excerpt.
- **BLOCKED** — precondition not met (app down, seed missing, login impossible, tool permission denied, browser tool failed to launch). Not a product verdict.
- **FLAKY** (derived) — failed, then passed on the single allowed retry with identical inputs. Report as FLAKY with both attempts' evidence; counts as non-blocking warning but opens a ticket.
- **INCONCLUSIVE** — evidence insufficient (e.g., canvas without semantics). Treat as BLOCKED for gating.

Retry policy: **exactly one** retry of a failed scenario in a fresh context. Agent must not change the plan's intent, invent alternative flows, or bypass checks to reach PASS. First wrong step is the root; later failures are consequences.

Report JSON schema (pass to `--json-schema`):
```json
{ "type":"object","required":["runId","summary","scenarios"],
  "properties":{
    "runId":{"type":"string"},
    "summary":{"type":"object","properties":{"pass":{"type":"integer"},"fail":{"type":"integer"},
               "blocked":{"type":"integer"},"flaky":{"type":"integer"}}},
    "scenarios":{"type":"array","items":{"type":"object",
      "required":["id","viewport","locale","status","steps"],
      "properties":{"id":{"type":"string"},"viewport":{"enum":["390x844","1280x800"]},"locale":{"type":"string"},
        "status":{"enum":["PASS","FAIL","BLOCKED","FLAKY","INCONCLUSIVE"]},
        "firstFailingStep":{"type":["integer","null"]},
        "steps":{"type":"array","items":{"type":"object","properties":{
          "n":{"type":"integer"},"status":{"enum":["PASS","FAIL","BLOCKED","SKIPPED"]},
          "expected":{"type":"string"},"observed":{"type":"string"},
          "screenshots":{"type":"array","items":{"type":"string"}},
          "consoleErrors":{"type":"array","items":{"type":"string"}}}}},
        "visualDeltas":{"type":"array","items":{"type":"object","properties":{
          "element":{"type":"string"},"expected":{"type":"string"},"observed":{"type":"string"},
          "severity":{"enum":["blocker","major","minor"]}}}}}}}}}
```
Also write a human `e2e-artifacts/<runId>/report.md` (table per scenario × viewport × locale) for PR comments.

### 4.7 Agent prompt skeleton (`e2e/run-prompt.md`)

```
You are an E2E tester. Use ONLY the browser tools (playwright MCP / playwright-cli). Do not edit source code.
For each scenario in the plan, for each viewport [390x844, 1280x800] and locale listed:
1) new isolated context; load storageState for the role; resize BEFORE navigating.
2) execute steps literally; locate elements by role + accessible name from the plan.
3) after each step: wait ≤10s for EXPECT, take snapshot; screenshot at steps marked or on failure,
   save as e2e-artifacts/{runId}/{scenarioId}/{step:02}-{slug}-{viewport}-{locale}.png
4) collect console errors and failed requests (status ≥ 400) per step.
5) classify PASS/FAIL/BLOCKED; on FAIL retry the scenario once in a fresh context; mark FLAKY if retry passes.
Never mark PASS without evidence. Never work around a failure by using a different flow.
Output must match the provided JSON schema.
```

### 4.8 Mobile stacks and Agy

Agy's browser can only exercise **web builds**. For flutter/kmp: run the Agy pass on the web target (Flutter web with semantics on; CMP wasmJs) at 390px for layout/flow/copy/RTL, and cover native-only behavior (permissions, deep links, keyboard insets, biometric) with Patrol/Maestro (Maestro MCP lets agents drive Android emulators/iOS simulators). Mark scenarios `web-verifiable: yes|no` in the plan so Agy reports native-only ones as SKIPPED, not BLOCKED.

---

## Sources

- Vite 8 announcement — https://vite.dev/blog/announcing-vite8 ; releases — https://vite.dev/releases ; Vite 7 — https://vite.dev/blog/announcing-vite7
- React Compiler + plugin-react v6 on Vite 8 — https://recca0120.github.io/en/2026/04/14/react-compiler-vite-v6/
- React Compiler 1.0 — https://react.dev/blog/2025/10/07/react-compiler-1
- eslint-plugin-react-hooks rules — https://react.dev/reference/eslint-plugin-react-hooks
- React 19.2 — https://blog.logrocket.com/react-19-2-is-here/
- TypeScript 7.0 release — https://www.infoq.com/news/2026/08/typescript-7-released/ ; TS 6.0 — https://devblogs.microsoft.com/typescript/announcing-typescript-6-0/
- typescript-eslint typed linting — https://typescript-eslint.io/getting-started/typed-linting/ ; shared configs — https://typescript-eslint.io/users/configs/
- Tailwind v4 upgrade guide — https://tailwindcss.com/docs/upgrade-guide ; v4.0 — https://tailwindcss.com/blog/tailwindcss-v4 ; 4.2/4.3 — https://jawadhassan.dev/blog/tailwind-css-4-3-release
- shadcn/ui Tailwind v4 — https://ui.shadcn.com/docs/tailwind-v4 ; changelog — https://ui.shadcn.com/docs/changelog
- TanStack Router + Query — https://tanstack.com/router/latest/docs/integrations/query ; external data loading — https://tanstack.com/router/v1/docs/framework/react/guide/external-data-loading
- TanStack Query v5 migration — https://tanstack.com/query/latest/docs/framework/react/guides/migrating-to-v5
- Zod 4 migration — https://zod.dev/v4/changelog ; release notes — https://zod.dev/v4
- Orval v8 — https://orval.dev/docs/versions/v8/ ; React Query guide — https://orval.dev/docs/guides/react-query/
- Vitest 4 — https://vitest.dev/blog/vitest-4 ; visual regression — https://main.vitest.dev/guide/browser/visual-regression-testing ; request mocking — https://vitest.dev/guide/mocking/requests
- MSW avoid request assertions — https://mswjs.io/docs/best-practices/avoid-request-assertions/
- Testing Library query priority — https://testing-library.com/docs/queries/about/
- Playwright best practices — https://playwright.dev/docs/best-practices ; auth — https://playwright.dev/docs/auth
- Playwright MCP — https://github.com/microsoft/playwright-mcp ; Playwright CLI — https://playwright.dev/agent-cli/introduction , https://github.com/microsoft/playwright-cli
- Chrome DevTools MCP — https://github.com/ChromeDevTools/chrome-devtools-mcp
- WCAG 2.2 — https://www.w3.org/TR/WCAG22/ ; new criteria — https://vispero.com/resources/new-success-criteria-in-wcag22/
- Flutter release notes — https://docs.flutter.dev/release/release-notes ; Flutter 3.47 — https://flutter.dev/blog/whats-new-in-flutter-3-47 ; 3.44/Dart 3.12 — https://www.fluttersolution.com/2026/05/flutter-344-dart-312-everything-new-at.html
- Riverpod 3 — https://riverpod.dev/docs/whats_new ; riverpod_generator — https://pub.dev/packages/riverpod_generator/changelog
- freezed changelog — https://pub.dev/packages/freezed/changelog
- very_good_analysis — https://pub.dev/packages/very_good_analysis
- go_router changelog — https://pub.dev/packages/go_router/changelog ; VGV routing — https://verygood.ventures/blog/routing-best-practices-in-flutter/
- flutter_secure_storage — https://pub.dev/packages/flutter_secure_storage/changelog
- Flutter i18n — https://docs.flutter.dev/ui/internationalization
- Flutter a11y testing — https://docs.flutter.dev/ui/accessibility/accessibility-testing ; web a11y — https://docs.flutter.dev/ui/accessibility/web-accessibility
- Flutter web agent testing — https://www.devassure.io/blog/flutter-web-app-agent-testing/
- alchemist — https://github.com/Betterment/alchemist ; Patrol — https://leancode.co/blog/everything-you-need-to-know-about-patrol
- openapi-generator dart-dio — https://github.com/OpenAPITools/openapi-generator/blob/master/docs/generators/dart-dio.md ; kotlin — https://github.com/openapitools/openapi-generator/blob/master/docs/generators/kotlin.md
- Kotlin 2.4.0 — https://blog.jetbrains.com/kotlin/2026/06/kotlin-2-4-0-released/
- New KMP default structure — https://blog.jetbrains.com/kotlin/2026/05/new-kmp-default-structure/ ; AGP 9 migration — https://kotlinlang.org/docs/multiplatform/multiplatform-project-agp-9-migration.html
- CMP releases — https://github.com/JetBrains/compose-multiplatform/releases ; CMP 1.10 — https://blog.jetbrains.com/kotlin/2026/01/compose-multiplatform-1-10-0/
- Navigation 3 in CMP — https://kotlinlang.org/docs/multiplatform/compose-navigation-3.html
- CMP UI testing — https://kotlinlang.org/docs/multiplatform/compose-test.html
- CMP localization — https://kotlinlang.org/docs/multiplatform/compose-localize-strings.html ; RTL — https://www.jetbrains.com/help/kotlin-multiplatform-dev/compose-rtl.html
- Compose a11y defaults — https://developer.android.com/develop/ui/compose/accessibility/api-defaults ; semantics — https://developer.android.com/develop/ui/compose/accessibility/semantics
- Ktor bearer auth — https://ktor.io/docs/client-bearer-auth.html ; Ktor 3.6 — https://blog.jetbrains.com/ktor/2026/09/18/ktor-3-6-0-is-now-available/ ; Darwin CertificatePinner — https://api.ktor.io/ktor-client-darwin/io.ktor.client.engine.darwin.certificates/-certificate-pinner/index.html
- Koin CMP — https://insert-koin.io/docs/quickstart/compose-multiplatform-annotations/ ; Koin 4.1 — https://blog.kotzilla.io/koin-4.1-is-here
- Mokkery — https://github.com/lupuuss/Mokkery ; KSafe — https://github.com/ioannisa/KSafe
- compose-rules (ktlint/detekt) — https://github.com/mrmans0n/compose-rules ; ComposablePreviewScanner — https://github.com/sergio-sastre/ComposablePreviewScanner
- Antigravity headless — https://antigravity.google/docs/cli/headless/ ; using CLI — https://antigravity.google/docs/cli/using/ ; browser (IDE) — https://antigravity.google/docs/ide/browser/ ; screenshots — https://antigravity.google/docs/screenshots/
- Agentic UI automation codelab (agy + BrowserMCP/Playwright; browser agent not in CLI) — https://codelabs.developers.google.com/agentic-ui-automation-with-antigravity
- agy cheat sheets — https://computingforgeeks.com/antigravity-cli-cheat-sheet/ , https://toolsbase.dev/en/reference/antigravity-cli-commands ; CLI changelog — https://github.com/google-antigravity/antigravity-cli/blob/main/CHANGELOG.md ; browser subagent Linux bug — https://github.com/google-antigravity/antigravity-cli/issues/629
- 15 agy tips — https://medium.com/google-cloud/15-antigravity-cli-tips-ddbc21c10a20 ; hands-on — https://dev.to/arindam_1729/antigravity-cli-a-hands-on-guide-to-googles-terminal-coding-agent-5bc7
- AI browser-agent E2E practices — https://tangle.tools/blog/ai-e2e-testing-browser-agents/
- Maestro Flutter — https://docs.maestro.dev/get-started/supported-platform/flutter ; Maestro MCP — https://verygood.ventures/blog/maestro-mcp-claude-mobile-ui-test-automation/
