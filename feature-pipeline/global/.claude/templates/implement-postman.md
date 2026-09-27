You are syncing the Postman collection with the API changes of one approved plan, stack `{stack}`. You touch `{collection}` and `{environment}` only. No application code.

## Read first
1. `{plan}` — the `## Stack: {stack}` section: *Surface* (routes) and the *Postman* table. The Postman table is your exact spec.
2. `{collection}` — the existing collection. Match its folder layout, naming, auth setup, and variable names (`{{baseUrl}}`, tokens). Open two existing requests and copy their shape.
3. `{environment}` — variables available. Add a variable only if a request in the plan needs one that does not exist; name it like the others.
4. The controllers/routes under `{root}` that the plan touched, to confirm the real method, path, and body shape. The code is the truth if the plan and code differ; record that under Deviations.

## Do
- One request per row of the Postman table, in the folder the row names. Request name = `<Endpoint> · <Scenario>` (e.g. `POST /refunds · already refunded → 409 ORDER_ALREADY_REFUNDED`).
- Body/params from the row's *Input*. Auth like sibling requests.
- Rows with `Action: update request` → edit the existing request in place, keep its id. Rows with `Action: delete request` → remove it. Never leave a request pointing at an endpoint that no longer exists.
- Postman tests are `{postman_tests}`.
  - `on`: every request gets a `pm.test` block asserting the expected status and, for error rows, the expected error code from the response body (use the same JSON path the existing tests use). Happy rows also assert the shape the plan's result type promises (fields present). An expected error is a passing test, not a failure.
  - `off`: no test scripts. Existing scripts on requests you update stay as they are unless the assertion no longer matches the contract; then update it.
- Keep the JSON valid and the collection importable. Do not reformat unrelated parts of the file.
  → 2026-09 update: first detect the format (see "Collection format"). v3 Native Git collections are YAML files, one per request; keep each YAML file valid and edit only the request files the plan rows name.

## Collection format (decide before editing)
| `{collection}` is | Format | Lint | Run |
|---|---|---|---|
| a `*.postman_collection.json` file | v2.1 JSON | `python -m json.tool {collection} > /dev/null` | `newman run {collection} -e {environment}` or `postman collection run {collection} -e {environment}` |
| a folder under `postman/collections/` with `*.request.yaml` files (Postman v12 Native Git) | v3 | `postman collection lint` | `postman collection run <path> -e <env>` only — Newman cannot run v3 |
- Never convert between formats. Match what exists.
- `postman collection …` flags: verify with `postman collection run --help` before using one not shown here.

## Structure
- One collection per API. Folders mirror resources/controllers (`Refunds/`), negatives in `<Resource>/Negative/`, multi-step journeys in `Workflows/`, cleanup in `Teardown/`.
- Request name per the rule above; description = purpose + preconditions (which variable must be set).
- Auth at collection level (`Bearer {{accessToken}}`) like the existing collection; per-request override only for auth scenarios (`noauth` for 401).
- Shared helpers (schema assert, token fetch) live in the collection-level script, not copied per request.

## Variables
- Environment (`{environment}`): `baseUrl`, credentials, tenant — placeholders only (`<set-in-ci>`), never real secrets. CI passes real values with `--env-var`.
- Values produced by the run → collection variables: `pm.collectionVariables.set("refundId", body.id)`. Never `pm.globals`. Never a hard-coded host.
- Chain only after the success assertion, guarded: `if (pm.response.code === 201) pm.collectionVariables.set("refundId", body.id);` — a failure must not cascade into confusing downstream errors.
- Unique data per run: `{{$guid}}`, `{{$timestamp}}`. Create what you need; delete it in `Teardown/`.
- One name per variable across scopes; no duplicate names in environment and collection.

## Assertions (when `{postman_tests}` is `on`)
Every request gets all four:
```js
pm.test("POST /refunds · valid → 201", () => pm.response.to.have.status(201));
pm.test("JSON content type", () => pm.expect(pm.response.headers.get("Content-Type")).to.include("json"));
pm.test("body matches schema", () => pm.response.to.have.jsonSchema(schema)); // required fields + types from the plan's result type
pm.test("amount echoes request", () => pm.expect(pm.response.json().amount).to.eql(JSON.parse(pm.request.body.raw).amount));
```
- Status, content type, schema (`required` + types), and ≥ 1 business assertion (a value tied to the input or a state change). Status-only is not enough.
- Error rows: schema = the repo's error shape (ProblemDetails `type/title/status` + error-code field, same JSON path existing tests use); assert the error code; assert the body has no stack trace (`pm.expect(pm.response.text()).to.not.include("   at ")`).
- Assert semantics: an error row passes when the API returns exactly the expected status and error code. A 2xx on an error row is a FAIL. A different error code is a FAIL.
- Test names stable and descriptive; they become JUnit case names.
- No `console.log` of tokens or bodies with PII.

## Negative set (per endpoint in the plan's Surface)
Write the plan's error rows. If the plan's Postman table lacks any of these for an endpoint, do not invent them; list the gap under `## Gaps`:
| Status | Scenario |
|---|---|
| 400 | malformed body / wrong type |
| 401 | missing or invalid token (`noauth`) |
| 403 | wrong role |
| 404 | unknown id, or another tenant's id (per plan) |
| 409 | duplicate / state conflict |
| 422 | business validation (per repo convention; some repos use 400) |

## Rules
- Touch `{collection}` and `{environment}` only. Test integrity: never delete or weaken an existing request's assertions unless a plan row says `update request` / `delete request`.
- Unclear row (no expected status, unknown route) → `BLOCKED: <row> — <why>` in the report; do not guess.
- Never paste a real token, password, or key into any file.

## Verify
Validate JSON (`python -m json.tool {collection} > /dev/null` or equivalent). If `newman` is on PATH and the environment points at a reachable server, you may run it; paste the summary. If not, say so.
  → 2026-09 update: use the Lint/Run commands from "Collection format". v3 → `postman collection lint`, never Newman. Newman exits non-zero on failed assertions unless `--suppress-exit-code`; never pass that flag. The orchestrator runs `tests.postman.run` itself when it is set.

## Report
Write `{report}` and stop.

```markdown
# Postman — <title> · stack: {stack}

## Requests
| Folder | Request | Action (added / updated / deleted) | Plan row |

## Tests
Postman tests: on/off. Assertions written: N. Scenarios covered: happy N · error codes N.

## Deviations
| Plan said | Reality | What I did |
`None.` if none.

## Validation
JSON valid: yes/no. newman: <summary or not run>.

## Status
DONE | PARTIAL | BLOCKED

## Format
v2.1 JSON | v3 Native Git — evidence (path).

## Variables
| Name | Scope (environment / collection) | Set by | Secret placeholder: yes/no |

## Commands run (proof)
| # | Command (exact) | Exit code | Last lines of output |

## Gaps
Endpoints in Surface without a 400/401/403/404/409/422 row in the plan, and why. `None.` if none.

BLOCKED: <reason + evidence>   ← last line, only when Status is BLOCKED
```
