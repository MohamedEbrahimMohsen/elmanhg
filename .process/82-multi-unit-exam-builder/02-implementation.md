# Implementation — [E6.S3] Multi-unit exam builder (#82)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Sessions/MultiUnitExamScope.cs` | 13 | Scope record, `ToKey` (`units:{size}:{sorted ids}`), JSON round-trip |
| `api/Elmanhg.Domain/Sessions/Session.MultiUnitExam.cs` | 31 | `StartMultiUnitExam`, `GetExamUnitIds` |
| `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamSizes.cs` | 10 | `MinUnits = 2`, sizes 20/40/60 |
| `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamPart.cs` | 5 | (unit, resolved blueprint) |
| `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamUnitPlan.cs` | 8 | Per-unit allocation |
| `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamPlan.cs` | 28 | Merged plan, `TypeCounts`, `EnsureServable` |
| `api/Elmanhg.Domain/Sessions/Exams/MultiUnitBlueprintMerge.cs` | 83 | Largest-remainder merge, time limit, pass mark |
| `api/Elmanhg.Domain/Sessions/Exams/MultiUnitExamQuestionSelector.cs` | 42 | Per-unit draw plus spill-over |
| `api/Elmanhg.Domain/Sessions/Exams/ExamUnitShare.cs` | 6 | Per-unit breakdown row |
| `api/Elmanhg.Application/Exams/Shared/MultiUnitExamSelection.cs` | 3 | Shared input |
| `api/Elmanhg.Application/Exams/Shared/MultiUnitExamSelectionValidator.cs` | 23 | Shared validator |
| `api/Elmanhg.Application/Exams/Shared/MultiUnitExamUnits.cs` | 6 | Loaded subject + ordered units |
| `api/Elmanhg.Application/Exams/Shared/MultiUnitExamPlanner.cs` | 53 | `LoadUnitsAsync`, `PlanAsync` |
| `api/Elmanhg.Application/Exams/Shared/MultiUnitExamOverviewResult.cs` | 5 | Overview results |
| `api/Elmanhg.Application/Exams/Shared/MultiUnitExamOverviewResultGenerator.cs` | 28 | Overview mapping |
| `api/Elmanhg.Application/Exams/Shared/MultiUnitExamPreviewResult.cs` | 5 | Preview results |
| `api/Elmanhg.Application/Exams/Shared/MultiUnitExamPreviewResultGenerator.cs` | 23 | Preview mapping |
| `api/Elmanhg.Application/Exams/GetMultiUnitExamOverview/GetMultiUnitExamOverviewQuery.cs` | 6 | Query |
| `…/GetMultiUnitExamOverview/GetMultiUnitExamOverviewValidator.cs` | 13 | Validator |
| `…/GetMultiUnitExamOverview/GetMultiUnitExamOverviewHandler.cs` | 42 | Handler |
| `api/Elmanhg.Application/Exams/PreviewMultiUnitExam/PreviewMultiUnitExamQuery.cs` | 6 | Query |
| `…/PreviewMultiUnitExam/PreviewMultiUnitExamValidator.cs` | 16 | Validator |
| `…/PreviewMultiUnitExam/PreviewMultiUnitExamHandler.cs` | 40 | Handler |
| `api/Elmanhg.Application/Exams/StartMultiUnitExam/StartMultiUnitExamCommand.cs` | 6 | Command |
| `…/StartMultiUnitExam/StartMultiUnitExamValidator.cs` | 16 | Validator |
| `…/StartMultiUnitExam/StartMultiUnitExamHandler.cs` | 67 | Handler (start/resume/auto-submit) |
| `…/StartMultiUnitExam/MultiUnitExamDraw.cs` | 49 | Candidate load, union check, selection, `Session.StartMultiUnitExam` |
| `api/Elmanhg.Tests/Builders/MultiUnitExamBuilder.cs` | 62 | Test builder |
| `api/Elmanhg.Tests/Integration/Exams/MultiUnitExamTestData.cs` | 65 | Integration seeding helpers |
| `api/Elmanhg.Tests/Domain/Sessions/Exams/MultiUnitBlueprintMergeTests.cs` | ~140 | Tests 1–13 |
| `api/Elmanhg.Tests/Domain/Sessions/Exams/MultiUnitExamPlanTests.cs` | 33 | Tests 14–15 |
| `api/Elmanhg.Tests/Domain/Sessions/Exams/MultiUnitExamQuestionSelectorTests.cs` | ~110 | Tests 16–21 |
| `api/Elmanhg.Tests/Domain/Sessions/MultiUnitExamScopeTests.cs` | 40 | Tests 22–24 |
| `api/Elmanhg.Tests/Domain/Sessions/SessionMultiUnitExamStartTests.cs` | ~125 | Tests 25–34 |
| `api/Elmanhg.Tests/Application/Features/Exams/Shared/MultiUnitExamSelectionValidatorTests.cs` | 64 | Tests 37–43 |
| `…/StartMultiUnitExam/StartMultiUnitExamValidatorTests.cs` | 25 | Tests 44–45 |
| `…/PreviewMultiUnitExam/PreviewMultiUnitExamValidatorTests.cs` | 25 | Tests 46–47 |
| `…/GetMultiUnitExamOverview/GetMultiUnitExamOverviewValidatorTests.cs` | 22 | Tests 48–49 |
| `…/GetMultiUnitExamOverview/GetMultiUnitExamOverviewHandlerTests.cs` | ~95 | Tests 50–54 |
| `…/PreviewMultiUnitExam/PreviewMultiUnitExamHandlerTests.cs` | ~125 | Tests 55–61 |
| `…/StartMultiUnitExam/StartMultiUnitExamHandlerTests.cs` | ~180 | Tests 62–71 |
| `api/Elmanhg.Tests/Integration/Exams/MultiUnitExamEndpointTests.cs` | ~230 | Tests 74–87 |
| `web/src/features/exam/schemas/multiExamSearchSchema.ts` | 9 | URL search schema |
| `web/src/features/exam/hooks/useMultiExamSearch.ts` | 40 | URL selection state |
| `web/src/features/exam/hooks/useStartMultiExam.ts` | 41 | Start mutation |
| `web/src/features/exam/pages/MultiExamBuilderPage.tsx` | 47 | Builder page |
| `web/src/features/exam/components/MultiExamSubjectSection.tsx` | ~75 | Overview states, pickers, preview |
| `web/src/features/exam/components/MultiExamSubjectSelect.tsx` | 38 | Subject select |
| `web/src/features/exam/components/MultiExamUnitPicker.tsx` | ~50 | Unit checkboxes |
| `web/src/features/exam/components/MultiExamSizePicker.tsx` | 35 | Size radios |
| `web/src/features/exam/components/MultiExamPreview.tsx` | ~70 | Live preview + start |
| `web/src/features/exam/components/MultiExamUnitShares.tsx` | 25 | Per-unit shares list |
| `web/src/features/exam/components/ExamUnitBreakdown.tsx` | 55 | «حسب الوحدة» table |
| `web/src/features/exam/schemas/multiExamSearchSchema.test.ts` | 24 | Tests 89–92 |
| `web/src/features/exam/pages/MultiExamBuilderPage.test.tsx` | ~300 | Tests 93–106 |
| `web/src/shared/api/generated/model/*` (7 new) | gen | Orval output |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Sessions/Session.Exam.cs` | Extracted `EnsureExamQuestions`, `CreateExam`; `IsServableInUnit` → `IsServableInUnits(question, IReadOnlySet<Guid>, lessons)`; `StartUnitExam` signature/behaviour unchanged |
| `api/Elmanhg.Domain/ExamBlueprints/ExamBlueprintShortfall.cs` | Public `Describe` |
| `api/Elmanhg.Domain/ExamBlueprints/ExamBlueprint.cs` | Private `Describe` removed; calls the shared one |
| `api/Elmanhg.Domain/Sessions/Exams/ExamShare.cs` | Static `Percent`; `ScorePercent` uses it |
| `api/Elmanhg.Domain/Sessions/Exams/ExamBreakdown.cs` | `ByUnit` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | 4 codes |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `.en.resx` | 4 strings each |
| `api/Elmanhg.Application/Exams/Shared/ExamSessionResult.cs` | `SubjectId`, `UnitBreakdown` |
| `api/Elmanhg.Application/Exams/Shared/ExamBreakdownResults.cs` | `ExamUnitBreakdownResult` |
| `api/Elmanhg.Application/Exams/Shared/ExamBreakdownResultGenerator.cs` | `Units(...)` |
| `api/Elmanhg.Application/Exams/Shared/ExamSessionResultGenerator.cs` | New parameters passed through |
| `api/Elmanhg.Application/Exams/Shared/ExamSessionResultLoader.cs` | Both exam kinds, unit loop, subject id, unit breakdown (70 lines) |
| `api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryHandler.cs` | Unit ids via `GetExamUnitIds` for every exam |
| `api/Elmanhg.Application/Progress/Shared/SessionHistoryResultGenerator.cs` | `MultiUnitExam => ForUnits` («A + B») |
| `api/Elmanhg.Api/Controllers/Exams/ExamsController.cs` | 3 actions, `AssessmentsTake` |
| `api/Elmanhg.Api/Controllers/Exams/Requests.cs` | `StartMultiUnitExamRequest` |
| `api/openapi/v1.json` | Regenerated by `dotnet build` |
| `api/Elmanhg.Tests/Domain/Sessions/Exams/ExamBreakdownTests.cs` | +2 tests (35–36) and a private helper |
| `api/Elmanhg.Tests/Application/Features/Exams/GetExamSession/GetExamSessionHandlerTests.cs` | +1 test (72), 2 usings |
| `api/Elmanhg.Tests/Application/Features/Progress/GetSessionHistory/GetSessionHistoryHandlerTests.cs` | +1 test (73) |
| `api/Elmanhg.Tests/Integration/Progress/SessionHistoryEndpointTests.cs` | +1 test (88), 1 using |
| `postman/elmanhg.postman_collection.json` | `secondUnitId` variable; 4 requests after "Submit exam"; folder description |
| `web/src/routes/student/multi-exam.tsx` | `validateSearch` + `MultiExamBuilderPage` |
| `web/src/features/exam/index.ts` | New exports |
| `web/src/features/exam/api/examSession.ts` | `multiUnitExamKind`, `isMultiUnitExam`, `examUnitNames` |
| `web/src/features/exam/api/invalidateExamViews.ts` | `examSubjectQueryPrefix` in the predicate |
| `web/src/features/exam/components/ExamRunner.tsx` | Multi-unit title |
| `web/src/features/exam/pages/ExamResultPage.tsx` | Multi title, `ExamUnitBreakdown`, multi retake link |
| `web/src/features/exam/i18n/ar.json`, `en.json` | Keys from Files #45 |
| `web/src/shared/i18n/ar.json`, `en.json` | 4 error codes |
| `web/src/test/examFixtures.ts` | `subjectId`, `unitBreakdown` on `openExam`; `examSecondUnitId`, `multiOverview`, `multiPreview`, `multiExam` |
| `web/src/features/exam/api/examSession.test.ts`, `api/invalidateExamViews.test.ts`, `pages/ExamPage.test.tsx`, `pages/ExamResultPage.test.tsx` | Added tests only (107–111) |
| `web/src/shared/api/generated/**` | Regenerated (`npm --prefix web run gen:api`) |
| `docs/exams.md`, `exam-blueprints.md`, `sessions.md`, `progress.md`, `PRD.md` §7.5, `claude-design-prompt.md`, `prototype.md` | As in the plan's Docs table |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `ExamItemPlacements.cs` (#27), only if the loader passes ~100 lines | The loader is 70 lines | Not created; `Place`/`Placement` stay in the loader |
| `web/src/routeTree.gen.ts` regenerated | The route path already existed; `validateSearch`/component changes do not alter the generated tree | Build ran; the file has no diff |
| `StartUnitExam` passes `new HashSet<Guid> { unit.Id }` | Collection-initialiser rule (`[]`) | `HashSet<Guid> unitIds = [unit.Id];` hoisted into a local; same behaviour |
| Test 7 asserts the worked example → 47 | 47 is exact, so the mutation `Ceiling → Floor` survived | Same test method also asserts a fractional case (25 min unit → 43.5 → 44) |
| `examFixtures.ts` hard-codes `subjectId: '9090…'` | Needed the id in tests too | Added an `examSubjectId` export and used it in `openExam` and `overview` (same value) |

## Build & test
- `dotnet build` (api/): `Build succeeded`, 0 warnings outside the vendored `core-libraries`.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (then restored): `total: 1879, failed: 0, succeeded: 1879, skipped: 0`. Docker 29.6.2 up.
- `npm --prefix web run gen:api`: regenerated; `getPreviewMultiUnitExamUrl` appends each `unitIds` value (`explodeParameters: ["unitIds"]`).
- `npm --prefix web run typecheck`: clean. `npm --prefix web run lint`: clean. `npm --prefix web run build`: `✓ built`.
- `npm --prefix web test -- --run`: `Test Files 100 passed (100), Tests 632 passed (632)`; also green under `vitest run --coverage`.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` (in `web/`): `All matched files use Prettier code style!`
- New tests: 88 api test methods (plan #1–88; two Theories expand to 3 and 4 cases) and 23 web tests (plan #89–111).
- Mutation checks: 17 API mutations (merge tie-break, rounding, contributing filter, spill-over, per-unit mix, `ByUnit` order, key sort, min units, duplicate rule, unit/subject check, servable sum, union filter, key conflict, deadline submit, loader breakdown, history join) and 20 web mutations. All killed; the one survivor (Ceiling) was fixed as above.

## Notes for review
- `MultiUnitExamSizes.All` (20/40/60) and `MinUnits` are domain constants with a WHY comment, as the plan prescribes (PRD §7.5 fixes them; no new config keys). If the reviewer reads §8.1 as covering these, they would move to options.
- `MultiUnitExamPlanner.PlanAsync` uses `x.Blueprint ?? throw new InvalidOperationException(...)` after the missing-blueprint check, to avoid a null-forgiving `!`.
- The unit picker puts the caption («(12 سؤال متاح)» / «لا يوجد امتحان لهذه الوحدة») outside the `<label>`, linked by `aria-describedby`. That keeps the checkbox name to the unit name and satisfies `jsx-a11y/label-has-associated-control`.
- Lazy-route flake (#179): every builder test awaits `router.loadRouteChunk` for `/student/multi-exam` and `/student/exam/$sessionId` before asserting. The retake test preloads the builder chunk before clicking. No findBy timeout was raised.
- `ExamSessionResult` shape changed (`subjectId`, `unitBreakdown`). The #81 handler tests stayed green unedited.
- Postman "Submit multi-unit exam" reuses `examSessionId` set by "Start multi-unit exam". Run it after the unit-exam "Submit exam", because one exam can be open at a time.
