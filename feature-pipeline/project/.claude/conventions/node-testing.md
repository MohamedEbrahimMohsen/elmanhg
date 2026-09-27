# Testing convention — node

<!-- Framework, where tests live, naming pattern (file + method), what every test must assert, what a vacuous test looks like here. The planner writes the Test plan from this; the reviewer scores tests against it. -->

## Framework

| Concern | Tool (2026-09) | Notes |
|---|---|---|
| Runner + assertions + mocks | Vitest 4 (`vitest`, `expect`, `vi`) | Jest only in legacy CJS/NestJS-11 repos that already use it; never both |
| HTTP (Fastify) | `app.inject(...)` on `buildApp(deps)` | no socket, no `listen()` |
| HTTP (Express/Nest) | `supertest(app)` | pass the app, not a listening server |
| Database | `testcontainers` + `@testcontainers/postgresql` | real Postgres; no pg-mem/SQLite |
| Outbound HTTP | `msw` (`setupServer`) or undici `MockAgent` | no real network |
| Time | `vi.useFakeTimers()` / `vi.setSystemTime()` or injected `clock` | |
| Data | builder functions; `@faker-js/faker` seeded | |
| Property tests | `fast-check` | parsers, money math |
| Coverage | `@vitest/coverage-v8` | |

Node 24, TypeScript 6, ESM. Test files are TypeScript and type-checked by `pnpm typecheck`.

## Location and naming

- Unit: `src/**/<file>.test.ts` next to the file under test.
- Integration: `test/integration/<module>/<behaviour>.int.test.ts`; global setup in `test/integration/global-setup.ts`; helpers in `test/helpers/` (`build-test-app.ts`, `builders.ts`, `db.ts`).
- LLM fixtures: `tests/fixtures/llm/<pipeline>/<case>.json`.
- Two Vitest projects in `vitest.config.ts`: `unit` (`src/**/*.test.ts`) and `integration` (`test/integration/**/*.int.test.ts`, `globalSetup`, `pool: 'forks'`).
- `describe('<unit under test>')` → `it('<does what> when <condition>')`: `it('returns 409 CONCURRENCY_CONFLICT when version is stale')`.
- One behaviour per `it`. Arrange / Act / Assert separated by one blank line.
- `it.each` only when cases differ by data alone; give each row a readable name (`'$input → $expected'`).

## Every test must

- Assert an observable outcome: return value, thrown `AppError` `code`, HTTP `statusCode` **and** problem+json `code`, persisted rows, or a boundary call that *is* the behaviour.
- Error-path route tests assert `res.headers['content-type']` starts with `application/problem+json`.
- Every Zod schema: one passing case + one failing case per rule, asserting the issue path.
- Every error code path: status + code.
- `await` every async assertion (`await expect(p).rejects.toMatchObject({ code: 'ORDER_NOT_FOUND' })`).
- Be deterministic: fake timers or injected clock, `faker.seed(42)`, no `setTimeout` waits (use `vi.waitFor`).
- Be independent: unique ids per test; no reliance on execution order; DB cleaned per file.
- Close what it opens: `await app.close()`, DB pool `end()` in `afterAll`.

## Never

- Edit, weaken, skip (`it.skip`, `describe.skip`, `xit`, `it.todo` replacing a real test), focus (`.only`), or delete an existing test to make it pass. The orchestrator's test-integrity guard blocks it. If a test is wrong: stop and write `BLOCKED: <test> — <why>` in the report.
- Assert that a mock returns what it was told to return.
- Hit a real network, a live LLM, or a shared dev database.
- Use `retry` in Vitest config or per test to hide flakiness.
- Mock the module under test, or `vi.mock` internal modules to reach a line (mock only at process boundaries).
- Snapshot entire responses containing timestamps/ids without property matchers.
- Leave fake timers or `vi.stubEnv` active after the test (`restoreMocks: true`, `unstubEnvs: true`, `vi.useRealTimers()` in `afterEach`).

## Unit vs integration boundary

| Tier | Covers | Doubles allowed |
|---|---|---|
| Unit | services, domain functions, schemas, mappers, `toProblem` | fake repos/clients passed through factories; fake clock |
| Integration | route → service → repo → real Postgres; auth `preHandler`s; error handler; migrations | `msw`/`MockAgent` for third-party HTTP; recorded LLM fixtures |
| E2E | deployed app | none |

- Every new endpoint gets ≥ 3 integration tests: happy path, one failure (400/404/409), one authz (other tenant → 404/403, no token → 401).

## Fakes vs mocks

- `buildApp({ db, clock, llm, payments })` takes dependencies; tests pass in-memory fakes (`createFakePayments()` recording calls).
- `vi.fn()` for callbacks; `vi.mock` only for SDKs that cannot be injected (email/payment SDK modules).
- Config: `restoreMocks: true`, `clearMocks: true`, `unstubEnvs: true`.

## Integration setup skeleton

```ts
// test/integration/global-setup.ts
import { PostgreSqlContainer } from '@testcontainers/postgresql';
import type { TestProject } from 'vitest/node';

export default async function setup(project: TestProject) {
  const pg = await new PostgreSqlContainer('postgres:17-alpine').start();
  project.provide('databaseUrl', pg.getConnectionUri());
  await runMigrations(pg.getConnectionUri());          // same command as the release step
  return async () => { await pg.stop(); };
}
```
- Cleanup between files: `TRUNCATE <tables> RESTART IDENTITY CASCADE` in `beforeEach`/`beforeAll`, or each test in a transaction rolled back.
- Verify the `provide`/`inject` API against the installed Vitest (`pnpm vitest --help`, Vitest docs for `globalSetup`).

## Test data builders

```ts
export const anOrder = (over: Partial<NewOrder> = {}): NewOrder => ({
  id: randomUUID(), tenantId: 'tenant-a', customerId: randomUUID(),
  lines: [{ sku: 'SKU-1', qty: 1, unitPriceMinor: 1000 }], currency: 'EGP', ...over,
});
```
- One builder per aggregate in `test/helpers/builders.ts`; overrides via partials; persisted helpers `await insertOrder(db, anOrder())`.

## Example route test

```ts
describe('GET /v1/orders/:id', () => {
  let app: FastifyInstance;
  beforeAll(async () => { app = await buildTestApp({ databaseUrl: inject('databaseUrl') }); });
  afterAll(async () => { await app.close(); });

  it('returns 404 ORDER_NOT_FOUND for another tenant\'s order', async () => {
    const order = await insertOrder(app.db, anOrder({ tenantId: 'tenant-a' }));

    const res = await app.inject({ method: 'GET', url: `/v1/orders/${order.id}`, headers: authHeader({ tenantId: 'tenant-b' }) });

    expect(res.statusCode).toBe(404);
    expect(res.headers['content-type']).toMatch(/^application\/problem\+json/);
    expect(res.json()).toMatchObject({ status: 404, code: 'ORDER_NOT_FOUND' });
  });
});
```

## Example unit test

```ts
describe('cancelOrder', () => {
  it('throws ORDER_ALREADY_SHIPPED when the order has shipped', async () => {
    const repo = createFakeOrderRepo([anOrderRow({ status: 'shipped' })]);
    const service = createOrderService({ repo, clock: () => new Date('2026-01-01T00:00:00Z') });

    const act = service.cancelOrder({ orderId: repo.rows[0]!.id, tenantId: 'tenant-a' });

    await expect(act).rejects.toMatchObject({ code: 'ORDER_ALREADY_SHIPPED', status: 409 });
    expect(repo.rows[0]!.status).toBe('shipped');
  });
});
```

## Coverage and commands

```bash
pnpm vitest run --project unit
pnpm vitest run --project integration
pnpm vitest run --coverage              # v8; thresholds in vitest.config.ts
```
- Thresholds in config: lines 80, branches 70 on `src/**`, excluding `src/generated/**`, `src/server.ts`, `src/instrumentation.ts`.
- Pipeline gate is changed-line coverage (≥ 80%).

## Flakiness

- A flaky test is a bug: quarantine within 24 h in `test/quarantine/` (separate non-blocking Vitest project) with an issue link on the first line; fix within one sprint.
- Nightly run with `--sequence.shuffle` to surface order dependence.
- Docker unavailable → the implementer reports `BLOCKED: docker unavailable`, never skips integration tests.
