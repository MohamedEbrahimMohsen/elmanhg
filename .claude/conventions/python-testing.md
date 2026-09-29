# Testing convention — python

<!-- Framework, where tests live, naming pattern (file + method), what every test must assert, what a vacuous test looks like here. The planner writes the Test plan from this; the reviewer scores tests against it. -->

## Elmanhg deltas — read first

1. No database yet, so there is no Testcontainers tier: `integration` means ASGI route tests that need no Docker.
2. HTTP doubles use `httpx2.MockTransport` / `httpx2.ASGITransport`. There is no `respx` or `asgi-lifespan`; the lifespan runs through `app.router.lifespan_context(app)`.
3. The pytest-asyncio loop scopes are `function` (`asyncio_default_fixture_loop_scope` and `asyncio_default_test_loop_scope`).
4. Run with `uv --directory ai run pytest -m "not eval"` (CI adds `--cov=elmanhg_ai --cov-branch`).

## Framework

| Concern | Tool (2026-09) | Notes |
|---|---|---|
| Runner | pytest ≥ 8.4 | `addopts = "-ra --strict-markers --strict-config"` |
| Async | pytest-asyncio 1.x, `asyncio_mode = "auto"`, `asyncio_default_fixture_loop_scope = "session"` | no `event_loop` fixture (removed in 1.0); or AnyIO plugin — one per repo |
| HTTP (FastAPI) | `httpx.AsyncClient(transport=ASGITransport(app=app), base_url="http://test")` + `asgi_lifespan.LifespanManager` | sync alternative: `with TestClient(app) as c:` |
| Database | `testcontainers[postgres]` (`PostgresContainer("postgres:17-alpine")`) | no SQLite for a Postgres service |
| Outbound HTTP | `respx` (httpx) | `pytest-socket` `--disable-socket` in the unit tier (allow unix sockets for Docker) |
| Time | `time-machine` or injected `Clock` | |
| Data | `factory_boy` / builder functions; Faker seeded | |
| Property tests | `hypothesis` | parsers, validators, money math |
| Coverage | `pytest-cov` | branch coverage |
| Speed/order | `pytest-xdist -n auto`, `pytest-randomly` | once tests are isolated |
| LLM | recorded fixtures (`tests/fixtures/`) via the `clients/model.py` fake | evals under `-m eval` |

## Location and naming

- `tests/unit/<package path>/test_<module>.py`, `tests/integration/<feature>/test_<behaviour>.py`, `tests/eval/test_eval_<pipeline>.py`.
- Shared fixtures in `tests/conftest.py` (app, client, db) and `tests/integration/conftest.py` (container, migrations); factories in `tests/factories.py`.
- Test function: `test_<unit>_<scenario>_<expected>` (`test_cancel_order_when_shipped_raises_order_already_shipped`).
- Markers declared in `pyproject.toml`: `integration`, `eval`. Unit tests carry no marker.
- One behaviour per test; Arrange / Act / Assert separated by one blank line.
- `@pytest.mark.parametrize(..., ids=[...])` with readable ids only when cases differ by data alone.

## Every test must

- Assert an observable outcome: return value, raised domain error **and** its `code`, HTTP `status_code` **and** problem+json `code`, persisted rows, or a boundary call that *is* the behaviour.
- Error responses assert `response.headers["content-type"].startswith("application/problem+json")`.
- Every Pydantic input model: one valid case + one invalid case per constraint, asserting the error `loc`.
- Every pipeline error path (timeout, malformed model output, empty retrieval) asserts the failure result/exception.
- Be deterministic: `time-machine`/fake clock, seeded `random`/Faker, recorded LLM responses, pinned temperature in evals, no `time.sleep`/`asyncio.sleep` waits.
- Be independent: fresh data per test; DB state rolled back; `app.dependency_overrides` cleared in teardown.

## Never

- Edit, weaken, skip (`@pytest.mark.skip`, `skipif` without an environment reason, `xfail` added to hide a failure), or delete an existing test to make it pass. The orchestrator's test-integrity guard blocks it. If a test is wrong: stop and write `BLOCKED: <test> — <why>` in the report.
- Assert that a mock returns what it was told to return.
- Call a live model, a real third-party API, or the network from unit or integration tests.
- Use `unittest.mock.patch` on the module under test; mock only at boundaries, always with `autospec=True` / `create_autospec`.
- `try/except` around assertions; `assert True`; asserting only `is not None`.
- Share an `AsyncSession` between tests or tasks.
- Add rerun plugins (`pytest-rerunfailures`) to hide flakiness.

## Unit vs integration boundary

| Tier | Covers | Doubles allowed | Marker |
|---|---|---|---|
| Unit | services, pipelines, schemas, scorers, pure functions | fakes for repos/clients/model client; fake clock | none |
| Integration | route → service → SQLAlchemy → real Postgres; auth deps; exception handlers; Alembic migrations | `respx` for third-party HTTP; recorded model client | `integration` |
| Eval | prompt/pipeline quality on a fixed dataset (≥ 20 cases) vs threshold | real model allowed, pinned params | `eval` (not in default CI) |

- Every new endpoint gets ≥ 3 integration tests: happy path, one failure (400/404/409), one authz (other tenant → 404/403, no token → 401).

## Fakes vs mocks

- Services receive collaborators (repo, clock, model client) as parameters/dependencies; tests pass fakes (`FakeModelClient(responses=[...])` loading recorded fixtures).
- FastAPI: override dependencies `app.dependency_overrides[get_settings] = lambda: test_settings`; clear in fixture teardown.
- `create_autospec(PaymentsClient, instance=True)` when a mock is unavoidable.

## Integration setup skeleton

```python
# tests/integration/conftest.py
@pytest.fixture(scope="session")
def pg_url() -> Iterator[str]:
    with PostgresContainer("postgres:17-alpine", driver="asyncpg") as pg:
        url = pg.get_connection_url()
        alembic_upgrade_head(url)                      # same migrations as production
        yield url

@pytest.fixture
async def db_session(pg_url: str) -> AsyncIterator[AsyncSession]:
    engine = create_async_engine(pg_url)
    async with engine.connect() as conn:
        trans = await conn.begin()
        session = AsyncSession(bind=conn, expire_on_commit=False, join_transaction_mode="create_savepoint")
        yield session
        await session.close()
        await trans.rollback()
    await engine.dispose()

@pytest.fixture
async def client(app: FastAPI, db_session: AsyncSession) -> AsyncIterator[httpx.AsyncClient]:
    app.dependency_overrides[get_session] = lambda: db_session
    async with LifespanManager(app), httpx.AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as c:
        yield c
    app.dependency_overrides.clear()
```
- Verify `PostgresContainer` keyword arguments against the installed `testcontainers` version (`python -c "from testcontainers.postgres import PostgresContainer; help(PostgresContainer)"`).

## Test data factories

```python
def an_order(**over: object) -> Order:
    defaults = {"id": uuid7(), "tenant_id": "tenant-a", "status": OrderStatus.PENDING, "total": Decimal("100.00")}
    return Order(**(defaults | over))
```
- Builders/factories in `tests/factories.py`; `factory_boy` `SQLAlchemyModelFactory` with the session injected by a fixture; `hypothesis` strategies for value objects.
- `uuid7` = `uuid.uuid7` (stdlib, Python 3.14+); on 3.13 use `uuid.uuid4()`.

## Example tests

```python
async def test_get_order_other_tenant_returns_404_problem(client: httpx.AsyncClient, db_session: AsyncSession) -> None:
    order = an_order(tenant_id="tenant-a")
    db_session.add(order)
    await db_session.flush()

    resp = await client.get(f"/v1/orders/{order.id}", headers=auth_header(tenant_id="tenant-b"))

    assert resp.status_code == 404
    assert resp.headers["content-type"].startswith("application/problem+json")
    assert resp.json()["code"] == "ORDER_NOT_FOUND"


def test_summarise_malformed_model_output_returns_rejection() -> None:
    model = FakeModelClient(responses=[load_fixture("llm/summarise/malformed.json")])

    result = summarise.run(SummariseIn(text="..."), model=model)

    assert result.status == "rejected"
    assert result.reason == "MODEL_OUTPUT_INVALID"
```

## Coverage and commands

```bash
uv run pytest -q -m "not integration and not eval"            # unit
uv run pytest -q -m integration                                # needs Docker
uv run pytest -q -m "not eval" --cov=<pkg> --cov-branch --cov-report=xml --cov-report=term-missing
uv run pytest -m eval                                          # prompt/pipeline changes only
```
- Gate: changed-line coverage ≥ 80% in the pipeline; `--cov-fail-under` only as a floor, never raised by deleting tests.

## Flakiness

- A flaky test is a bug: quarantine within 24 h with `@pytest.mark.quarantine` (declared marker, excluded from the blocking job) + issue link; fix within one sprint.
- `pytest-randomly` on in CI to surface order dependence.
- Docker unavailable → report `BLOCKED: docker unavailable`, never skip integration tests.
