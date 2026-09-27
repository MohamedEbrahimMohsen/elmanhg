# Testing convention — react

<!-- Framework, where tests live, naming pattern (file + method), what every test must assert, what a vacuous test looks like here. The planner writes the Test plan from this; the reviewer scores tests against it. -->

## Framework

| Level | Tool (version) | Env |
|---|---|---|
| Unit | Vitest 4 | `node` |
| Component / integration (the bulk) | Vitest 4 + @testing-library/react + @testing-library/user-event + @testing-library/jest-dom + MSW 2 | `jsdom` |
| Real layout / visual | Vitest 4 browser mode (`@vitest/browser-playwright`), `toMatchScreenshot` | Chromium |
| A11y | `vitest-axe` (component), `@axe-core/playwright` (E2E) | — |
| E2E (deterministic, critical journeys) | Playwright (`@playwright/test`) | Chromium |
| Coverage | `@vitest/coverage-v8` | — |
## Location and naming

- Unit/component tests beside the code: `InvoiceForm.tsx` → `InvoiceForm.test.tsx` (or `features/<f>/__tests__/`). Schemas: `invoiceSchema.test.ts`.
- Shared test utils: `src/test/` (`renderWithProviders.tsx`, `msw/server.ts`, `setup.ts`).
- Playwright specs: `e2e/<feature>.spec.ts`; auth setup `e2e/auth.setup.ts`; `playwright/.auth/` gitignored.
- `describe('<Component or hook>')`; `it('<does observable thing> when <condition>')` — e.g. `it('shows inline error when amount is empty')`.
## Every test must

- Assert a user-observable outcome (text, role, state, URL), not internals (state values, hook calls, request counts).
- Use queries in this priority: `getByRole` (with `name`) → `getByLabelText` → `getByPlaceholderText` → `getByText` → `getByDisplayValue` → `getByAltText` → `getByTitle` → `getByTestId` (last resort, with a comment why). `findBy*` for async; `queryBy*` only to assert absence.
- Create `const user = userEvent.setup()` before render and `await` every interaction.
- Render through `renderWithProviders` (fresh `QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } })`, memory router, i18n `lng: 'en'`).
- Stub HTTP only with MSW; server `listen({ onUnhandledRequest: 'error' })`, `resetHandlers()` after each test.
- Fail if the production line under test is removed (non-vacuous).
## Never

- `fireEvent` for user interactions; manual `act()` around user-event.
- `vi.mock('axios')`, mocked `fetch`, mocking TanStack Query or Orval hooks.
- `container.querySelector`, CSS-class selectors, snapshot of whole component trees (`toMatchSnapshot` on DOM).
- `setTimeout`/`sleep`/`waitForTimeout`; relying on test order; real network.
- `it.skip`, `test.skip`, `describe.skip`, `xit`, `.only`, `test.fixme` added to pass a pipeline.
- Editing or deleting an existing test the plan's `### Tests` table does not list as `modify`/`delete`.
- Vacuous tests: `expect(true).toBe(true)`, render with no assertion, asserting a mock returned what it was told to return.

## What to test at which level

| Code | Level | Must cover |
|---|---|---|
| Zod schema | Unit | valid case + one case per rule |
| Formatter / pure util | Unit | boundaries, `ar` + `en` locale |
| Feature hook (query/mutation wrapper) | Component (`renderHook` + MSW) | success, error mapping, invalidation effect visible in UI |
| Data screen / page | Component | loading → success, empty, error + retry, validation errors, one `ar` render (`dir="rtl"` on root) |
| Form | Component | required errors on submit, server `fieldErrors` shown inline, focus on first invalid field, submit disabled while submitting |
| Design-system component | Browser mode | visual at 390 and 1280 (`toMatchScreenshot`) |
| Critical journey (login, create, pay) | Playwright E2E | happy path + one negative path, axe scan per page |

## Skeletons

```tsx
// src/test/msw/server.ts
import { setupServer } from 'msw/node';
export const server = setupServer(...getInvoicesMock());   // Orval-generated handlers as defaults
// src/test/setup.ts
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

```tsx
// features/invoices/components/InvoiceList.test.tsx
import { http, HttpResponse } from 'msw';
describe('InvoiceList', () => {
  it('shows invoices after loading', async () => {
    renderWithProviders(<InvoiceList />);
    expect(screen.getByRole('status', { name: /loading/i })).toBeInTheDocument();
    expect(await screen.findByRole('row', { name: /E2E Acme/ })).toBeVisible();
  });
  it('shows retry on server error', async () => {
    server.use(http.get('*/api/invoices', () => HttpResponse.json({ title: 'Boom' }, { status: 500 })));
    const user = userEvent.setup();
    renderWithProviders(<InvoiceList />);
    const retry = await screen.findByRole('button', { name: /retry/i });
    server.resetHandlers();                                   // back to success handlers
    await user.click(retry);
    expect(await screen.findByRole('row', { name: /E2E Acme/ })).toBeVisible();
  });
  it('has no axe violations', async () => {
    const { container } = renderWithProviders(<InvoiceList />);
    await screen.findByRole('table');
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

```ts
// e2e/invoices.spec.ts
test('create invoice', async ({ page }) => {
  await page.goto('/invoices');
  await page.getByRole('button', { name: 'Create invoice' }).click();
  await page.getByLabel('Amount').fill('150.00');
  await page.getByRole('button', { name: 'Save' }).click();
  await expect(page.getByRole('status')).toHaveText('Invoice created');
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
});
```
Playwright config: `trace: 'on-first-retry'`, `retries: process.env.CI ? 2 : 0`, auth via setup project + `storageState`.

## Coverage

- Command: `npx vitest run --coverage` (v8 provider).
- Thresholds on `src/features/**`: lines 80, branches 70. Exclude `src/shared/api/generated/**`, `src/routeTree.gen.ts`, `**/*.d.ts`.
- New/changed lines in the diff: every branch has a test (reviewer checks `04-coverage-react.md`).

## Flakiness

- Freeze time (`vi.useFakeTimers({ shouldAdvanceTime: true })` + `vi.setSystemTime`), `TZ=UTC`, fixed locale.
- No test depends on another test's state or order.
- A test that flakes twice is quarantined within 24 h with `test.fixme` + linked ticket by a human — never by the implementer.

## Test integrity

- Never edit, skip, or delete a test to make it pass. The orchestrator diffs test files after every implement/rework/fix; unlisted changes are a blocking "test integrity" finding.
- Test looks wrong → stop and write `BLOCKED: <test> — <why>` in the report.
