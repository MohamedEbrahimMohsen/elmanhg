---
name: node-feature
description: Style guide for every change in this repo's Node.js services (APIs, workers, AI-tool glue). The planner, style-checker, reviewer and implementer judge services/ code against this file and nothing else. Team default; tune the sections, keep the numbering.
---

# Node feature handbook

Stack: **Node 22 LTS · TypeScript strict · Fastify (or Express) · Zod · Prisma or Drizzle · pino · Vitest · pnpm/npm**
  → 2026-09 update: Node **24** LTS (Node 22 is maintenance, EOL Apr 2027) · TypeScript **6.0** (not 7.x: typescript-eslint cannot use TS 7 until 7.1) · Fastify 5 (Express 5 only where Express already exists; NestJS 11/12 only where Nest already exists) · Zod 4 (≥4.2) · Prisma 7 or Drizzle (one per repo) · Vitest 4 · ESLint 10 flat config · pnpm 11.
Typical use: the API and orchestration layer of an AI product; calls Python models or LLM providers, serves a React POC.
Shared contract rules (errors, pagination, idempotency, versioning): global skill `momenta-api-contract`. New packages: global skill `momenta-dependency-policy`.

## 1. Layout

- `src/modules/<module>/` with `routes.ts`, `service.ts`, `repo.ts`, `schemas.ts`, `types.ts`. One module per resource.
- Cross-cutting in `src/lib/` (logger, config, errors, http client, llm client). Nothing business-specific there.
- Entry `src/server.ts` registers modules. Workers in `src/workers/<name>.ts`, one job type per file.
- No file over 200 lines. A service function over 60 lines gets split.
- `src/app.ts` exports `buildApp(deps): FastifyInstance` with no `listen()`; `src/server.ts` only builds, listens, and handles signals. Tests use `buildApp` with fakes.
- `src/instrumentation.ts` starts OpenTelemetry and is loaded with `node --import` before anything else.
- Unit tests co-located (`service.test.ts`); integration tests in `test/integration/*.int.test.ts`; DB migrations in `db/migrations/`.
- `package.json`: `"type": "module"`, `"engines": { "node": ">=24" }`, `"packageManager": "pnpm@11.x.y"` (exact version).
- Generated code (Prisma client, OpenAPI clients) in `src/generated/`, excluded from lint and coverage, never hand-edited.

## 2. Naming

- Files `kebab-case.ts`. Types and classes `PascalCase`. Functions and variables `camelCase`. Constants `SCREAMING_SNAKE`.
- Route handlers are verbs (`createOrder`, `listOrders`). Schemas end in `Schema` (`createOrderSchema`), inferred types match without the suffix.
- Error codes: constants in `src/lib/errors.ts`, `SCREAMING_SNAKE`, thrown as `AppError(code, status, details?)`. Same codes as the API contract exposes.
- Env vars `UPPER_SNAKE`, read once in `src/lib/config.ts` through a Zod schema.
- Relative imports carry the `.js` extension (`import { x } from './service.js'`) under `moduleResolution: nodenext`.
- URL paths plural kebab-case under a version prefix (`/v1/purchase-orders/:id`); JSON fields camelCase.
- Test titles state behaviour: `it('returns 409 when stock is insufficient')`.

## 3. Forbidden

- `any`. `as unknown as`. `@ts-ignore`. Non-null `!` on values that can be null.
- `process.env.X` outside `config.ts`. Secrets in code or in committed `.env` files.
- `console.log`. Use the pino logger with a child per module.
- `try { … } catch (e) {}` and swallowed rejections. Unhandled promises. `.then()` chains where `await` reads better.
- Business logic in a route handler. Database access in a route handler or service (repo only).
- Calling an LLM or model provider directly from a module. Goes through `src/lib/llm.ts` (timeouts, retries, cost log).
- `require()`. Default exports (except config files). Circular imports.
- Blocking the event loop: sync fs, `JSON.parse` on multi-MB bodies, CPU loops without a worker.
- Comments in production code beyond one line for a non-obvious invariant. `// TODO`. Commented-out code.
- `await` inside `forEach` (it does not await). `Promise.all` over unbounded user-controlled arrays (use `p-limit` with a fixed concurrency).
- `dotenv` (use `node --env-file=.env`), `ts-node` (use `tsx` or `tsc`), `.eslintrc*` / `.eslintignore`, `moduleResolution: "node"`, `baseUrl` path hacks.
- `express-async-errors`, `body-parser` (Express 5 has both built in).
- `new PrismaClient()` per request; DB pools not closed in tests.
- String-built SQL. Use parameterised queries / Prisma / Drizzle / Kysely tagged templates.
- `jwt.decode()` for authentication; `jsonwebtoken.verify` without `algorithms`.
- `npm start` as the container entrypoint; container running as root.

## 4. Required

- Every route: Zod schema for params, query, body, and response; validation at the boundary; typed handler.
- Every error path returns `{ code, message }` with the error code constant and the documented status. Middleware maps `AppError`; anything else is 500 and logged with the request id.
  → 2026-09 update: the body is RFC 9457 `application/problem+json`: `type, title, status, detail, instance, code, traceId` (+ `errors` for validation). `code` = the constant. Services that already return `{ code, message }` add the problem fields and keep `message` until the next major API version (see `momenta-api-contract`).
- Every external call (HTTP, LLM, DB) has a timeout. LLM calls log model, tokens in/out, latency, cost.
- Idempotent workers: a job re-run never double-writes. Retries with backoff, dead-letter after N.
- `async` everywhere it does IO; `await` every promise or return it.
- Every new endpoint → Postman request(s) in the same change.
- ESLint + Prettier clean. `tsc --noEmit` clean. Both run in `build`.
- `fetch`/undici calls pass `signal: AbortSignal.timeout(ms)`.
- Tenant/user ids come from the verified token (`req.user`), never from `req.body`/`req.query`.
- Response schemas declared per status so the serializer strips unknown fields (no column leaks).
- `SIGTERM` handler: stop accepting, `await app.close()`, close DB pool, `await otelSdk.shutdown()`, exit.

## 5. Tests

- Vitest. `*.test.ts` next to the file it tests. Route tests through the app instance (`app.inject`), not a live port.
- Services tested with the repo mocked at the boundary; repos tested against a test database or an in-memory driver, never mocked against themselves.
  → 2026-09 update: repos and route integration tests run against real PostgreSQL via `@testcontainers/postgresql`; no in-memory DB substitute (SQLite/pg-mem) for a Postgres app.
- Every error code path has a test asserting status and code. Every Zod schema has a passing and a failing case per rule.
- LLM calls are recorded fixtures under `tests/fixtures/llm/`; no live model in tests.
- No test asserts a mock returned what it was told to return.
- Every new endpoint: happy path + one failure (400/404/409) + one authz case (other tenant → 404/403). Full rules: `conventions/node-testing.md`.
- Never edit, skip (`it.skip`, `xit`, `.only`), or delete an existing test to make it pass. If a test is wrong, stop and write `BLOCKED: <test> — <why>` in the report.

## 6. DO / DON'T catalog (every DON'T is a blocking review finding)

| # | DON'T | DO |
|---|-------|----|
| 6.1 | `process.env.OPENAI_KEY` in a module | `config.openaiKey` from `config.ts` |
| 6.2 | `fetch('https://api.openai.com…')` in a service | `llm.complete({ model, prompt })` from `lib/llm.ts` |
| 6.3 | `res.status(400).send('bad')` | `throw new AppError(ErrorCodes.INVALID_INPUT, 400)` |
| 6.4 | `req.body.amount` untyped | `createOrderSchema.parse(req.body)` at the boundary |
| 6.5 | `console.log(...)` | `log.info({ orderId }, 'created')` |
| 6.6 | `catch (e) {}` | catch, log with request id, rethrow or map to `AppError` |
| 6.7 | DB query inside a route | route → service → repo |
| 6.8 | New endpoint, no Postman request | request(s) added in the same change |
| 6.9 | A worker that writes without checking it already ran | idempotency key checked first |
| 6.10 | `any` to make it compile | the real type, or `unknown` narrowed |
| 6.11 | `items.forEach(async i => await save(i))` | `for (const i of items) await save(i)` or `pLimit(5)` + `Promise.all` |
| 6.12 | `fetch(url)` with no timeout | `fetch(url, { signal: AbortSignal.timeout(5_000) })` |
| 6.13 | `where: { tenantId: req.body.tenantId }` | `where: { tenantId: req.user.tenantId }` |
| 6.14 | `` db.query(`SELECT … WHERE id = ${id}`) `` | `db.query('SELECT … WHERE id = $1', [id])` / ORM |
| 6.15 | `jwt.decode(token)` to read the user | `jwtVerify(token, jwks, { issuer, audience, algorithms })` |
| 6.16 | `fs.readFileSync` in a request path | `await fs.promises.readFile` |

## 7. New project from scratch

Use only when `01-context.md` says the service does not exist yet. Walking-skeleton rules: global skill `momenta-greenfield-bootstrap`.
```bash
mkdir svc && cd svc && corepack enable && pnpm init
pnpm add fastify @fastify/type-provider-zod zod @fastify/swagger @fastify/swagger-ui @fastify/helmet @fastify/cors @fastify/rate-limit pino jose
pnpm add -D typescript@6 @types/node@24 tsx vitest @vitest/coverage-v8 eslint@10 @eslint/js typescript-eslint prettier testcontainers @testcontainers/postgresql
npx tsc --init
```
- Every name above is verified per `momenta-dependency-policy` before `pnpm add`; exact versions are written by pnpm into `pnpm-lock.yaml`.
- `tsconfig.json` (TS 6):
```json
{ "compilerOptions": { "target": "es2024", "module": "nodenext", "moduleResolution": "nodenext",
  "strict": true, "noUncheckedIndexedAccess": true, "exactOptionalPropertyTypes": true,
  "verbatimModuleSyntax": true, "isolatedModules": true, "types": ["node"],
  "outDir": "dist", "rootDir": "src", "sourceMap": true, "skipLibCheck": true } }
```
  TS 6 defaults `types` to `[]`: list `"node"` explicitly.
- Scripts: `"dev": "tsx watch --env-file=.env src/server.ts"`, `"build": "tsc -p tsconfig.json"`, `"typecheck": "tsc --noEmit"`, `"lint": "eslint ."`, `"format:check": "prettier --check ."`, `"test": "vitest run"`.
- Config (`src/lib/config.ts`):
```ts
const Env = z.object({ NODE_ENV: z.enum(['development', 'test', 'production']), PORT: z.coerce.number().default(3000),
  DATABASE_URL: z.url(), JWT_ISSUER: z.url(), LOG_LEVEL: z.enum(['debug', 'info', 'warn', 'error']).default('info') });
export const config = Object.freeze(Env.parse(process.env));
```
- Health: `GET /health/live` (process up, no deps) and `GET /health/ready` (DB `SELECT 1` with a 1 s timeout); both unauthenticated, excluded from rate limit and OpenAPI.
- Dockerfile:
```dockerfile
FROM node:24-slim AS deps
WORKDIR /app
RUN corepack enable
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
FROM deps AS build
COPY . .
RUN pnpm build && pnpm prune --prod
FROM node:24-slim
ENV NODE_ENV=production
WORKDIR /app
COPY --from=build --chown=node:node /app/node_modules ./node_modules
COPY --from=build --chown=node:node /app/dist ./dist
USER node
CMD ["node", "--enable-source-maps", "--import", "./dist/instrumentation.js", "dist/server.js"]
```
- `docker-compose.yml`: `postgres:17`, `redis:7` (if caching/queues), an OTLP collector or Aspire dashboard; every service has a `healthcheck`; app `depends_on: { db: { condition: service_healthy } }`; app service `init: true`; backing services get no fixed host port.
- `.env.example` lists every key from the `Env` schema with dummy values; `.env` gitignored.
- Migrations run as a separate release step (`prisma migrate deploy` / `drizzle-kit migrate`), never on app boot.

## 8. HTTP contract

Full shapes in `momenta-api-contract`. Fastify wiring:
```ts
app.setValidatorCompiler(validatorCompiler);
app.setSerializerCompiler(serializerCompiler);
app.setErrorHandler((err, req, reply) => {
  const p = toProblem(err, req.id);            // AppError | ZodError/validation | unknown → RFC 9457
  if (p.status >= 500) req.log.error({ err }, 'unhandled');
  void reply.status(p.status).type('application/problem+json').send(p);
});
```
- Validation errors → 400, `code: "VALIDATION_FAILED"`, `errors: [{ path, code, message }]`. Unknown errors → 500 with a generic `title`; stack only in logs.
- 404 for routes and for other tenants' resources; `setNotFoundHandler` also returns problem+json.
- OpenAPI: `@fastify/swagger` with `transform: jsonSchemaTransform` from the Zod type provider; UI (`@fastify/swagger-ui`) only when `NODE_ENV !== 'production'`. Export spec to `openapi/v1.json` in CI and diff it (`oasdiff breaking`).
- Versioning: `app.register(v1Routes, { prefix: '/v1' })`.
- Pagination: `?limit=` (max 100, enforced in the Zod schema) + `?cursor=` (opaque base64url); response `{ items, nextCursor }`.
- Idempotency: `Idempotency-Key` header on POSTs creating orders/payments; table with a unique constraint on `(user_id, key)`; insert-first, then execute; replay stored response.
- Rate limit: `@fastify/rate-limit`, Redis store when more than one instance, key by user id then IP; 429 + `Retry-After`.
- CORS: `@fastify/cors` with the origin list from config; `@fastify/helmet` registered.
- `trustProxy` set to the real proxy hop count/CIDR behind a load balancer.
- Express 5 only: async handler rejections reach the error middleware automatically; error middleware (4 args) registered last; path syntax `/*splat`, `{/:optional}`.
- NestJS only: global `ValidationPipe({ whitelist: true, forbidNonWhitelisted: true, transform: true })` (Nest 11) or Standard Schema route validation (Nest 12); `@nestjs/swagger`.

## 9. Data

- Prisma 7: generator `provider = "prisma-client"` with `output`, driver adapter mandatory, `prisma.config.ts`; env not auto-loaded.
```ts
import { PrismaClient } from '../generated/prisma/client.js';
import { PrismaPg } from '@prisma/adapter-pg';
export const prisma = new PrismaClient({ adapter: new PrismaPg({ connectionString: config.DATABASE_URL }) });
```
- Drizzle: schema in `src/db/schema.ts`, migrations by `drizzle-kit generate`, reviewed SQL committed.
- Migrations: review the generated SQL for `DROP`/`RENAME`/`NOT NULL` without default; expand/contract across releases (add → dual-write/backfill → switch reads → drop later).
- Transactions: `prisma.$transaction(async (tx) => …)` / `db.transaction(async (tx) => …)`; pass `tx` explicitly to repos; no outbound HTTP/LLM call inside a transaction.
- Optimistic concurrency: `version` int column; `UPDATE … WHERE id = $1 AND version = $2`; 0 rows → 409 `CONCURRENCY_CONFLICT` (Prisma: `updateMany` + check `count`).
- N+1: no repo call inside a loop over rows; use `include`/`with`, an `IN (...)` batch, or DataLoader.
- Time: columns `timestamptz`; wire format `toISOString()` (UTC `Z`); services get a `clock: () => Date` dependency.
- Money: integer minor units or `numeric` mapped to string; never JS `number` arithmetic on decimals.
- IDs: UUIDv7 or bigint internal + opaque public id; never expose sequential ids of guessable resources.

## 10. Cross-cutting

- AuthN: JWT via JWKS (`jose`: `createRemoteJWKSet` + `jwtVerify(token, jwks, { issuer, audience, algorithms: ['RS256'] })`) or `@fastify/jwt` with `algorithms` set. Browser sessions: cookie `httpOnly; Secure; SameSite=Lax` + CSRF token on unsafe methods.
- AuthZ: per-route `preHandler: [requireScope('orders:write')]`; resource-level checks in the service by `tenantId` from the token.
- Logging: pino JSON; `pino-pretty` only as a dev transport; `redact: ['req.headers.authorization', 'req.headers.cookie', '*.password', '*.token']`; `genReqId` honours `x-request-id`; log objects first: `log.info({ orderId }, 'order placed')`.
- OpenTelemetry: `@opentelemetry/sdk-node` + `@opentelemetry/auto-instrumentations-node` + OTLP exporter in `instrumentation.ts`, started with `node --import ./dist/instrumentation.js`; ESM hook registered in that file; configured by `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_ENDPOINT`.
- Config/secrets: only `config.ts` reads `process.env`; secrets from the platform (Key Vault / Secrets Manager → env).
- Resilience: timeout on every call; retries (exponential + jitter) only for idempotent requests (`p-retry`, or `cockatiel` for retry + circuit breaker).
- Caching: Redis cache-aside, TTL with jitter, versioned keys (`orders:v2:{id}`), per-user data keyed by user/tenant; single-flight for hot keys.
- Background jobs: BullMQ (Redis) or pg-boss (Postgres) in a separate worker entrypoint; `setInterval` in the API process is not a job system.
- Unhandled rejection → log and exit non-zero (the orchestrator restarts); never swallow `unhandledRejection`.

## 11. Tooling & CI commands

Run in this order; each must exit 0. Paste commands + output tails + exit codes in the implementation report.
```bash
corepack enable && pnpm install --frozen-lockfile
pnpm lint                      # eslint . (ESLint 10 flat config)
pnpm format:check              # prettier --check .   (or: biome ci .)
pnpm typecheck                 # tsc --noEmit
pnpm test -- --coverage        # vitest run --coverage (v8)
pnpm audit --prod
```
- `eslint.config.ts`:
```ts
import js from '@eslint/js'; import tseslint from 'typescript-eslint'; import { defineConfig } from 'eslint/config';
export default defineConfig(
  { ignores: ['dist', 'src/generated'] },
  js.configs.recommended,
  tseslint.configs.strictTypeChecked,
  { languageOptions: { parserOptions: { projectService: true } },
    rules: { '@typescript-eslint/no-floating-promises': 'error', '@typescript-eslint/no-misused-promises': 'error' } },
);
```
- `pnpm-workspace.yaml` / `.npmrc`: keep `minimumReleaseAge` (pnpm 11 default 1 day; team value 7 days = `10080`); postinstall scripts only for packages on the `allowBuilds` allow-list.
- npm repos: `npm ci`, `ignore-scripts=true` in `.npmrc`.

## 12. Dependencies & licences

Policy: `momenta-dependency-policy`. A package is added only if the plan names it with an exact version.
- Verify before adding: `pnpm view <pkg> name version time.created license repository.url --json` and weekly downloads `curl -s https://api.npmjs.org/downloads/point/last-week/<pkg>`.
- Scoped successors, not old names: `@fastify/type-provider-zod` (not `fastify-type-provider-zod`), `@fastify/*` plugins (not `fastify-*`).
- Do not add: `dotenv`, `ts-node`, `express-async-errors`, `body-parser`, `request`, `moment` (use `Intl`/`date-fns`/`Temporal` where available), `node-serialize`, `jsonwebtoken` in new code (use `jose`).
- Licence allow-list: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC. Check with `pnpm licenses list --prod`.

## 13. Security gotchas (Node)

- Prototype pollution: deep-merge of user JSON, `obj[userKey] = …`; use `Map`/`Object.create(null)`, reject `__proto__`, `constructor`, `prototype` keys.
- `child_process.exec` with template strings → `execFile`/`spawn` with an args array, never `shell: true`.
- Path traversal: `path.join(base, userInput)` → `path.resolve` + `startsWith(base + path.sep)`.
- SSRF: `fetch(userUrl)` follows redirects by default → allow-list scheme+host, block private/link-local/metadata IPs, `redirect: 'manual'`.
- `eval`, `new Function`, `vm` (not a sandbox), `node-serialize`.
- Body size limit set (`bodyLimit` in Fastify, `express.json({ limit })`); no ReDoS-prone regex on input.
- CORS origin reflection with `credentials: true`.
- Mongo operator injection (`{ "$gt": "" }` from JSON body) → Zod-typed inputs.
- Secrets in `.env` committed, in logs, or in error bodies → blocking.
- LLM output is untrusted: validate with Zod before using it in SQL, shell, HTML, URLs, file paths.

## 14. LLM mistakes to avoid (Node)

- CommonJS `require`/`module.exports` in an ESM project; missing `.js` in relative imports.
- Express 4 idioms in Express 5: `app.get('*')`, `:param?`, `req.param()`, `res.send(status, body)`.
- Zod 3 APIs in Zod 4: `errorMap`, `ZodError.errors` (now `.issues`), `z.string().email()` (use `z.email()`).
- Prisma 6 patterns in Prisma 7: `provider = "prisma-client-js"`, import from `@prisma/client` without generated output, `$use` middleware, relying on auto `.env` loading.
- Jest APIs (`jest.fn`, `jest.mock`) in a Vitest project; supertest against `app.listen()` leaving open handles.
- Missing `return await` inside `try` blocks (rejection escapes the catch).
- Hallucinated package names: ~5% of LLM-suggested npm names do not exist and some are squatted. Verify every new name (§12).
- `typescript@7` added as the project compiler.
- Editing, skipping or deleting a failing test instead of fixing the code.

## 15. Definition of done

- [ ] `pnpm install --frozen-lockfile`, `pnpm lint`, `pnpm format:check`, `pnpm typecheck`, `pnpm test -- --coverage` all exit 0; commands + output tails + exit codes pasted in `03-implementation-node.md`.
- [ ] Every new route: Zod schemas for params/query/body/response, auth `preHandler`, problem+json errors, Postman request, tests (happy + failure + authz).
- [ ] Every new error code in `src/lib/errors.ts` and in the OpenAPI error catalogue.
- [ ] Every outbound call has a timeout; every LLM call goes through `lib/llm.ts`.
- [ ] Schema change → reviewed migration in the same change.
- [ ] No new package unless named in the plan; `pnpm-lock.yaml` updated; verification output in the report.
- [ ] Generated OpenAPI committed; no breaking diff unless the plan declares a new version.
- [ ] No existing test modified/deleted/skipped unless the plan's `### Tests` table lists it.
- [ ] `git diff origin/main -U0 -- '*.ts' | grep -E '^\+.*(console\.log|: any\b|@ts-ignore|process\.env|\.skip\(|\.only\()'` prints nothing outside `config.ts`.
- [ ] Anything not done → report ends with `BLOCKED: <reason>`.
