VERDICT: CHANGES_REQUESTED

# Review — [E6.S1] Exam blueprint authoring (#80)

## Blocking

### 1. A `null` entry in `typeCounts` crashes the validator and returns 500
**Where:** `api/Elmanhg.Application/ExamBlueprints/Shared/ExamBlueprintInputValidator.cs:21`, `:23`, `:25` (and `ExamBlueprintShapeGenerator.cs:10` behind them)
**Rule:** Correctness. Client input must produce 4xx, not 5xx (skill §8.7: an expected 4xx is a result, not an application fault). Testing convention: every validator rule needs a failing case.
**Problem:** The three `Must` predicates on `TypeCounts` call `x.Count` and `x.Type` on every element without a null check. `System.Text.Json` binds `[null]` into `List<ExamTypeCountInput>` as a null element. The MVC implicit required check does not cover collection elements. So the first `Must` throws `NullReferenceException` inside the `ValidationBehaviour`. `CoreExceptionMiddleware` maps that to 500 `UNHANDLED_EXCEPTION` and logs it at Error with a stack trace. The implementer flagged this gap in `02-implementation.md` "Notes for review" and left it unfixed. The gap comes from the plan rule text, but the behaviour is still wrong on a new public endpoint.
**Failure:** An admin sends `PUT /api/exam-blueprints/subjects/{id}` (or `/units/{id}`) with `{"typeCounts":[null],"passMark":50}`, or `[{"type":"Mcq","count":1},null]`. The response is 500 instead of 422.
**Fix:**
- Make the three list predicates skip null entries, e.g. `list.Where(x => x is not null)`.
- Add a per-element null rule, e.g. `RuleForEach(x => x.TypeCounts).NotNull().WithErrorCode(ErrorCodes.ExamBlueprintCountInvalid)` or a dedicated code, with resx and web strings if it is new.
- Add a validator test such as `Validate_NullEntry_FailsCountInvalid`, plus an endpoint test asserting 422.

## Non-blocking
- `web/src/features/blueprints/components/TypeCountsTable.tsx:89`: the per-row error is built as `{t(type)}: {t(message)}` with a literal colon separator. One interpolated key (e.g. `editor.rowError` = `{type}: {message}`) would follow skill §6.17. There is no visible failure in either locale today.
- `api/Elmanhg.Domain/ExamBlueprints/ExamBlueprintJson.cs:17,21`: the defensive `InvalidOperationException` throws (JSON literal `null`) have no test. The rows are always written canonically, so these cannot be reached through the API.
- `docs/exam-blueprints.md:89`: says `EXAM_BLUEPRINT_EMPTY` covers "No body". An empty body on `[FromBody]` is most likely rejected by model binding (400) before the validator runs. `Validate_NullBlueprint_FailsEmpty` covers the validator only, not the wire. Worth checking and rewording.
- `PROGRESS.md:33-34`: a blank line was added between row 20 and row 21 of "Finished stories", which splits the markdown table so row 21 renders outside it. This is bookkeeping, not story code.
- The local `api/Elmanhg.Api/appsettings.json` has no `ExamBlueprints` section. The code defaults (100/300) satisfy `ValidateOnStart`, as the report says.

## Verified
- **Build and tests (run by me):**
  - `dotnet test api/ -c Release` with `appsettings.json` moved aside, then restored: Passed, 1618/1618, 0 failed.
  - `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude api/core-libraries`: no diagnostics.
  - The Release build regenerated `api/openapi/v1.json` (mtime updated) and the diff is unchanged (+502), so there is no OpenAPI drift.
  - Web: `typecheck` clean, `lint` clean (`--max-warnings=0`), `test -- --run` 90 files / 560 tests passed, `prettier --check ... --end-of-line auto` clean.
- **Domain:** D1-D9 exist with the planned signatures.
  - `ExamBlueprint` follows "Domain behaviour" exactly: guard, then mutate, then stamp; `Delete` refuses the default; `Apply` stores the canonical jsonb.
  - `ExamBlueprintShortfall.Find` is pure and ordered by type.
  - `ServableQuestionCount` is in `Elmanhg.Domain.Questions`.
- **Application:** A1-A20 exist and match the plan.
  - Handler steps and order match. The user guard comes first. `SaveChangesAsync` is called once. `ConfigureAwait(false)` is on every await.
  - Audit actions and resource types match Decision 20. `ExamBlueprintResult` implements `IAuditableResult`.
  - Options: `[Range]` attributes, `ValidateOnStart`, and defaults of 100/300. The values are also in `appsettings.example.json` and `ApiFactory`.
- **Infrastructure:**
  - `CountServableByUnitAndTypeAsync` uses `WhereServable` in one grouped query.
  - `ConfigureExamBlueprints` has jsonb columns, two Restrict FKs, two partial unique indexes, and the global soft-delete filter.
  - A 409 catch covers both index names.
  - The migration creates only the table, its FKs and its indexes (no Drop or Rename). `AppDbContextTests` lists the migration as the 17th.
  - The repository is registered in DI.
- **API:** the controller has 4 actions, routes as in Decision 13, `[Authorize(Policy = DefaultCodes.BlueprintsManage)]` on each, and passes the cancellation token.
- **Error codes:** all 12 codes are in both resx files and both web `errors` dictionaries. The Arabic resx has no tashkeel (declared deviation, confirmed).
- **Postman:** folder `ExamBlueprints` sits between `Progress` and `AuditLogs`. It has 6 requests in the planned order with correct methods, URLs and bodies, inherits bearer auth, and sets the variables `blueprintSubjectId` and `blueprintUnitId`. The create-subject and create-unit bodies match `CreateSubjectCommand(Name)` and `CreateUnitCommand(SubjectId, Name)`.
- **Docs:**
  - `docs/exam-blueprints.md` exists with every planned section.
  - PRD §10.2 has the appended paragraph. PRD §15 drops the blueprint ids from Subject/Unit and the new ExamBlueprint row matches the entity.
  - `docs/audit-log.md` has 3 rows and lists the entity.
  - `docs/claude-design-prompt.md` §4 bullet is replaced and matches the UI as built.
  - No divergence found.
- **Web:** W1-W20 exist.
  - The route uses `validateSearch`, and the `blueprints` namespace is registered.
  - Only tokens and logical properties are used. `w-24` and `size-4.5` resolve through `--spacing: var(--ds-space-1)`.
  - Files are within the limits: the largest component is 106 lines.
  - Loading, empty and error+retry states are present, plus an `ar` RTL test and an axe test.
- **Declared deviations are real and harmless:** the non-null-asserted `DifficultyMix` child rules, the `PercentMax` WHY comment, `percentMax` in the schema, `serverErrorFields` as a constant, `useWatch` instead of `watch()`, and `SeedServableMcqAsync` returning `Guid`. No undeclared extra files.
- **Test plan:** every row 1-103 exists under the planned name. The only existing test edited is #73.

## Test quality
- `ExamBlueprintTests`, `ExamBlueprintShortfallTests`: constrain the code. They check the exact canonical JSON, the context string `"Mcq 1/2"`, that state is unchanged after a failed update, and the ordering.
- `ExamBlueprintInputValidatorTests`: constrain every planned rule with a failing case. They are missing the null-element case (Blocking #1).
- `Save*HandlerTests`, `DeleteExamBlueprintHandlerTests`: real entities, with no assertions that only echo a mock.
  - Success paths assert `Received(1)` on save. Throwing paths assert the exception type, the code and `DidNotReceive()`.
  - `Handle_CountsAcrossUnits_UsesSubjectPool` and `Handle_OtherUnitsQuestions_DoNotCount` separate the subject pool from the unit pool.
- `GetSubjectExamBlueprintsHandlerTests`: constrain the overview mapping (zero-filled types, subject sum, orphan ignored).
- `ExamBlueprintsEndpointTests`, `ExamBlueprintPersistenceTests`: real PostgreSQL. They cover the servable filter (a pending question and a Draft lesson are excluded), upsert identity, audit rows, 401/403/404/400/422, and both unique indexes returning 409.
- Web tests constrain behaviour through the DOM:
  - The PUT body is captured in MSW.
  - The refetch after an error is proven by a changed available count.
  - Delete plus refetch is driven by handler state.
  - No vacuous tests found.
