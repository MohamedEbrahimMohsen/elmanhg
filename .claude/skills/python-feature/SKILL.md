---
name: python-feature
description: Style guide for every change in this repo's plain Python code (FastAPI services, LLM pipelines, workers, CLIs, AI tooling). Flask and Django apps have their own stacks. The planner, style-checker, reviewer and OpenCode judge ai/ code against this file and nothing else. Team default; tune the sections, keep the numbering.
---

# Python feature handbook

Stack: **Python 3.12 · FastAPI · Pydantic v2 · uv · ruff · mypy strict · pytest · structlog · LiteLLM for model calls**
  → 2026-09 update: Python **3.13** baseline (3.14 allowed) · FastAPI ~0.14x (Pydantic v1 support removed) · Pydantic 2.x · SQLAlchemy 2.0/2.1 + Alembic · pytest ≥8.4 + pytest-asyncio 1.x · mypy or pyright in CI (`ty` is beta: non-blocking pilot only). LiteLLM pinned to an exact version (`==`), bumped only by a plan.
Every prompt is a versioned file. Every model call is measured. Nothing ships without an eval.
Shared contract rules (errors, pagination, idempotency, versioning): global skill `momenta-api-contract`. New packages: global skill `momenta-dependency-policy`.

## Elmanhg deltas — read first

These override the sections below for this repo.

1. Stack pin: Python 3.13 (`ai/.python-version`), uv 0.12.17, package `elmanhg_ai` under `ai/src/`, settings prefix `ELMANHG_AI_`.
2. LLM: the Claude API through the `anthropic` SDK (pinned `==`) inside `clients/anthropic_model.py`, behind the `ModelClient` protocol in `clients/model.py`. There is **no LiteLLM**. `FakeModelClient` is the default (`ELMANHG_AI_LLM_PROVIDER=fake`). The anthropic 1.x `messages.create` has no `temperature`, so none is set.
3. HTTP library: `httpx2` (anthropic's transport). Never add `httpx`, `requests` or `respx`.
4. No database, SQLAlchemy or Alembic until a story needs one. Ignore §9 until then.
5. No OpenTelemetry SDK until E13.S2: the trace id comes from the `traceparent` header (`core/middleware.py`).
6. No Postman for `ai/`: the service is internal, and the .NET API's Postman collection covers user-facing endpoints.
7. Evals (`-m eval`) become mandatory from the first story that ships a production prompt against the live model (#91). The `eval` marker is declared now.
8. Contract: `ai/openapi/v1.json` is regenerated with `uv run python -m elmanhg_ai.openapi_export` and checked by `tests/integration/test_openapi_document.py`. The .NET side of the contract is `api/Elmanhg.Application/Shared/AiService/`, and both must change together (`docs/ai-service.md`).

## 1. Layout

- `ai/src/<pkg>/` with `api/` (routers, schemas), `pipelines/` (one module per pipeline), `prompts/` (`.md` or `.jinja`, versioned in the filename `summarise.v3.md`), `retrieval/`, `eval/` (datasets + scorers), `clients/` (model, vector store).
- One pipeline = one module with a single public `run(input: X) -> Y`. No file over 300 lines.
- Config through `pydantic-settings`; no `os.environ` reads outside `settings.py`.
- `main.py` exposes `create_app() -> FastAPI`; long-lived resources (DB engine, `httpx.AsyncClient`, Redis, model client) are created in `lifespan` and stored on `app.state`.
- `api/deps.py` holds `Annotated` dependency aliases (`SessionDep`, `CurrentUser`, `SettingsDep`).
- Non-AI features use `features/<name>/{router.py, schemas.py, service.py, repository.py, models.py}`; `service.py` imports nothing from `fastapi`.
- `migrations/` (Alembic), `tests/unit/`, `tests/integration/`, `tests/fixtures/`, `tests/conftest.py`.
- `pyproject.toml` + committed `uv.lock` + `.python-version`.

## 2. Naming

- `snake_case` modules and functions, `PascalCase` Pydantic models ending in `In` / `Out` for API boundaries and no suffix for domain types.
- Prompts `verb_object.vN.md`. Evals `test_eval_<pipeline>.py`. Scorers `score_<what>`.
- Model names, temperatures, and limits are settings, never literals.
- Error codes: `UPPER_SNAKE` constants in `core/errors.py`; domain exceptions `<What>Error(DomainError)`.
- Routes: plural kebab-case under `/v1` (`/v1/purchase-orders/{order_id}`); JSON fields camelCase via `alias_generator=to_camel` + `populate_by_name=True` on API models.
- Tests `test_<unit>_<scenario>_<expected>`.
- Settings env prefix = the package name upper-cased (`ACME_`).

## 3. Forbidden

- Calling a model without going through `clients/model.py` (timeouts, retries, cost logging live there).
- Prompt text as a Python string literal. Prompts are files.
- `print`. `except Exception: pass`. Bare `except`.
- `Any` in public signatures. Untyped `dict` crossing a module boundary.
- Hidden randomness: temperature or sampling not pinned in evals.
- Logging raw user content or full prompts at INFO. Hash or truncate.
- `requests` (use `httpx`). Global mutable state. Notebooks in `src/`.
- Blocking calls inside `async def`: `time.sleep`, sync DB drivers, `open()` on large files, CPU-heavy pandas. Use `def` endpoints, `anyio.to_thread.run_sync`, or async libraries.
- `datetime.utcnow()`, `datetime.now()` without `tz`, naive datetimes, `pytz` (use `zoneinfo`).
- Pydantic v1 API: `class Config`, `.dict()`, `.json()`, `parse_obj`, `@validator`, `orm_mode`, `Field(regex=)`.
- `@app.on_event("startup")`, `python-jose`, `passlib`, `fastapi-jwt-auth` in new code.
- `session.query()`, `declarative_base()` from `sqlalchemy.ext.declarative`, `engine.execute()`.
- Raising `HTTPException` from `service.py` / pipelines (framework-free core).
- f-string SQL; `text()` without bound params. `pickle`/`yaml.load`/`eval`/`exec` on untrusted input.
- Sharing one `AsyncSession` across concurrent tasks (`asyncio.gather`).

## 4. Required

- Every pipeline: typed input, typed output, a `.v1` prompt file, structured logs with `pipeline`, `prompt_version`, `model`, `tokens_in`, `tokens_out`, `latency_ms`, `cost_usd`.
- Every model call: timeout, retry with backoff, max tokens, and a cost record.
- Every pipeline change: an eval in `eval/` with a fixed dataset (≥ 20 cases) and a scorer; the plan states the threshold and the PR reports the score before and after.
- Every external boundary validated with Pydantic. Every endpoint documented by its schema.
- Idempotent jobs; a retry never double-writes.
- API input models: `model_config = ConfigDict(extra="forbid")`; separate `In` / `Out` models; `response_model` (or return annotation) on every route.
- Every error is RFC 9457 `application/problem+json` via registered exception handlers (§8).
- Every `httpx` client has an explicit `httpx.Timeout`; one shared client per app, created in `lifespan`.
- Secrets typed `SecretStr` in settings.
- Model output parsed into a Pydantic model; parse failure → repair-or-reject path with a test (never trusted as SQL/shell/HTML/URL input).
- Type hints on every function; `mypy --strict` (or pyright strict) clean.
- Every new endpoint → Postman request(s) in the same change.

## 5. Tests

- Unit tests mock `clients/model.py` with recorded responses (`vcr`-style fixtures under `tests/fixtures/`), never a live model.
- Evals run separately (`pytest -m eval`) and are required for any prompt or pipeline change.
- Every error path (timeout, malformed model output, empty retrieval) has a test.
- Every new endpoint: integration test through `httpx.AsyncClient(transport=ASGITransport(app=app))` against a Testcontainers Postgres (if the service has a DB): happy path + one failure + one authz case. Full rules: `conventions/python-testing.md`.
- Outbound HTTP mocked with `respx`; no real network in the unit tier.
- Never edit, skip (`@pytest.mark.skip`, `xfail` added to hide a failure), or delete an existing test to make it pass. If a test is wrong, stop and write `BLOCKED: <test> — <why>` in the report.

## 6. DO / DON'T catalog (every DON'T is a blocking review finding)

| # | DON'T | DO |
|---|-------|----|
| 6.1 | `openai.chat.completions.create(...)` in a pipeline | `clients.model.complete(...)` |
| 6.2 | `PROMPT = """You are…"""` | `prompts/summarise.v2.md` loaded by name |
| 6.3 | Change a prompt without an eval run | Eval before/after in the PR |
| 6.4 | `temperature=0.7` literal | `settings.summarise_temperature` |
| 6.5 | Parse model JSON with `json.loads` and hope | Pydantic model + repair-or-reject path with a test |
| 6.6 | Log the full prompt | Log hash, version, token counts |
| 6.7 | `except Exception:` swallow | Typed error, re-raise or return a failure result |
| 6.8 | A notebook as the implementation | Module + test + eval |
| 6.9 | `datetime.utcnow()` | `datetime.now(UTC)` via injected `Clock` |
| 6.10 | `httpx.AsyncClient()` per request | `app.state.http` created in `lifespan` |
| 6.11 | `raise HTTPException(404)` in `service.py` | `raise OrderNotFoundError(order_id)` mapped to problem+json |
| 6.12 | `session.query(Order).get(id)` | `await session.get(Order, id)` / `select(Order).where(...)` |
| 6.13 | `os.getenv("KEY")` in a module | `settings.key` from `get_settings()` |
| 6.14 | Untrusted text concatenated into the system prompt | untrusted text in the user/tool message, delimited and labelled as data |

## 7. New project from scratch

Use only when `01-context.md` says the package does not exist yet. Walking-skeleton rules: global skill `momenta-greenfield-bootstrap`.
```bash
uv init --package acme --python 3.13 && cd acme
uv add "fastapi[standard]" pydantic-settings "sqlalchemy[asyncio]" asyncpg alembic structlog httpx tenacity
uv add --dev pytest pytest-asyncio pytest-cov "testcontainers[postgres]" factory-boy hypothesis respx time-machine asgi-lifespan ruff mypy
uv run alembic init -t async migrations
uv run fastapi dev src/acme/main.py
```
- Every name above is verified per `momenta-dependency-policy` before `uv add`; `uv.lock` committed.
- `lifespan`:
```python
@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    app.state.engine = create_async_engine(settings.database_url, pool_pre_ping=True)
    app.state.sessionmaker = async_sessionmaker(app.state.engine, expire_on_commit=False)
    app.state.http = httpx.AsyncClient(timeout=httpx.Timeout(10.0, connect=3.0))
    yield
    await app.state.http.aclose()
    await app.state.engine.dispose()
```
- Health: `/health/live` (no deps) and `/health/ready` (`SELECT 1` with timeout); unauthenticated; excluded from OpenAPI (`include_in_schema=False`).
- Dockerfile (uv, multi-stage, non-root):
```dockerfile
FROM python:3.13-slim AS builder
COPY --from=ghcr.io/astral-sh/uv:<pinned-version> /uv /uvx /bin/
ENV UV_COMPILE_BYTECODE=1 UV_LINK_MODE=copy UV_PYTHON_DOWNLOADS=0
WORKDIR /app
RUN --mount=type=cache,target=/root/.cache/uv \
    --mount=type=bind,source=uv.lock,target=uv.lock \
    --mount=type=bind,source=pyproject.toml,target=pyproject.toml \
    uv sync --locked --no-install-project --no-dev
COPY . .
RUN --mount=type=cache,target=/root/.cache/uv uv sync --locked --no-dev
FROM python:3.13-slim
RUN useradd -r -u 10001 app
COPY --from=builder --chown=app:app /app /app
ENV PATH="/app/.venv/bin:$PATH"
USER app
CMD ["uvicorn", "acme.main:app", "--host", "0.0.0.0", "--port", "8000", "--proxy-headers"]
```
  `<pinned-version>` = an exact uv tag; never `:latest`.
- `docker-compose.yml`: `postgres:17`, `redis:7` if needed, OTLP collector; `healthcheck` on every service; `depends_on: condition: service_healthy`; no fixed host ports for backing services.
- `.env.example` with every `ACME_*` key and dummy values; `.env` gitignored.
- `pyproject.toml` tool config:
```toml
[tool.ruff]
target-version = "py313"
line-length = 100
[tool.ruff.lint]
select = ["E","F","W","I","B","UP","SIM","ASYNC","S","DTZ","RUF","PT","N","C4","PERF","TRY","FAST"]
[tool.mypy]
strict = true
plugins = ["pydantic.mypy"]
[tool.pytest.ini_options]
addopts = "-ra --strict-markers --strict-config"
asyncio_mode = "auto"
asyncio_default_fixture_loop_scope = "session"
testpaths = ["tests"]
markers = ["eval: model evaluations, excluded from default CI"]
```

## 8. HTTP contract

Full shapes in `momenta-api-contract`. FastAPI wiring:
- `app.add_exception_handler(DomainError, problem_handler)`; override `RequestValidationError` → **400** (team default, not FastAPI's 422) problem+json with `code: "VALIDATION_FAILED"` and `errors=[{loc, code, msg}]`; `StarletteHTTPException` → problem+json; catch-all `Exception` → 500 generic, logged with `logger.exception`.
- Response header `Content-Type: application/problem+json`; `traceId` extension from the current span.
- Versioning: `APIRouter(prefix="/v1")`.
- OpenAPI: auto-generated; `generate_unique_id_function` gives stable `operationId`s (`<tag>_<route_name>`); every router tagged; `responses={404: {"model": Problem}, ...}` declared per route. CI exports `app.openapi()` to `openapi/v1.json` and diffs it (`oasdiff breaking`). Public prod: `docs_url=None, redoc_url=None`.
- Pagination: `limit: Annotated[int, Query(ge=1, le=100)] = 20`, `cursor: str | None`; keyset `WHERE (created_at, id) < (:c_at, :c_id) ORDER BY created_at DESC, id DESC`; response `{ items, nextCursor }`.
- Idempotency: `Idempotency-Key` header on POSTs creating orders/payments; table unique on `(user_id, key)`; claim with `INSERT … ON CONFLICT DO NOTHING RETURNING`.
- Rate limiting at the gateway; in-app only with the plan naming the library and a Redis backend.
- CORS: `CORSMiddleware(allow_origins=settings.cors_origins, ...)`; never `["*"]` with `allow_credentials=True`. `TrustedHostMiddleware`; uvicorn `--forwarded-allow-ips` set to the proxy.
- AI endpoints streaming tokens use SSE (`StreamingResponse` with `text/event-stream`) and enforce a per-request token budget and timeout.

## 9. Data

- SQLAlchemy 2.x typed ORM: `class Base(DeclarativeBase)`, `Mapped[int]`, `mapped_column(...)`, `select()` queries.
- One `AsyncSession` per request via dependency (`async with sessionmaker() as s: yield s`); `expire_on_commit=False`.
- Relationships default `lazy="raise"`; load with `selectinload()` / `joinedload()` explicitly (N+1 becomes an error).
- Transactions: `async with session.begin():` at the service/use-case boundary; one commit per use case.
- Migrations: `uv run alembic revision --autogenerate -m "<what>"` then review the script; `alembic upgrade head` as a release step; `MetaData(naming_convention=...)` set; expand/contract; data backfills in separate batched migrations.
- Optimistic concurrency: `version_id_col` in `__mapper_args__`; `StaleDataError` → 409 `CONCURRENCY_CONFLICT`. Short critical sections: `with_for_update()`.
- Time: `DateTime(timezone=True)` columns; `datetime.now(UTC)` through an injected `Clock` protocol.
- Money: `Numeric(18, 2)` ↔ `Decimal`; never `float`.
- Vector/RAG queries filter by tenant/user ACL inside the query (metadata filter), not after retrieval.

## 10. Cross-cutting

- AuthN: `HTTPBearer` + `PyJWT` (`jwt.decode(token, key, algorithms=["RS256"], audience=..., issuer=...)`) with `PyJWKClient` for JWKS. Passwords: `argon2-cffi` or `pwdlib`.
- AuthZ: dependencies `require_scope("orders:write")` via `Security(...)`; resource checks in the service; tenant from the token only.
- Logging: `structlog` JSON in prod; contextvars bind `request_id` and `trace_id` in middleware; stdlib logging routed through structlog; never `print`.
- OpenTelemetry: `opentelemetry-instrument uvicorn acme.main:app` with `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_ENDPOINT`; multi-worker or `--reload` → programmatic `FastAPIInstrumentor.instrument_app(app)`, `SQLAlchemyInstrumentor`, `HTTPXClientInstrumentor`.
- Config: one `@lru_cache` `get_settings()` injected as a dependency (tests override via `app.dependency_overrides`).
- Resilience: `tenacity` (`stop_after_attempt(3)`, `wait_exponential_jitter()`, `retry_if_exception_type(httpx.TransportError)`) only for idempotent calls.
- Background jobs: `BackgroundTasks` only for trivial fire-and-forget; durable work → Celery / Dramatiq / arq / Taskiq in a separate process (the plan names which).
- LLM safety: untrusted text (user input, retrieved docs, tool output) never goes into the system prompt; tools minimal and executed with the end user's permissions; destructive tools need human confirmation; `max_tokens`, timeouts, per-user rate/cost caps on every model path.

## 11. Tooling & CI commands

Run in this order; each must exit 0. Paste commands + output tails + exit codes in the implementation report.
```bash
uv sync --locked
uv run ruff format --check .
uv run ruff check .
uv run mypy src                     # or: uv run pyright
uv run pytest -q -m "not eval" --cov=acme --cov-branch --cov-report=xml --cov-report=term-missing
uv run pytest -m eval               # only when a prompt/pipeline changed
uvx pip-audit                       # or: osv-scanner scan source -r .
```
- Bumping a dependency: `uv lock --upgrade-package <name>`; commit `uv.lock`. No hand-edited `requirements.txt` (export with `uv export` when a platform needs it).

## 12. Dependencies & licences

Policy: `momenta-dependency-policy`. A package is added only if the plan names it with an exact version.
- Verify before adding: `pip index versions <pkg>` and `curl -s https://pypi.org/pypi/<pkg>/json | jq '.info.license, .info.project_urls'`; upload date of the chosen version from `.releases["<ver>"][0].upload_time_iso_8601`.
- Python has the highest LLM package-hallucination rate: never add a name you have not seen on PyPI in this session.
- Do not add: `python-jose`, `passlib`, `fastapi-jwt-auth`, `requests`, `pytz`, `pydantic-sqlalchemy`-style invented helpers.
- LLM SDKs (`openai`, `anthropic`, `litellm`) pinned with `==`; bumped only via a plan with eval before/after.
- Licence allow-list: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, PSF-2.0. Check with `uvx pip-licenses --from=mixed`.

## 13. Security gotchas (Python)

- `pickle`, `yaml.load` (use `yaml.safe_load`), `eval`/`exec`, `subprocess(..., shell=True)`.
- `tarfile.extractall` without `filter="data"`; `zipfile` extraction without path checks (zip-slip).
- `httpx`/`requests` with `verify=False` or no timeout.
- `random` for tokens → `secrets`. Untrusted XML → `defusedxml`. `tempfile.mktemp` → `NamedTemporaryFile`.
- `assert` for security checks (stripped with `-O`).
- f-string SQL / `text(f"...")`.
- SSRF: URLs from users or model output → allow-list scheme+host, block private/link-local/metadata IPs, no auto-follow redirects.
- Prompt injection: retrieved/user/tool text is data; model output validated before any sink (SQL, shell, HTML, file path, URL).
- Secrets or PII in logs, prompts logged at INFO, or eval fixtures.

## 14. LLM mistakes to avoid (Python)

- Pydantic v1 syntax; `from pydantic import BaseSettings` (moved to `pydantic-settings`); `orm_mode` (now `from_attributes=True`).
- `from sqlalchemy.ext.declarative import declarative_base`; `session.query(User).get(id)`; `Column(Integer)` without `Mapped`.
- `@pytest.fixture def event_loop()` (removed in pytest-asyncio 1.0); `@pytest.mark.asyncio` missing under `asyncio_mode=strict`.
- `TestClient(app)` without `with` (lifespan never runs); `AsyncClient(app=app)` (removed; use `ASGITransport`).
- `requests` inside `async def`; `asyncio.run()` inside a running loop.
- `datetime.utcnow()`, `pytz`, scattered `os.getenv`.
- `Optional[str]` field assumed optional without a default (it is required in Pydantic v2).
- Hallucinated or unmaintained packages (`fastapi-jwt-auth`, `flask-restful-swagger-3`).
- Editing, skipping or deleting a failing test instead of fixing the code.

## 15. Definition of done

- [ ] `uv sync --locked`, `ruff format --check`, `ruff check`, `mypy`, `pytest` (non-eval) all exit 0; commands + output tails + exit codes pasted in `03-implementation-python.md`.
- [ ] Prompt or pipeline changed → eval run, score before/after vs the plan's threshold in the report.
- [ ] Every new endpoint: `In`/`Out` models, auth dependency, problem+json errors, Postman request, tests (happy + failure + authz).
- [ ] Every model call through `clients/model.py` with timeout, retries, max tokens, cost log.
- [ ] Model change → reviewed Alembic migration in the same change.
- [ ] No new package unless named in the plan; `uv.lock` updated; verification output in the report.
- [ ] Generated OpenAPI committed; no breaking diff unless the plan declares a new version.
- [ ] No existing test modified/deleted/skipped unless the plan's `### Tests` table lists it.
- [ ] `git diff origin/main -U0 -- '*.py' | grep -E '^\+.*(print\(|utcnow\(|except Exception: *pass|import requests|pytest\.mark\.skip)'` prints nothing.
- [ ] Anything not done → report ends with `BLOCKED: <reason>`.
