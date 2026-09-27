# Testing convention — python-flask

<!-- Framework, where tests live, naming pattern (file + method), what every test must assert, what a vacuous test looks like here. -->

## Framework

| Concern | Tool (2026-09) | Notes |
|---|---|---|
| Runner | pytest ≥ 8.4 | `addopts = "-ra --strict-markers --strict-config"` |
| App | `create_app(TestConfig)` fixture | `TESTING = True`, DB URL from the container |
| HTTP | `app.test_client()` | no live server |
| CLI | `app.test_cli_runner()` | for `flask` commands |
| Database | `testcontainers[postgres]` + Flask-Migrate `upgrade` once per session | no SQLite for a Postgres app |
| Data | `factory_boy` `SQLAlchemyModelFactory` (`sqlalchemy_session_persistence = "flush"`) | |
| Outbound HTTP | `respx` (httpx) | no real network |
| Time | `time-machine` or injected clock | |
| Coverage | `pytest-cov` | branch coverage |

Rules not specific to Flask (fakes, hypothesis, markers, flakiness) follow `conventions/python-testing.md`.

## Location and naming

- `tests/<resource>/test_routes.py` (every endpoint, status + code per scenario), `tests/<resource>/test_service.py` (every rule and every raised `AppError`), `tests/<resource>/test_schemas.py`.
- Fixtures in `tests/conftest.py`: `app`, `client`, `runner`, `db_session`, `auth_headers`. Factories in `tests/factories.py`.
- Test function: `test_<unit>_<scenario>_<expected>` (`test_create_order_missing_sku_returns_400_validation_failed`).
- One behaviour per test; Arrange / Act / Assert separated by one blank line.

## Every test must

- Route tests assert `resp.status_code`, `resp.json`, and for errors `resp.mimetype == "application/problem+json"` and `resp.json["code"]`.
- Service tests run inside `with app.app_context():` (fixture) and assert the returned value or the raised `AppError` **and** its `code`.
- Schema tests: one valid case + one invalid case per rule.
- Assert persisted state through a fresh `db.session.execute(select(...))` after the request.
- Be deterministic: frozen/injected time, seeded Faker, no sleeps.
- Be independent: per-test transaction rolled back; unique data per test.

## Never

- Edit, weaken, skip, or delete an existing test to make it pass. The orchestrator's test-integrity guard blocks it. If a test is wrong: stop and write `BLOCKED: <test> — <why>` in the report.
- Mock `db.session` or the ORM against itself; use the test database.
- Assert that a mock returns what it was told to return.
- Run the app with `app.run()` or a live port in tests.
- Share `db.session` across threads without an app context.
- Use SQLite or `sqlite:///:memory:` as the test DB of a Postgres app.

## Unit vs integration boundary

| Tier | Covers | Doubles allowed |
|---|---|---|
| Unit | schemas, pure service helpers, error mapping | fakes for clients and clock |
| Integration | `test_client` → view → service → real Postgres; auth decorators; error handlers; migrations | `respx` for third-party HTTP |

- Every new endpoint gets ≥ 3 route tests: happy path, one failure (400/404/409), one authz (other tenant → 404/403, no token → 401).

## Fixture skeleton

```python
@pytest.fixture(scope="session")
def pg_url() -> Iterator[str]:
    with PostgresContainer("postgres:17-alpine", driver="psycopg") as pg:
        yield pg.get_connection_url()

@pytest.fixture(scope="session")
def app(pg_url: str) -> Iterator[Flask]:
    app = create_app(TestConfig(SQLALCHEMY_DATABASE_URI=pg_url))
    with app.app_context():
        flask_migrate.upgrade()                         # same migrations as production
        yield app

@pytest.fixture(autouse=True)
def db_session(app: Flask) -> Iterator[scoped_session]:
    connection = db.engine.connect()
    transaction = connection.begin()
    db.session.configure(bind=connection, join_transaction_mode="create_savepoint")
    yield db.session
    db.session.remove()
    transaction.rollback()
    connection.close()

@pytest.fixture
def client(app: Flask) -> FlaskClient:
    return app.test_client()
```
- Verify the session re-binding pattern against the installed Flask-SQLAlchemy version (3.1 docs, "Testing") before copying; if it differs, use `TRUNCATE ... RESTART IDENTITY CASCADE` per test instead.

## Example test

```python
def test_get_order_other_tenant_returns_404_problem(client: FlaskClient, auth_headers: Callable[..., dict[str, str]]) -> None:
    order = OrderFactory(tenant_id="tenant-a")

    resp = client.get(f"/v1/orders/{order.id}", headers=auth_headers(tenant_id="tenant-b"))

    assert resp.status_code == 404
    assert resp.mimetype == "application/problem+json"
    assert resp.json["code"] == "ORDER_NOT_FOUND"
```

## Coverage and commands

```bash
uv run pytest -q --cov=app --cov-branch --cov-report=xml --cov-report=term-missing
```
- Gate: changed-line coverage ≥ 80% in the pipeline.
- Docker unavailable → report `BLOCKED: docker unavailable`, never skip DB tests.
