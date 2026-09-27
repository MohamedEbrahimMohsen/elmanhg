# Testing convention — python-django

<!-- Framework, where tests live, naming pattern (file + method), what every test must assert, what a vacuous test looks like here. -->

## Framework

| Concern | Tool (2026-09) | Notes |
|---|---|---|
| Runner | pytest ≥ 8.4 + pytest-django | `DJANGO_SETTINGS_MODULE = "config.settings.test"` in `[tool.pytest.ini_options]` |
| DB access | `@pytest.mark.django_db` / `db` fixture | each test in a rolled-back transaction; `transaction=True` only for `on_commit`/`select_for_update` behaviour |
| API | DRF `APIClient` | `force_authenticate(user)` in most tests; one real token-flow test per auth scheme |
| Database | PostgreSQL (compose service or Testcontainers) | never SQLite for a Postgres project |
| Data | `factory_boy.django.DjangoModelFactory` | `SubFactory`, `Sequence`, `LazyAttribute` |
| Query budget | `django_assert_num_queries` | every list endpoint |
| Commit hooks | `django_capture_on_commit_callbacks` | instead of `transaction=True` where possible |
| Outbound HTTP | `respx` (httpx) | no real network |
| Time | `time-machine` or injected clock | |
| Parallel | `pytest-xdist -n auto` | pytest-django creates one DB per worker |
| Coverage | `pytest-cov` | branch coverage |

Rules not specific to Django (fakes, hypothesis, markers, flakiness) follow `conventions/python-testing.md`.

## Location and naming

- `apps/<area>/tests/test_services.py`, `test_selectors.py`, `test_api.py`, `test_serializers.py`, `test_models.py` (constraints), `factories.py`.
- Project-wide fixtures (`api_client`, `user`, `other_tenant_user`) in the root `conftest.py`.
- Test function: `test_<unit>_<scenario>_<expected>` (`test_order_cancel_when_shipped_raises_order_already_shipped`).
- One behaviour per test; Arrange / Act / Assert separated by one blank line.

## Every test must

- API tests assert `response.status_code`, the body, and for errors `response["Content-Type"]` starts with `application/problem+json` and `response.json()["code"]`.
- Service tests assert the returned object/state or the raised `ApplicationError` **and** its `code`.
- List endpoints wrap the request in `django_assert_num_queries(<n>)` with a fixed `n` independent of row count (create ≥ 3 rows).
- Constraint tests: violating a `UniqueConstraint`/`CheckConstraint` raises `IntegrityError` (inside `transaction.atomic()`).
- Side effects registered with `on_commit` are asserted via `django_capture_on_commit_callbacks(execute=True)`.
- Be deterministic: `time-machine`/injected clock, factory `Sequence`s, no sleeps.
- Be independent: no data shared across tests except via fixtures.

## Never

- Edit, weaken, skip, or delete an existing test to make it pass. The orchestrator's test-integrity guard blocks it. If a test is wrong: stop and write `BLOCKED: <test> — <why>` in the report.
- Edit an applied migration to make tests pass.
- Mock the ORM (`Order.objects`) or `QuerySet` methods.
- Load JSON fixture dumps (`loaddata`) for test data.
- Use SQLite or `--nomigrations` in CI.
- Use `CELERY_TASK_ALWAYS_EAGER` / `ImmediateBackend` outside the unit-tier test settings.
- Assert that a mock returns what it was told to return.

## Unit vs integration boundary

| Tier | Covers | DB | Doubles allowed |
|---|---|---|---|
| Unit | pure helpers, serializers' field validation via `factory.build()` | no (`build()` objects) | fakes for clients and clock |
| Integration | services, selectors, API views, permissions, constraints, migrations | yes (`django_db`) | `respx` for third-party HTTP |

- Every new endpoint gets ≥ 3 API tests: happy path, one failure (400/404/409), one authz (other user/tenant → 404/403, anonymous → 401/403 per auth class).

## Test settings (`config/settings/test.py`)

- `PASSWORD_HASHERS = ["django.contrib.auth.hashers.MD5PasswordHasher"]` (speed; tests only).
- `EMAIL_BACKEND = "django.core.mail.backends.locmem.EmailBackend"`.
- Cache: `LocMemCache` except tests of throttling behaviour, which use the Redis service.
- Tasks: Celery `task_always_eager = True` / Django 6 `ImmediateBackend` only in this module.
- Overrides inside a test via the pytest-django `settings` fixture, never by editing `test.py` for one test.

## Example tests

```python
@pytest.mark.django_db
def test_order_detail_other_tenant_returns_404_problem(api_client: APIClient) -> None:
    order = OrderFactory(tenant__slug="tenant-a")
    api_client.force_authenticate(UserFactory(tenant__slug="tenant-b"))

    response = api_client.get(reverse("orders:order-detail", kwargs={"version": "v1", "pk": order.pk}))

    assert response.status_code == 404
    assert response["Content-Type"].startswith("application/problem+json")
    assert response.json()["code"] == "ORDER_NOT_FOUND"


@pytest.mark.django_db
def test_order_list_query_count_is_constant(api_client: APIClient, django_assert_num_queries) -> None:
    user = UserFactory()
    OrderFactory.create_batch(5, tenant=user.tenant)
    api_client.force_authenticate(user)

    with django_assert_num_queries(3):
        response = api_client.get(reverse("orders:order-list", kwargs={"version": "v1"}))

    assert response.status_code == 200
    assert len(response.json()["items"]) == 5
```

## Coverage and commands

```bash
uv run pytest --create-db -n auto --cov --cov-branch --cov-report=xml --cov-report=term-missing   # CI
uv run pytest --reuse-db                                                                          # local
```
- Gate: changed-line coverage ≥ 80% in the pipeline. Excluded: `migrations/`, `config/asgi.py`, `config/wsgi.py`, `manage.py`.
- Flaky tests: quarantine marker + issue within 24 h (see `python-testing.md`). Postgres unavailable → report `BLOCKED: database unavailable`, never skip.
