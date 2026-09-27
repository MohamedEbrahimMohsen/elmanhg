---
name: python-flask-feature
description: Style guide for every change in this repo's Flask application. The planner, style-checker, reviewer and implementer judge the Flask code against this file and nothing else. Team default; tune the sections, keep the numbering.
---

# Python · Flask feature handbook

Stack: **Python 3.12 · Flask 3 · Blueprints · Flask-SQLAlchemy · Marshmallow or Pydantic for schemas · Alembic · pytest · ruff · mypy**
  → 2026-09 update: Python **3.13** (3.14 allowed) · Flask **3.1.x** · Flask-SQLAlchemy 3.1 in SQLAlchemy 2.0 style · Flask-Migrate (Alembic) · psycopg 3 · uv + `uv.lock` · gunicorn in containers. New repos: Pydantic v2 schemas; existing repos keep the schema library they already use (one per repo).
Flask is small on purpose. The app factory, blueprints, and a service layer are the whole architecture. Do not add a framework on top of the framework.
Shared contract rules (errors, pagination, idempotency, versioning): global skill `momenta-api-contract`. New packages: global skill `momenta-dependency-policy`.

## 1. Layout

- App factory in `app/__init__.py` (`create_app(config)`). No module-level `app`.
- One blueprint per resource in `app/<resource>/`: `routes.py`, `service.py`, `models.py`, `schemas.py`. Registered in the factory.
- Cross-cutting in `app/core/`: `errors.py`, `db.py`, `config.py`, `logging.py`, `auth.py`.
- Migrations in `migrations/` via Alembic. Every model change ships with its migration.
- No file over 200 lines. A view function over 30 lines moves logic into `service.py`.
- Extensions created unbound in `app/extensions.py` (`db = SQLAlchemy(model_class=Base)`, `migrate = Migrate()`), bound with `init_app` inside `create_app`.
- `wsgi.py` at the repo root: `app = create_app()`; the only module-level app, used only by gunicorn.
- Tests in `tests/<resource>/`; shared fixtures in `tests/conftest.py`; factories in `tests/factories.py`.
- `pyproject.toml` + committed `uv.lock` + `.python-version`.

## 2. Naming

- Modules `snake_case.py`. Classes `PascalCase`. Functions `snake_case`. Constants `UPPER_SNAKE`.
- Blueprint names match the folder (`orders_bp`). View functions are verbs (`create_order`, `list_orders`).
- Schemas end in `Schema` (Marshmallow) or `In`/`Out` (Pydantic). Models have no suffix.
- Error codes: constants in `app/core/errors.py`, `UPPER_SNAKE`, raised as `AppError(code, status)`.
- Config via `app.config` populated from a `Config` class per environment; env vars read only there.
- Blueprint `url_prefix="/v1/<plural-kebab>"`; JSON fields camelCase on the wire (Pydantic `alias_generator=to_camel`, Marshmallow `data_key`).
- Tests `test_<unit>_<scenario>_<expected>`.

## 3. Forbidden

- Business logic in a view. DB queries in a view. `db.session.commit()` anywhere but the service.
- Global `app` import in modules (`from app import app`). Use `current_app` or pass what you need.
- `os.environ` outside `config.py`. Secrets in code.
- `print`. Bare `except:`. `except Exception: pass`.
- Returning raw dicts with hand-built error shapes. Errors go through the `AppError` handler.
- Raw SQL strings in routes or services. `Model.query` in a route.
- Untyped view arguments; `request.json` without schema validation.
- Comments beyond one line for a non-obvious invariant. `# TODO`. Commented-out code.
- `Model.query` anywhere in new code (legacy API); use `db.session.execute(select(...))`, `db.get_or_404`, `db.paginate`.
- `app.run()` / the dev server in a container; `debug=True` outside local (the Werkzeug debugger is remote code execution).
- `@app.before_first_request` (removed), `flask.ext.*`, `flask_script`, `werkzeug.contrib`, `app.json_encoder` (use `app.json` provider).
- `flask-restful`, `flask-restplus` (unmaintained).
- `async def` views for throughput (Flask is WSGI; no concurrency gain).
- `render_template_string` with user input (SSTI). `jsonify` of raw ORM objects.
- `datetime.utcnow()` or naive datetimes.

## 4. Required

- Every route: schema validation on input, schema serialisation on output, explicit status code, `@auth_required` (or the repo's equivalent) where the resource is not public.
- Every error path returns `{ "code": ..., "message": ... }` with the constant and the documented status. One error handler registered in the factory.
  → 2026-09 update: the body is RFC 9457 `application/problem+json`: `type, title, status, detail, instance, code, traceId` (+ `errors` for validation). `code` = the constant. Existing `{ code, message }` APIs add the problem fields and keep `message` until the next major API version (see `momenta-api-contract`).
- Services are plain functions or classes that take the session; they raise `AppError`, never return error tuples.
- Every model change → Alembic migration in the same change. Every new endpoint → Postman request(s) in the same change.
- Type hints on every public function. `mypy` and `ruff` clean in `build`.
- Structured logging with request id. Timeouts on every outbound HTTP call.
- Error handlers registered for `AppError`, `HTTPException` (404/405/413 included, so no HTML error pages), and `Exception` (500 generic, logged with `logger.exception`).
- Prod config sets `SECRET_KEY` (from env), `SECRET_KEY_FALLBACKS` for rotation, `MAX_CONTENT_LENGTH`, `MAX_FORM_MEMORY_SIZE`, `TRUSTED_HOSTS`, `SESSION_COOKIE_SECURE/HTTPONLY/SAMESITE`.
- Behind a proxy: `app.wsgi_app = ProxyFix(app.wsgi_app, x_for=1, x_proto=1)` with the real hop count.
- Tenant/user ids from `g.current_user` (set by auth), never from the request body.

## 5. Tests

- pytest with the app factory fixture (`create_app(TestConfig)`) and a per-test transaction rolled back. Test client for routes; no live server.
- `tests/<resource>/test_routes.py` for each endpoint's status + code per scenario; `test_service.py` for logic and every raised `AppError`.
- Schemas: one passing and one failing case per rule.
- No mocking the ORM against itself; use the test database.
- Test database = PostgreSQL via Testcontainers (never SQLite for a Postgres app). Full rules: `conventions/python-flask-testing.md`.
- Every new endpoint: happy path + one failure + one authz case (other tenant → 404/403); error responses assert `resp.mimetype == "application/problem+json"` and `resp.json["code"]`.
- Never edit, skip, or delete an existing test to make it pass. If a test is wrong, stop and write `BLOCKED: <test> — <why>` in the report.

## 6. DO / DON'T catalog (every DON'T is a blocking review finding)

| # | DON'T | DO |
|---|-------|----|
| 6.1 | `Order.query.filter_by(...)` inside a view | `orders_service.get(order_id)` |
| 6.2 | `return jsonify(error="bad"), 400` | `raise AppError(ErrorCodes.INVALID_INPUT, 400)` |
| 6.3 | `data = request.json; data["amount"]` | `CreateOrderSchema().load(request.json)` |
| 6.4 | `db.session.commit()` in a route | once, in the service, after all mutations |
| 6.5 | `from app import app` | `current_app` / app factory |
| 6.6 | `os.environ["DB_URL"]` in a module | `current_app.config["DB_URL"]` from `config.py` |
| 6.7 | Model change without migration | Alembic revision in the same PR |
| 6.8 | New endpoint, no Postman request | request(s) added in the same change |
| 6.9 | `except Exception: pass` | typed except, log with request id, re-raise or `AppError` |
| 6.10 | `Order.query.get(order_id)` in a service | `db.session.get(Order, order_id)` / `db.get_or_404(Order, order_id)` |
| 6.11 | `CMD ["flask", "run"]` / `app.run(debug=True)` | `CMD ["gunicorn", "--bind", "0.0.0.0:8000", "wsgi:app"]` |
| 6.12 | `CORS(app)` (all origins) | `CORS(app, origins=config["CORS_ORIGINS"])` |
| 6.13 | `requests.get(url)` | `httpx.Client(timeout=httpx.Timeout(10, connect=3))` shared per app |
| 6.14 | `send_file(request.args["path"])` | `send_from_directory(safe_root, validated_name)` |

## 7. New project from scratch

Use only when `01-context.md` says the Flask app does not exist yet. Walking-skeleton rules: global skill `momenta-greenfield-bootstrap`.
```bash
uv init --package app --python 3.13
uv add flask flask-sqlalchemy flask-migrate "psycopg[binary,pool]" pydantic pydantic-settings structlog gunicorn httpx
uv add --dev pytest pytest-cov factory-boy "testcontainers[postgres]" time-machine respx ruff mypy
uv run flask --app wsgi db init
```
- Every name above is verified per `momenta-dependency-policy` before `uv add`; `uv.lock` committed.
- Health: blueprint `health` with `/health/live` (no deps) and `/health/ready` (`db.session.execute(text("SELECT 1"))`), unauthenticated.
- Dockerfile: same uv multi-stage, non-root build as `python-feature` §7, with `CMD ["gunicorn", "--bind", "0.0.0.0:8000", "--workers", "3", "--access-logfile", "-", "wsgi:app"]`.
- `docker-compose.yml`: `postgres:17`, `redis:7` if caching/limits/queues, app with `healthcheck`; `depends_on: condition: service_healthy`; local dev command `flask --app wsgi run --debug` only in compose's dev profile.
- `.env.example` lists every config key with dummy values; `.env` gitignored.
- Migrations: `flask --app wsgi db upgrade` as a release step, never inside `create_app`.

## 8. HTTP contract

Full shapes in `momenta-api-contract`.
```python
def problem(status: int, code: str, title: str, detail: str | None = None, **ext: object) -> Response:
    body = {"type": f"{current_app.config['ERRORS_BASE_URL']}/{code.lower().replace('_', '-')}",
            "title": title, "status": status, "detail": detail, "instance": request.path,
            "code": code, "traceId": g.get("trace_id"), **ext}
    return Response(json.dumps(body), status=status, mimetype="application/problem+json")
```
- Validation errors → 400, `code: "VALIDATION_FAILED"`, `errors: [{ path, code, message }]` (Pydantic `ValidationError` / Marshmallow `ValidationError` mapped in one handler). Missing/invalid JSON body (`request.get_json(silent=True) is None`) → 400, not 415/500.
- Versioning: blueprint `url_prefix="/v1/..."`.
- OpenAPI: contract-first `openapi/v1.yaml` updated in the same change as the route; validated with `uvx openapi-spec-validator openapi/v1.yaml`; breaking changes checked with `oasdiff breaking`. An OpenAPI-generating extension (APIFlask / flask-smorest) only with an ADR.
- Pagination: keyset `{ items, nextCursor }` for large tables; `db.paginate(select(...), page=, per_page=, max_per_page=100)` only for admin grids.
- Idempotency: `Idempotency-Key` header on POSTs creating orders/payments; table unique on `(user_id, key)`.
- Rate limit: `Flask-Limiter` with Redis `storage_uri` (in-memory storage is per-process: forbidden in prod).
- CORS: `flask-cors` with explicit origins. Security headers from the reverse proxy or `flask-talisman`.
- Caching: `Flask-Caching` with Redis or redis-py; per-user data keyed by user/tenant.

## 9. Data

- Models: `class Base(DeclarativeBase)`, `db = SQLAlchemy(model_class=Base)`, `Mapped[...]` + `mapped_column(...)`.
- Queries: `db.session.execute(select(Order).where(...)).scalars()`; relationships `lazy="raise"` by default, loaded with `selectinload()`.
- Transactions: one `db.session.commit()` per use case in the service; `db.session.begin_nested()` for savepoints; rollback in the error path.
- Migrations: `flask db migrate -m "<what>"` → review → commit; `MetaData(naming_convention=...)`; expand/contract across releases; no destructive op unless the plan names it.
- Optimistic concurrency: `version_id_col` → `StaleDataError` → 409 `CONCURRENCY_CONFLICT`.
- Time: `DateTime(timezone=True)`, `datetime.now(UTC)` via an injected clock.
- Money: `Numeric(18, 2)` ↔ `Decimal`.

## 10. Cross-cutting

- AuthN: JWT verified with PyJWT (`algorithms=["RS256"]`, `audience`, `issuer`, `PyJWKClient`) or `flask-jwt-extended`, in a decorator that sets `g.current_user`. Browser forms: CSRF via Flask-WTF.
- AuthZ: `@require_scope("orders:write")` decorators + resource checks in the service.
- Logging: structlog JSON configured before `create_app`; `before_request` binds `request_id`; gunicorn `--access-logfile -`.
- OpenTelemetry: `opentelemetry-instrument gunicorn wsgi:app` or `FlaskInstrumentor().instrument_app(app)`; with gunicorn `--preload`, initialise the SDK in the `post_fork` hook.
- Outbound HTTP: `httpx.Client` with explicit timeout; `tenacity` retries only for idempotent calls.
- Background jobs: Celery / RQ / Dramatiq, tasks run inside `with app.app_context():`, take ids not objects, are idempotent.
- Server: `gunicorn -w <2*CPU+1> -k gthread --threads 4 wsgi:app`, or one worker per container when the orchestrator scales pods.

## 11. Tooling & CI commands

Run in this order; each must exit 0. Paste commands + output tails + exit codes in the implementation report.
```bash
uv sync --locked
uv run ruff format --check .
uv run ruff check .
uv run mypy app
uv run pytest -q --cov=app --cov-branch --cov-report=xml --cov-report=term-missing
uvx openapi-spec-validator openapi/v1.yaml
uvx pip-audit
```
- Ruff `select` as `python-feature` §7 minus `FAST`, plus `"FLASK"` if the installed ruff lists that rule set (`uv run ruff linter | grep -i flask`).

## 12. Dependencies & licences

Policy: `momenta-dependency-policy`. A package is added only if the plan names it with an exact version.
- Verify before adding: `pip index versions <pkg>`; licence and project URLs from `https://pypi.org/pypi/<pkg>/json`.
- Do not add: `flask-restful`, `flask-restplus`, `flask-script`, `python-jose`, `passlib`, `requests`, `pytz`, `Flask-SQLAlchemy<3`.
- Licence allow-list: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, PSF-2.0. Check with `uvx pip-licenses --from=mixed`.

## 13. Security gotchas (Flask)

- `app.run(debug=True)` / `FLASK_DEBUG=1` in any deployed environment: Werkzeug debugger = RCE.
- `render_template_string(user_input)` (SSTI); `Markup(user_input)` / `|safe` on user data (XSS).
- Weak or committed `SECRET_KEY`; client-side `session` is signed, not encrypted: never store secrets in it.
- `send_file` / `send_from_directory` with user-controlled paths.
- `CORS(app)` or `origins="*"` with `supports_credentials=True`.
- Missing `MAX_CONTENT_LENGTH` (memory exhaustion on uploads).
- General Python sinks: `pickle`, `yaml.load`, `eval`/`exec`, `subprocess(..., shell=True)`, `verify=False`, `random` for tokens (use `secrets`).
- Object lookups not filtered by tenant/owner (BOLA) → 404.

## 14. LLM mistakes to avoid (Flask)

- `@app.before_first_request`; `from flask.ext.sqlalchemy import SQLAlchemy`; `flask_script.Manager`.
- `Model.query.get(id)`; `SQLALCHEMY_TRACK_MODIFICATIONS` boilerplate copied everywhere.
- `json.dumps(model.__dict__)`; `app.json_encoder = CustomEncoder`.
- `request.json` without handling a missing/invalid body.
- `from werkzeug.contrib...` imports.
- `app.run(debug=True)` in a Dockerfile `CMD`.
- Hallucinated extensions (`flask-restful-swagger-3`); every new name verified (§12).
- Editing, skipping or deleting a failing test instead of fixing the code.

## 15. Definition of done

- [ ] `uv sync --locked`, `ruff format --check`, `ruff check`, `mypy`, `pytest` all exit 0; commands + output tails + exit codes pasted in `03-implementation-python-flask.md`.
- [ ] Every new route: input/output schema, auth decorator, problem+json errors, `openapi/v1.yaml` updated, Postman request, tests (happy + failure + authz).
- [ ] Every new error code in `app/core/errors.py` and the OpenAPI error catalogue.
- [ ] Model change → reviewed migration in the same change.
- [ ] No new package unless named in the plan; `uv.lock` updated; verification output in the report.
- [ ] No existing test modified/deleted/skipped unless the plan's `### Tests` table lists it.
- [ ] `git diff origin/main -U0 -- '*.py' | grep -E '^\+.*(print\(|\.query\.|debug=True|utcnow\(|except Exception: *pass|pytest\.mark\.skip)'` prints nothing.
- [ ] Anything not done → report ends with `BLOCKED: <reason>`.
