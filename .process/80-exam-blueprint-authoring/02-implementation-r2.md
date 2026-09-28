# Implementation r2 — [E6.S1] Exam blueprint authoring (#80), rework

## Blocking findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Made the three `TypeCounts` list predicates (Empty, TooLarge, TypeDuplicate) skip null entries with `x is not null`. | `api/Elmanhg.Application/ExamBlueprints/Shared/ExamBlueprintInputValidator.cs:21,23,25` |
| 1 | Added the per-entry rule `RuleForEach(x => x.TypeCounts).NotNull().WithErrorCode(ErrorCodes.ExamBlueprintTypeCountRequired)`. | `api/Elmanhg.Application/ExamBlueprints/Shared/ExamBlueprintInputValidator.cs:27` |
| 1 | Added the new code `EXAM_BLUEPRINT_TYPE_COUNT_REQUIRED`. | `api/Elmanhg.Application/Exceptions/ErrorCodes.cs:134` |
| 1 | Added resx strings. en: "Every type count entry is required." ar (no tashkeel): "كل صف في أعداد الأنواع مطلوب." | `api/Elmanhg.Api/Resources/Messages.en.resx:263`, `Messages.ar.resx:263` |
| 1 | Added the same strings to the web `errors` dictionaries. | `web/src/shared/i18n/en.json:165`, `web/src/shared/i18n/ar.json:165` |
| 1 | Added a row to the error-code table (422, "A `null` entry in the type counts list."). | `docs/exam-blueprints.md:92` |
| 1 | Validator test `Validate_NullEntry_FailsTypeCountRequired`. Input is `[Mcq 1, null]`, and the test asserts the new code is the only error. | `api/Elmanhg.Tests/Application/Features/ExamBlueprints/Shared/ExamBlueprintInputValidatorTests.cs:79` |
| 1 | Endpoint tests `PutSubject_NullTypeCountEntry_Returns422TypeCountRequired` and `PutUnit_NullTypeCountEntry_Returns422TypeCountRequired`. Both send `{"typeCounts":[null],"passMark":50}` and assert 422 plus the new code. The body is in the new `NullEntryBody` field. | `api/Elmanhg.Tests/Integration/ExamBlueprints/ExamBlueprintsEndpointTests.cs:16,136,205` |

## Files created
None (apart from this report).

## Files modified
The files are listed in the table above. `PROGRESS.md` and `scripts/` were not touched.

## Deviations
None. I chose a dedicated code (`EXAM_BLUEPRINT_TYPE_COUNT_REQUIRED`) over reusing `EXAM_BLUEPRINT_COUNT_INVALID`, as the rework brief asked. The review offered either option.

## Mutation check
- **A: removed the `NotNull` rule.** All 3 new tests failed. The validator test found no errors. Both endpoint tests got `EXAM_BLUEPRINT_EMPTY` instead of the new code.
- **B: removed the `x is not null` filters from the list predicates.** All 3 new tests failed. The validator threw `NullReferenceException`, and the endpoints hit the original 500 through `CoreExceptionMiddleware`.
- After each mutation I restored the file from a backup and confirmed the restore with grep.

## Build & test
- `dotnet test --project api/Elmanhg.Tests -c Release --filter-class "*ExamBlueprint*"`: Passed, 83/83.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside: Passed, total 1621, failed 0. The previous run had 1618, so the count rose by 3. I restored the file afterwards.
- `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude api/core-libraries`: no output (clean).
- **OpenAPI:** the diff for `api/openapi/v1.json` is unchanged at +502. The error codes are not part of the schema, so I did not regenerate Orval.
- **Web** (the i18n JSON changed):
  - `npm run typecheck`: clean.
  - `npm run lint`: clean.
  - `npm test -- --run`: 90 files, 560 tests passed.
  - `npm run format:check -- --end-of-line auto`: "All matched files use Prettier code style!"

## Notes for review
- For a `[null]` body the response also carries `EXAM_BLUEPRINT_EMPTY`, because the non-null sum is 0. The endpoint tests assert `Contain` on the code string, which follows the existing `PutSubject_AllZero_Returns422Empty` pattern.
- For `ChildRules`, FluentValidation skips null elements, so the new `NotNull` rule is the only per-element error. The validator test's `ContainSingle` proves this.
- I left the non-blocking items unchanged.
