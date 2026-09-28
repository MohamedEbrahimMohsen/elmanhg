VERDICT: APPROVED

# Review — Graders for mcq, multi-select, true/false (#71), round 1

## Blocking
None.

## Non-blocking
- `api/core-libraries/Core.Localization/Localizer.cs:8` — the root cause of the implementer's deviation is here: `factory.Create("Messages", Assembly.GetEntryAssembly()!.GetName().Name!)`. It is correct in production (the API is hosted as `Elmanhg.Api`, so the location resolves to `Elmanhg.Api.Resources.Messages`), but it is fragile. It gives the wrong assembly under any other host, and it throws a NullReferenceException when there is no managed entry assembly. The cleaner long-term fix is an explicit resource assembly (for example `AddCoreLocalization(typeof(Program).Assembly)`). That fix changes vendored Core, which is out of this story's scope ("mirror, don't modernize"). Suggest a `deferred` follow-up issue. The test seam is acceptable for now; see Verified.
- `docs/question-schemas.md:222` — "Arabic by default" holds only when `CoreLocalization:DefaultLanguage` is set (`appsettings.example.json:5` sets it to `ar`). `api/core-libraries/Core.Localization/DependencyInjection.cs:13` falls back to `en` when it is unset. This behaviour existed before this story, the documented configuration agrees with the doc, and the web always sends `Accept-Language`. It is worth making `ar` the in-code default later, in line with the #58 "default in code" lesson.
- `docs/question-schemas.md:224` — "A Multi answer that is not exactly the correct set returns [the tally]" technically includes an empty selection, which returns Unanswered (`ChoiceGrader.cs:37-40`). The bullet order implies precedence, but a clause such as "an answered Multi answer…" would remove the ambiguity.
- `api/Elmanhg.Tests/Domain/Questions/Grading/ChoiceGraderTests.cs` is 196 lines. That is over the ~100-line guide, but it comes from the 20 required tests plus 9 existing ones, and other test files are also over 100 lines.
- `web/src/features/questions/pages/QuestionEditorPage.test.tsx:212` — no test asserts that the feedback line is absent when `feedback` is null. The plan did not require one; it would be worth adding later.

## Verified
- **Deviation (ApiResourceStringLocalizerFactory):**
  - It is declared, test-only, and does not touch production code. The production registration `api/core-libraries/Core.Localization/DependencyInjection.cs:41` (`AddScoped<ILocalizer, Localizer>`) is unchanged.
  - The override (`api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs:114-115`) uses the real `Localizer` class with the same base name ("Messages") and the same `ResourcesPath`. It redirects only `location` to `typeof(Program).Assembly` (`ApiResourceStringLocalizerFactory.cs:8,17`), which reproduces exactly what production resolves. It keeps the scoped lifetime, the same as the production `ILocalizer` and its consumer `ErrorResponseHandler` (`Core.Exceptions/DependencyInjection.cs:9`).
- **Nothing is masked:**
  - Before the override, every integration-test `GetMessage` missed its resource and returned the fallback: the exception message for errors, or "" for feedback.
  - No integration test asserts a `message` field; grep over `Elmanhg.Tests/Integration` for `"message"` and `Message.Should` returns nothing. Error-message tests were therefore never checking localised text, and they still pass (1192/1192) now that real resx text flows.
  - The override makes the integration host more production-like, not less.
- **Formula docs against code, for every plan edge case** (`docs/question-schemas.md:208-214` against `ChoiceGrader.cs:28-52`):
  - distinct ids (HashSet)
  - unknown id counts as wrong (`selected.Count - right`)
  - null ids are ignored (`Where(x => x is not null)`)
  - empty or only-null selection is Unanswered with 0, in both modes
  - ordinal, case-sensitive comparison
  - `total = |correct|`
  - all-or-nothing when `partialCredit` is absent (MultiGradingSpec default false; QG1)
  - all correct plus one wrong scores 2/3 (CG13, QG2)
  - equal right and wrong scores 0 (CG17)
  - rounding is 4 dp normalised and 2 dp score, away from zero (`QuestionGrade.cs:17`; QG2 gives 0.67/0.6667)

  All agree. The PRD §6 row and the feedback sentence are present (`docs/PRD.md:153,158`), and so is the design-prompt §4 line (`docs/claude-design-prompt.md:152`).
- **Plan files:**
  - Files to create F1–F6 exist with the specified contracts.
  - The only extra files are the declared test seam and the regenerated zod file, which falls under `generated/**`.
  - D1 (no interface), D3, D6, D8 (no default for `Feedback`) and D9 (`GradeResultPanel.tsx:48`, tokens `text-caption` and `text-text-muted`) are honoured.
  - The resx texts match the plan exactly in both languages, placeholders intact.
- **D7:** the 9 existing ChoiceGrader assertions switched to `.Value` with unchanged numbers. The QuestionGrader and handler D7 edits only add fields.
- **Tests:** CG1–CG20, QG1–QG6, GT1–GT4, GH1, DG1–DG3 and WE1 all exist under their plan names.
- **Postman:** the grade-draft request exists (`postman/elmanhg.postman_collection.json:1090`). The request contract is unchanged and there are no saved responses, so no change is needed.
- **CI checks I re-ran myself (appsettings.json absent, CI parity):**
  - `dotnet build -c Release --no-incremental`: 0 errors. All 9 warnings are in core-libraries.
  - OpenAPI: v1.json is unchanged by the rebuild (sha1 identical); its only diff is the intended `feedback` field.
  - `dotnet test`: 1192/1192 passed.
  - Vulnerable packages: none.
  - `has-pending-model-changes`: no changes.
  - web `gen:api`: no drift beyond the 3 intended files.
  - typecheck, lint (`--max-warnings=0`) and format:check: clean.
  - `test --run`: 70 files, 398/398 passed.
  - build: OK.

## Test quality
- **ChoiceGraderTests:** strong. Every new test asserts the whole `NormalisedGrade` record, so both the value and the exact tally are pinned. A wrong count, feedback where there should be none, or a swapped mode would fail.
- **QuestionGraderTests:** strong. QG1–QG3 assert the full `QuestionGrade` record, including the rounding and pass-through of feedback. QG4–QG6 assert the exception type and the exact message for all three #151 throws.
- **GradeFeedbackTextTests:** constraining. The default substitute returns "", so a wrong key (GT2) or a wrong or missing context value (GT3 matches right=2, wrong=1, total=3) yields "" and fails. GT1 asserts that the localizer is never called.
- **GradeQuestionDraftHandlerTests.GH1:** it fails if the handler drops or misroutes feedback. The context contents are pinned by GT3, not here.
- **QuestionGradeDraftEndpointTests DG1–DG3:** real end-to-end localisation in both languages against the actual resx, plus the null-on-the-wire case. They constrain the result.
- **WE1:** it asserts that the server feedback text is rendered in the preview. It does not cover the null branch (see Non-blocking).
