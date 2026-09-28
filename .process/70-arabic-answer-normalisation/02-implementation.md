# Implementation — Arabic answer normalisation (#70)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Questions/Schemas/AnswerNormalization.cs` | 6 | A1: 8-rule `sealed record`, all default `true`, `Default` |
| `api/Elmanhg.Domain/Questions/Grading/ArabicCharacters.cs` | 54 | A3: `internal static class`, every code point as `'\uXXXX'`, `IsTashkeel`, `IsInvisibleControl`, `Map` |
| `api/Elmanhg.Infrastructure/Migrations/20260928085849_AddAnswerNormalizationRules.cs` | 40 | M1: 4 public SQL consts (no braces), Up/Down |
| `api/Elmanhg.Infrastructure/Migrations/20260928085849_AddAnswerNormalizationRules.Designer.cs` | generated | M1 designer. `AppDbContextModelSnapshot.cs` untouched |
| `web/src/features/questions/components/NormalizationRulesField.tsx` | 24 | W1: fieldset, legend, hint, 8 `CheckboxField`s in a 2-column grid from `md` |
| `api/Elmanhg.Tests/Domain/Questions/Grading/EgyptianSpellingVariantsTests.cs` | 47 | N25–N26 corpus |
| `api/Elmanhg.Tests/Integration/Persistence/AnswerNormalizationRulesMigrationTests.cs` | 77 | N37–N39 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs` | Rewritten (A2): `Normalize(string?, AnswerNormalization)`, `IsDropped`, `DropUnpairedSurrogates` (WHY comment). The `bool` overload and the constants are gone |
| `api/Elmanhg.Domain/Questions/Grading/TextGrader.cs` | `Matches` takes `AnswerNormalization`; `?? AnswerNormalization.Default`; `NumberRules`, `PlainDecimal`, `ArabicThousandsSeparator`; `TryParseNumber` per D8; extended WHY comment |
| `api/Elmanhg.Domain/Questions/Schemas/FillSchemas.cs`, `ShortSchemas.cs` | `AnswerNormalization? Normalization` replaces `UnifyLetterVariants` |
| `api/Elmanhg.Application/Questions/Shared/FillQuestionRules.cs`, `ShortQuestionRules.cs` | Canonical `Normalization ?? AnswerNormalization.Default` (the numeric branch is unchanged) |
| `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportBodies.cs` | `LetterVariants(bool)` helper; Fill and Short text use it |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | `QUESTION_IMPORT_HELP_UNIFY_LETTER_VARIANTS` text per the plan |
| Tests X1–X6 | `AnswerNormalizerTests` (+N1–N14), `TextGraderTests` (+N15–N22), `QuestionGraderTests` (+N23–N24), `FillQuestionRulesTests` (rename, +N27–N29), `ShortQuestionRulesTests` (rename, +N30–N31), `QuestionImportParserTests` (+N32–N33), `QuestionsEndpointTests` (+N34), `QuestionGradeDraftEndpointTests` (+N35–N36), `AppDbContextTests` (13th migration) |
| `web/src/features/questions/api/questionOptions.ts` | `normalizationRules`, `NormalizationRule` |
| `web/src/features/questions/schemas/questionContentSchemas.ts`, `questionEditorSchema.ts` | `normalizationSchema` / `normalization` object replaces `unifyLetterVariants` |
| `web/src/features/questions/api/questionValues.ts` | `readNormalization`; empty values, readFill, readShort and toContent use `normalization` |
| `web/src/features/questions/components/FillBlanksField.tsx`, `ShortAnswerFields.tsx` | `<NormalizationRulesField />` replaces the unify checkbox; `CheckboxField` import dropped |
| `web/src/features/questions/i18n/en.json`, `ar.json` | `editor.blanks.unify` deleted; `editor.normalization.*` added |
| `web/src/features/questions/api/questionValues.test.ts` (X7, +N40–N42), `pages/QuestionEditorPage.test.tsx` (+N43–N45) | Per the test plan |
| `docs/PRD.md` §6.2, `docs/question-schemas.md`, `docs/question-import.md`, `docs/claude-design-prompt.md` §4/§5, `docs/prototype.md` | Per the plan |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `readNormalization(stored: Partial<Record<NormalizationRule, boolean>> \| undefined)` | `tsconfig` has `exactOptionalPropertyTypes`. The zod-inferred `{ stripTashkeel?: boolean \| undefined … }` is not assignable to that type, so `tsc` fails (TS2345) | Signature is `Partial<Record<NormalizationRule, boolean \| undefined>> \| undefined`. Behaviour is unchanged |
| N43: "Save → `Question saved.`" (`findByText`) | The success toast from the earlier "saves the question" test is still in the DOM when this test runs in the same file. `findByText` then finds two matches and throws. The test passes alone | Used `findAllByText('Question saved.')` (length > 0), then `waitFor(bodies.length === 1)`, then asserted `bodies[0].gradingSpec` exactly as planned |
| A3 / TextGrader constants shown as literal characters in the plan | D12 requires `'\uXXXX'` escapes | Wrote every constant as an escape, including `'٫'`, `'٬'`, `'−'` in `TextGrader` |

## Build & test
- `dotnet build api/ -c Release` → `0 Warning(s)`, `0 Error(s)`.
- `git status --porcelain api/openapi` → empty (no drift).
- `dotnet test api/ -c Release` → `Test run summary: Passed! total: 1142 failed: 0 succeeded: 1142 skipped: 0`. There is no `appsettings.json` in this checkout, so this matches CI.
- `dotnet list api/ package --vulnerable --include-transitive` → no project has vulnerable packages.
- `dotnet tool restore` → `Restore was successful.`
- `dotnet ef migrations has-pending-model-changes … --configuration Release --no-build` → `No changes have been made to the model since the last migration.`
- `dotnet format whitespace … --verify-no-changes` on the touched projects → exit 0.
- `npm --prefix web run gen:tokens` and `gen:api` → no changes in git status.
- `typecheck` → clean. `lint` → clean. `format:check` → `All matched files use Prettier code style!`
- `test -- --run` → `Test Files 70 passed (70)`, `Tests 397 passed (397)`.
- `build` → `✓ built`.
- Docker was up (Testcontainers ran). `ai/` was not touched.

## Notes for review
- The existing `QuestionGraderTests.Grade_FillHalf_ReturnsPartial` and `Grade_ShortText_UsesTextGrader` still send `"unifyLetterVariants":true` in the spec JSON. Per D5 the key is silently ignored, so both tests stay valid. The plan does not list them for editing, and I left them unchanged.
- N34 compares the snapshot's `gradingSpec` with the whole stored `GradingSpec`, and `GradingSpec.normalization` with the expected object. That is at least as strict as the plan's wording.
- N37–N39 run the upgrade SQL across the whole shared test database. This is idempotent, and no other test writes `unifyLetterVariants`.
- The migration `.cs` file lost its UTF-8 BOM when I rewrote it. It has no effect on the build.
- Postman: no endpoint or contract route changed. Its request bodies contained no `unifyLetterVariants`, so there was nothing to update.
- While debugging N43, one isolated run logged a single MSW "unhandled GET /api/lessons/". It did not recur in later full-suite runs. It looks like the faker-generated update response, which also exists in the pre-existing "saves the question" test.
