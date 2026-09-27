---
name: python-django-feature
description: Style guide for every change in this repo's Django project (Django + DRF). The planner, style-checker, reviewer and implementer judge the Django code against this file and nothing else. Team default; tune the sections, keep the numbering.
---

# Python · Django feature handbook

Stack: **Python 3.12 · Django 5 · Django REST Framework · django-filter · Celery for jobs · pytest-django · ruff · mypy (django-stubs)**
  → 2026-09 update: Python **3.13** · **Django 5.2 LTS** default (supported to Apr 2028); Django 6.0 only for greenfield that needs its built-in tasks API / CSP / template partials (Python ≥3.12) and only with DRF ≥3.17 · DRF 3.17 · drf-spectacular for OpenAPI · psycopg 3 · uv + `uv.lock`.
Use Django's own machinery: models, managers, serializers, viewsets, permissions, migrations. A pattern Django already has is never reimplemented.
Shared contract rules (errors, pagination, idempotency, versioning): global skill `momenta-api-contract`. New packages: global skill `momenta-dependency-policy`.

## 1. Layout

- One Django app per bounded area in `apps/<area>/`: `models.py`, `managers.py`, `serializers.py`, `views.py`, `urls.py`, `services.py`, `selectors.py`, `permissions.py`, `tasks.py`, `migrations/`, `tests/`.
- Project settings in `config/settings/{base,dev,test,prod}.py`. Env vars read only there via `django-environ`.
- Writes go through `services.py` (functions), reads through `selectors.py`. Views orchestrate; they do not decide.
- No file over 250 lines. `models.py` over that splits into a `models/` package.
- `config/{urls.py, asgi.py, wsgi.py}`; `manage.py`, `pyproject.toml`, `uv.lock`, `.python-version` at the root.
- Custom user model (`AUTH_USER_MODEL = "accounts.User"`) exists before the first migration of a new project.
- Test factories in `apps/<area>/tests/factories.py`.

## 2. Naming

- Apps and modules `snake_case`. Models `PascalCase` singular. Managers `<Model>Manager` / QuerySets `<Model>QuerySet`.
- Serializers `<Model><Purpose>Serializer` (`OrderCreateSerializer`, `OrderListSerializer`). Never one serializer for read and write.
- Services are verbs (`order_create`, `order_cancel`). Selectors are `<model>_list`, `<model>_get`.
- Error codes: constants in `apps/core/errors.py`, `UPPER_SNAKE`, raised as `ApplicationError(code, status)` mapped by one exception handler.
- URLs kebab-case, named `<app>:<resource>-<action>`.
- Services and selectors take keyword-only arguments (`def order_cancel(*, order: Order, by: User, clock: Clock = system_clock) -> Order`).
- Migrations get a descriptive `--name` (`makemigrations orders --name add_order_version`).
- JSON fields camelCase on the wire (`djangorestframework-camel-case` renderer/parser if the plan adopts it; otherwise serializer `source=` mapping); the choice is one per project.
- Tests `test_<unit>_<scenario>_<expected>`.

## 3. Forbidden

- Business logic in views, serializers, or signals. `.save()` with side effects hidden in `Model.save()`.
- `objects.raw()` / raw SQL outside a selector with a comment on why. `.all()` unpaged from a list endpoint.
- N+1 queries: a list endpoint without `select_related` / `prefetch_related` where the serializer follows a relation.
- `settings.X` read outside `config/settings/`; `os.environ` anywhere else.
  → 2026-09 update: reading `django.conf.settings.X` in app code is allowed; the rule is that settings are *defined* only in `config/settings/` and `os.environ` is read only there.
- `print`. Bare `except:`. Swallowed exceptions.
- Fat serializers doing writes (`create()` / `update()` with logic). They call a service.
- Migrations edited after merge. Squash instead.
- Comments beyond one line for a non-obvious invariant. `# TODO`. Commented-out code.
- `ModelSerializer` with `fields = "__all__"` or `exclude = [...]`.
- `.extra()`, f-string SQL in `raw()`/`cursor.execute()`; use params / `RawSQL(sql, params)`.
- `datetime.now()` / `datetime.utcnow()`; use `django.utils.timezone.now()` via the injected clock.
- Importing models directly inside `RunPython` migrations (use `apps.get_model`).
- Sync ORM calls in `async def` views without `sync_to_async`; async views without an I/O fan-out reason.
- `DEBUG=True`, `ALLOWED_HOSTS=["*"]`, `@csrf_exempt` on cookie-authenticated endpoints.
- `ATOMIC_REQUESTS=True` as a blanket fix; transactions are explicit in services.
- SQLite in tests of a PostgreSQL project.

## 4. Required

- Every endpoint: a DRF view or viewset, explicit `permission_classes`, a dedicated input serializer and output serializer, pagination on lists, filtering through `django-filter`.
- Every write: `@transaction.atomic` in the service, validation in the service (not only in the serializer), one commit.
- Every error path: `ApplicationError` with a constant; the handler returns `{ "code", "message" }` and the documented status.
  → 2026-09 update: the handler returns RFC 9457 `application/problem+json`: `type, title, status, detail, instance, code, traceId` (+ `errors` for validation). `code` = the constant. Existing `{ code, message }` APIs add the problem fields and keep `message` until the next major API version (see `momenta-api-contract`).
- Every model change → migration in the same change, reviewed for data safety. Every new endpoint → Postman request(s) in the same change.
- Type hints on services and selectors. `mypy` with `django-stubs` and `ruff` clean in `build`.
- Celery tasks idempotent, retried with backoff, arguments serialisable (ids, not objects).
- `get_queryset()` scoped to the requesting user/tenant; object lookups outside it return 404.
- `REST_FRAMEWORK["DEFAULT_PERMISSION_CLASSES"] = ["rest_framework.permissions.IsAuthenticated"]` (deny by default).
- Side effects after commit via `transaction.on_commit(...)` (emails, task enqueue, webhooks).
- DB constraints (`UniqueConstraint`, `CheckConstraint(condition=...)`) back every uniqueness/range rule the service enforces.
- `@extend_schema` on every view whose request/response drf-spectacular cannot infer.

## 5. Tests

- `pytest-django`. `apps/<area>/tests/test_<thing>.py`. Factories with `factory_boy`; no fixtures loading JSON dumps.
- API tests through `APIClient` for every endpoint: status + error code per scenario. Service tests for every rule and every raised `ApplicationError`.
- `django_assert_num_queries` on list endpoints to lock out N+1.
- No mocking the ORM; use the test database.
- Test database = PostgreSQL (compose service or Testcontainers); migrations run in CI (no `--nomigrations`). Full rules: `conventions/python-django-testing.md`.
- Every new endpoint: happy path + one failure + one authz case (other user/tenant → 404/403); error responses assert `application/problem+json` and `code`.
- Never edit, skip, or delete an existing test to make it pass. If a test is wrong, stop and write `BLOCKED: <test> — <why>` in the report.

## 6. DO / DON'T catalog (every DON'T is a blocking review finding)

| # | DON'T | DO |
|---|-------|----|
| 6.1 | `order.status = "cancelled"; order.save()` in a view | `order_cancel(order=order, by=user)` in `services.py` |
| 6.2 | `Order.objects.all()` in a list view | `order_list(filters=...)` selector, paginated, `select_related` |
| 6.3 | One `OrderSerializer` for input and output | `OrderCreateSerializer` + `OrderDetailSerializer` |
| 6.4 | `raise ValidationError("bad")` in a service | `raise ApplicationError(ErrorCodes.INVALID_INPUT, 400)` |
| 6.5 | Logic in `Model.save()` or a `post_save` signal | explicit service call |
| 6.6 | `os.environ["SECRET"]` in an app | `settings.SECRET` from `config/settings/` |
| 6.7 | Model change without migration | `makemigrations` in the same PR, migration reviewed |
| 6.8 | New endpoint, no Postman request | request(s) added in the same change |
| 6.9 | Celery task taking a model instance | task takes an id, loads inside |
| 6.10 | `get_object_or_404(Order, pk=pk)` | `get_object_or_404(order_list(user=request.user), pk=pk)` |
| 6.11 | `order.stock = order.stock - 1; order.save()` | `Order.objects.filter(pk=pk).update(stock=F("stock") - 1)` |
| 6.12 | `send_email.delay(order.id)` inside `atomic()` | `transaction.on_commit(lambda: send_email.delay(order.id))` |
| 6.13 | `CheckConstraint(check=Q(...))` | `CheckConstraint(condition=Q(...), name="...")` |
| 6.14 | `SerializerMethodField` running a query per row | annotate/prefetch in the selector |
| 6.15 | `if qs.count() > 0` | `if qs.exists()` |

## 7. New project from scratch

Use only when `01-context.md` says the Django project does not exist yet. Walking-skeleton rules: global skill `momenta-greenfield-bootstrap`.
```bash
uv init acme --python 3.13 && cd acme
uv add "django>=5.2,<5.3" djangorestframework drf-spectacular django-filter django-environ \
  "psycopg[binary,pool]" django-cors-headers structlog django-structlog gunicorn
uv add --dev pytest pytest-django pytest-cov factory-boy ruff mypy django-stubs djangorestframework-stubs time-machine
uv run django-admin startproject config .
mkdir -p apps/accounts && uv run python manage.py startapp accounts apps/accounts
```
- Every name above is verified per `momenta-dependency-policy` before `uv add`; `uv.lock` committed.
- Create the custom user model and run the first `makemigrations` only after it exists.
- Settings: `env = environ.Env()`; `DEBUG = env.bool("DEBUG", False)`; `DATABASES = {"default": env.db()}`; `SECRET_KEY`, `ALLOWED_HOSTS`, `CSRF_TRUSTED_ORIGINS` from env; `USE_TZ = True`; `TIME_ZONE = "UTC"`.
- DB pool (Django ≥5.1): `"OPTIONS": {"pool": {"min_size": 2, "max_size": 10}}`; do not combine with persistent `CONN_MAX_AGE`.
- Health: `/health/live` (no deps) and `/health/ready` (`connection.ensure_connection()`), unauthenticated.
- Dockerfile: uv multi-stage non-root build as `python-feature` §7; `RUN python manage.py collectstatic --noinput` (WhiteNoise if the app serves static); `CMD ["gunicorn", "config.wsgi", "-w", "3", "-b", "0.0.0.0:8000"]` (ASGI: `-k uvicorn_worker.UvicornWorker config.asgi`).
- `docker-compose.yml`: `postgres:17`, `redis:7` (cache, throttling, Celery broker), `healthcheck` on each; `depends_on: condition: service_healthy`; migrations in a one-shot `migrate` service, not in the app entrypoint.
- `.env.example` lists every env key with dummy values; `.env` gitignored.

## 8. HTTP contract

Full shapes in `momenta-api-contract`.
- `REST_FRAMEWORK["EXCEPTION_HANDLER"] = "apps.core.exceptions.problem_exception_handler"`: converts DRF `ValidationError` (→ 400 `VALIDATION_FAILED` + `errors`), `APIException`, `Http404`, `PermissionDenied`, `ApplicationError`, and known `IntegrityError` constraints (→ 409) into `application/problem+json`; unknown → 500 generic, logged.
- Versioning: `DEFAULT_VERSIONING_CLASS = "rest_framework.versioning.URLPathVersioning"`, `ALLOWED_VERSIONS = ["v1"]`, URLs `path("api/<version>/", include(...))`.
- OpenAPI: `DEFAULT_SCHEMA_CLASS = "drf_spectacular.openapi.AutoSchema"`; CI runs `manage.py spectacular --file openapi/v1.yaml --validate --fail-on-warn` and diffs it (`oasdiff breaking`). Swagger UI only in non-prod settings.
- Pagination: `CursorPagination` (`ordering = ("-created_at", "-id")`, `page_size = 20`, `max_page_size = 100`) for feeds/large lists; `PageNumberPagination` with `max_page_size = 100` for admin grids. Response shape per `momenta-api-contract` (custom `get_paginated_response` returning `items`, `nextCursor`).
- Filtering: `django-filter` `FilterSet` with explicit fields; `OrderingFilter` with an `ordering_fields` whitelist.
- Idempotency: `IdempotencyKey` model with `UniqueConstraint(fields=["user", "key"])`, claimed with `get_or_create` inside `atomic()`.
- Throttling: `UserRateThrottle` / `ScopedRateThrottle` with the Redis cache backend (LocMem is per process: forbidden in prod); 429 + `Retry-After`.
- CORS: `django-cors-headers`, `CORS_ALLOWED_ORIGINS` explicit, `CorsMiddleware` placed above `CommonMiddleware`.
- Caching: `django.core.cache.backends.redis.RedisCache`; `cache_page` only on anonymous endpoints; versioned low-level keys.
- django-ninja only in projects that already use it; never mixed with DRF in one project.

## 9. Data

- Models: `TextChoices` for enums; `DecimalField(max_digits=18, decimal_places=2)` for money; `db_default` for DB-side defaults; `Meta.indexes` and `Meta.constraints` declared.
- Selectors: `select_related` (FK/1-1), `prefetch_related` (M2M/reverse), `only()`/`values()` for wide tables, `exists()` not `count()`, `iterator(chunk_size=...)` for large scans, `bulk_create`/`bulk_update` in batches.
- Transactions: `with transaction.atomic():` in services; `select_for_update()` only inside `atomic()`.
- Optimistic concurrency: `version` `IntegerField`; `Order.objects.filter(pk=pk, version=v).update(..., version=F("version") + 1)`; 0 rows → 409 `CONCURRENCY_CONFLICT`; ETag / `If-Match` on PUT/PATCH (412 on mismatch).
- Migrations: `makemigrations` committed; CI `makemigrations --check --dry-run`; `migrate` as a release step; expand/contract; `RunPython` with `reverse_code` and `apps.get_model`; `AddIndexConcurrently` (`atomic = False`) on big Postgres tables; `SeparateDatabaseAndState` for zero-downtime renames.
- Time: `USE_TZ = True`; `timezone.now()` only through the injected clock in services.

## 10. Cross-cutting

- AuthN: `djangorestframework-simplejwt` (or OIDC via the gateway); session auth + CSRF for same-site SPAs. Never `rest_framework_jwt`.
- AuthZ: `permission_classes` per view + `has_object_permission`; tenant filter in `get_queryset()`.
- DI: collaborators passed as keyword args with production defaults (`clock: Clock = system_clock`, `payments: PaymentsClient | None = None`).
- Logging: `LOGGING` dictConfig with a JSON formatter; structlog + `django-structlog` binds `request_id`.
- OpenTelemetry: `opentelemetry-instrumentation-django` (+ psycopg, redis, celery instrumentations) via `opentelemetry-instrument gunicorn config.wsgi`; `DJANGO_SETTINGS_MODULE` set before instrumenting.
- Background jobs: Celery (+ `django-celery-beat`) default. Django 6.0 `django.tasks` only on 6.0 projects and only with a production worker backend named in the plan (built-in backends are dev/test only). Tasks take ids, are idempotent, are enqueued with `transaction.on_commit`.
- Async: async views only for I/O fan-out under ASGI; ORM via `aget`/`acreate`/`async for` or `sync_to_async`.
- Outbound HTTP: `httpx` with explicit timeout; `tenacity` retries only for idempotent calls.
- Prod security settings: `SECURE_HSTS_SECONDS`, `SESSION_COOKIE_SECURE`, `CSRF_COOKIE_SECURE`, `SECURE_PROXY_SSL_HEADER` (only behind a trusted proxy); Django 6.0 HTML surfaces: `SECURE_CSP` + `ContentSecurityPolicyMiddleware`.

## 11. Tooling & CI commands

Run in this order; each must exit 0. Paste commands + output tails + exit codes in the implementation report.
```bash
uv sync --locked
uv run ruff format --check .
uv run ruff check .
uv run mypy .
uv run python manage.py check --deploy --settings=config.settings.prod
uv run python manage.py makemigrations --check --dry-run
uv run python manage.py spectacular --file openapi/v1.yaml --validate --fail-on-warn
uv run pytest --create-db --cov --cov-branch --cov-report=xml --cov-report=term-missing
uvx pip-audit
```
- Ruff: `python-feature` §7 `select` minus `FAST`, plus `"DJ"`.
- mypy: `plugins = ["mypy_django_plugin.main", "mypy_drf_plugin.main"]`, `[tool.django-stubs] django_settings_module = "config.settings.test"`.

## 12. Dependencies & licences

Policy: `momenta-dependency-policy`. A package is added only if the plan names it with an exact version.
- Verify before adding: `pip index versions <pkg>`; licence, project URLs and supported Django versions (classifiers) from `https://pypi.org/pypi/<pkg>/json`.
- A new Django package must declare support for the project's Django major (classifier `Framework :: Django :: 5.2` or `6.0`).
- Do not add: `drf-yasg` (Swagger 2 only; use drf-spectacular), `djangorestframework-jwt` / `rest_framework_jwt` (dead; use simplejwt), `django-rest-swagger`, `python-jose`, `passlib`, `pytz`, `requests`.
- Licence allow-list: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, PSF-2.0. Check with `uvx pip-licenses --from=mixed`.

## 13. Security gotchas (Django / DRF)

- DRF default permission is `AllowAny` if not set globally: `DEFAULT_PERMISSION_CLASSES` must be `IsAuthenticated`.
- `fields = "__all__"` serializers: mass assignment + data exposure.
- `get_queryset()` / `get_object_or_404` not filtered by `request.user`/tenant (BOLA).
- `raw()`, `extra()`, `RawSQL`, `cursor.execute` with f-strings (SQL injection).
- `mark_safe`, `|safe`, `{% autoescape off %}` on user data (XSS).
- `@csrf_exempt` on session-authenticated endpoints.
- `DEBUG=True`, `ALLOWED_HOSTS=["*"]`, `SECRET_KEY` in code; `check --deploy` warnings ignored.
- File uploads without size limit / extension allow-list; user files served inline from `MEDIA_ROOT`.
- General Python sinks: `pickle`, `yaml.load`, `eval`, `subprocess(shell=True)`, `verify=False`, `random` for tokens.

## 14. LLM mistakes to avoid (Django)

- `from django.conf.urls import url` (removed; use `path`/`re_path`); `ugettext` (removed); `USE_L10N` (removed in 5.0); `index_together` (removed in 5.1).
- `CheckConstraint(check=...)` (deprecated in 5.1 → `condition=`); `django.utils.timezone.utc` (removed in 5.0 → `datetime.UTC`).
- `JSONField` from `django.contrib.postgres.fields` (use `models.JSONField`).
- `drf-yasg` for OpenAPI 3; `rest_framework_jwt`.
- `ATOMIC_REQUESTS=True` as the default transaction fix; `get_object_or_404` inside services.
- `CELERY_ALWAYS_EAGER` / `CELERY_TASK_ALWAYS_EAGER` in production settings.
- `django.tasks` on 5.2 (it is 6.0+), or assuming 6.0's built-in task backends run jobs in production.
- `DEFAULT_AUTO_FIELD` churn on 6.0 (default is already `BigAutoField`).
- Editing an applied migration instead of adding a new one; editing, skipping or deleting a failing test.

## 15. Definition of done

- [ ] All §11 commands exit 0; commands + output tails + exit codes pasted in `03-implementation-python-django.md`.
- [ ] Every new endpoint: view with explicit `permission_classes`, scoped `get_queryset`, separate input/output serializers, pagination on lists, problem+json errors, Postman request, tests (happy + failure + authz + query budget on lists).
- [ ] Every new error code in `apps/core/errors.py` and the OpenAPI error catalogue.
- [ ] Model change → reviewed migration in the same change; `makemigrations --check` clean.
- [ ] `openapi/v1.yaml` regenerated and committed; no breaking diff unless the plan declares a new version.
- [ ] No new package unless named in the plan; `uv.lock` updated; verification output in the report.
- [ ] No existing test modified/deleted/skipped unless the plan's `### Tests` table lists it.
- [ ] `git diff origin/main -U0 -- '*.py' | grep -E '^\+.*(print\(|__all__|\.extra\(|csrf_exempt|datetime\.now\(|utcnow\(|pytest\.mark\.skip)'` prints nothing.
- [ ] Anything not done → report ends with `BLOCKED: <reason>`.
