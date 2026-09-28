VERDICT: APPROVED

# Review r2 — [E6.S1] Exam blueprint authoring (#80), rework

## Blocking
None.

## Round-1 finding #1 — resolved
- `api/Elmanhg.Application/ExamBlueprints/Shared/ExamBlueprintInputValidator.cs:21,23,25`: the Empty, TooLarge and TypeDuplicate predicates now skip null entries.
- `ExamBlueprintInputValidator.cs:27`: `RuleForEach(x => x.TypeCounts).NotNull().WithErrorCode(ErrorCodes.ExamBlueprintTypeCountRequired)`. A null element now yields a validation failure, which becomes 422 with a code, not 500.
- The new code `EXAM_BLUEPRINT_TYPE_COUNT_REQUIRED` is in these places:
  - `api/Elmanhg.Application/Exceptions/ErrorCodes.cs:134`
  - both resx files at line 263
  - `web/src/shared/i18n/en.json:165` and `ar.json:165`
  - `docs/exam-blueprints.md:92`, whose error-code table row (422) agrees with the code, so there is no docs divergence.
- The handler and `ExamBlueprintShapeGenerator` cannot be reached with a null element, because the validator short-circuits in `ValidationBehaviour`.

## Mutation check (run by me; file backed up and restored byte-identical, confirmed with `cmp`)
- **A: deleted line 27 (the `NotNull` rule).** All 3 new tests failed. The endpoints returned 422 `EXAM_BLUEPRINT_EMPTY` without the new code, and the validator test found no matching error.
- **B: removed the `x is not null` filters on lines 21/23/25.** All 3 new tests failed. The validator threw `NullReferenceException`, and both endpoints logged 500 `UNHANDLED_EXCEPTION`.

## Non-blocking
- `api/Elmanhg.Tests/Application/Features/ExamBlueprints/Shared/ExamBlueprintInputValidatorTests.cs:81`: the null guard in the TypeDuplicate predicate (`ExamBlueprintInputValidator.cs:25`) has no mutation of its own that a test catches. The Sum predicate on line 21 throws first for `[Mcq 1, null]`. A test that removes only the line 25 guard would need rule-level cascade isolation, so this is low value.
- The round-1 non-blocking items are unchanged, as the report declares.

## Verified
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside: Passed, 1621/1621, 0 failed (round 1: 1618, +3 new tests). I restored `appsettings.json` afterwards and git shows it unmodified.
- `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude api/core-libraries`: clean.
- `api/openapi/v1.json` diff is still +502 after the Release build, so there is no drift. The error codes are not part of the schema, so the Orval regen was rightly skipped.
- The report's claims match the code:
  - file:line references, test names, the `NullEntryBody` field, and the mutation results.
  - "Files created: None" (no new untracked files beyond round 1).
  - "Deviations: None". The dedicated code was one of the two options the round-1 review offered.
- No regression. The rework touches only the validator, error-code constants, resx, i18n, one doc row and three tests. All other ExamBlueprint tests still pass inside the full run.

## Test quality
- `Validate_NullEntry_FailsTypeCountRequired`: constrains the implementation. `ContainSingle().Which.Should().Be(...)` pins both the code and that there are no spurious errors. Mutations A and B both kill it.
- `PutSubject_NullTypeCountEntry_Returns422TypeCountRequired` and `PutUnit_NullTypeCountEntry_Returns422TypeCountRequired`: constrain the wire behaviour against real PostgreSQL (status 422 plus the code). Mutations A and B both kill them.
