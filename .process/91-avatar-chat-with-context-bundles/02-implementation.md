# Implementation — [E8.S3] Avatar chat with context bundles (#91)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Avatar/AvatarEntryPoint.cs | 3 | A1 enum |
| api/Elmanhg.Domain/Avatar/AvatarMessageUsage.cs | 22 | A2 quota counter entity (`Record` factory, no mutators) |
| api/Elmanhg.Domain/Avatar/IAvatarMessageUsageRepository.cs | 8 | A3 `CountOnDayAsync` |
| api/Elmanhg.Domain/Sessions/InProgressExamSpecification.cs | 13 | A4 single definition of "exam in progress" |
| api/Elmanhg.Application/Shared/Options/AvatarOptions.cs | 22 | B1 options (validated on start, even-history rule) |
| api/Elmanhg.Application/Shared/AiService/AiChatSource.cs | 3 | B2 |
| api/Elmanhg.Application/Avatar/Shared/AvatarTurnRole.cs, AvatarTurn.cs, AvatarCitationResult.cs, AvatarReplyResult.cs, AvatarStatusResult.cs, AvatarContext.cs | 3–5 each | B3–B8 records |
| api/Elmanhg.Application/Avatar/Shared/AvatarGate.cs | 44 | B9 exam guard + quota count/ensure |
| api/Elmanhg.Application/Avatar/Shared/AvatarText.cs | 20 | B10 plain text / truncate / retrieval query |
| api/Elmanhg.Application/Avatar/Shared/AvatarAnswerText.cs | 75 | B11 student answer as text |
| api/Elmanhg.Application/Avatar/Shared/AvatarAnswerText.Correct.cs | 43 | B12 correct answer as text |
| api/Elmanhg.Application/Avatar/Shared/AvatarContextBundleFactory.cs | 45 | B13 Global / Lesson / Question bundles |
| api/Elmanhg.Application/Avatar/Shared/AvatarSourceFactory.cs | 29 | B14 match → `AiChatSource` |
| api/Elmanhg.Application/Avatar/Shared/AvatarCitationMapper.cs | 21 | B15 citations → `AvatarCitationResult` |
| api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageCommand.cs | 7 | B16 |
| api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageValidator.cs | 55 | B17 |
| api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.cs | 53 | B18 |
| api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.Context.cs | 90 | B19 context loaders |
| api/Elmanhg.Application/Avatar/GetAvatarStatus/GetAvatarStatusQuery.cs, GetAvatarStatusHandler.cs | 6 / 33 | B20–B21 |
| api/Elmanhg.Infrastructure/Avatar/AvatarMessageUsageRepository.cs | 25 | C1 parameterised `SqlQuery` count |
| api/Elmanhg.Infrastructure/Migrations/20260929143733_AddAvatarMessageUsages.cs (+ .Designer.cs) | generated | C2 CreateTable + index only |
| api/Elmanhg.Api/Controllers/Avatar/AvatarController.cs | 33 | C3 two actions, `Avatar.Chat` |
| api/Elmanhg.Tests/Domain/Avatar/AvatarMessageUsageTests.cs | 21 | D1 |
| api/Elmanhg.Tests/Domain/Sessions/InProgressExamSpecificationTests.cs | 71 | D2 |
| api/Elmanhg.Tests/Application/Features/Avatar/AvatarTestData.cs | 101 | D3 (+ nested `SendHarness`, see Deviations) |
| api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageValidatorTests.cs, SendAvatarMessageHandlerTests.cs, SendAvatarMessageHandlerContextTests.cs | — | tests 9–41 |
| api/Elmanhg.Tests/Application/Features/Avatar/GetAvatarStatus/GetAvatarStatusHandlerTests.cs | — | tests 42–46 |
| api/Elmanhg.Tests/Application/Features/Avatar/Shared/AvatarAnswerTextTests.cs, AvatarContextBundleFactoryTests.cs, AvatarSourceFactoryTests.cs, AvatarCitationMapperTests.cs | — | tests 47–65 |
| api/Elmanhg.Tests/Integration/Avatar/AvatarTestData.cs | 36 | D13 |
| api/Elmanhg.Tests/Integration/Avatar/AvatarMessageEndpointTests.cs, AvatarStatusEndpointTests.cs | — | tests 71–86 |
| api/Elmanhg.Tests/Integration/Persistence/AvatarMessageUsagePersistenceTests.cs | — | test 87 |
| ai/src/elmanhg_ai/prompts/avatar_system.v2.md, avatar_turn.v2.md | — | E1–E2 prompt v2 (exact text from the plan; turn identical to v1) |
| ai/src/elmanhg_ai/eval/__init__.py, scorers.py, avatar_chat.py | 0 / 43 / 112 | E3–E5 eval harness |
| ai/src/elmanhg_ai/eval/datasets/avatar_chat.v2.jsonl | 22 lines | E6, 7 `safety` cases (generated from the plan's fixture tables) |
| ai/tests/fixtures/anthropic/message_with_citations.json | 1 | E7 |
| ai/tests/unit/test_eval_scorers.py, test_avatar_chat_eval.py, ai/tests/eval/test_eval_avatar_chat.py | — | E8–E10 |
| web/src/features/avatar/index.ts, locales.ts, i18n/ar.json, i18n/en.json | — | F1–F4 |
| web/src/features/avatar/api/avatarContext.ts, avatarErrors.ts, citationLink.ts | — | F5–F7 |
| web/src/features/avatar/hooks/avatarReducer.ts, avatarControllerContext.ts, useAvatar.ts, useAvatarChat.ts | — | F8–F11 |
| web/src/features/avatar/schemas/avatarMessageSchema.ts | — | F12 |
| web/src/features/avatar/components/AvatarProvider.tsx, AvatarDock.tsx, AvatarPanel.tsx, AvatarConversation.tsx, AvatarMessageBubble.tsx, AvatarCitations.tsx, AvatarNotice.tsx, AvatarComposer.tsx, AskAvatarButton.tsx | ≤ 75 each | F13–F21 |
| web/src/features/avatar/components/AvatarPanel.test.tsx + api/*.test.ts, hooks/avatarReducer.test.ts, schemas/avatarMessageSchema.test.ts | — | F22–F27 |
| web/src/test/avatarFixtures.ts | — | F28 |
| docs/avatar.md | — | G1 |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/SharedKernel/DefaultCodes.cs | `AvatarChat = "Avatar.Chat"` |
| api/Elmanhg.Api/Authorization/PermissionMatrixPolicies.cs | `Avatar.Chat` → Student |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs, api/Elmanhg.Api/Resources/Messages.{ar,en}.resx | 8 `AVATAR_*` codes with the plan's texts |
| api/Elmanhg.Application/Shared/AiService/AiChatRequest.cs, AiChatReply.cs | `Sources` / `Citations` |
| api/Elmanhg.Infrastructure/AiService/FakeAiServiceClient.cs, HttpAiServiceClient.cs | fake cites first source; HTTP maps missing `citations` to `[]` |
| api/Elmanhg.Application/DependencyInjection.cs, api/Elmanhg.Infrastructure/DependencyInjection.cs | `AvatarOptions` (+ even rule, ValidateOnStart); repository |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs, Migrations/AppDbContextModelSnapshot.cs | DbSet, `ConfigureAvatar`, soft-delete filter; snapshot regenerated |
| api/Elmanhg.Api/appsettings.example.json (+ local gitignored appsettings.json) | `Avatar` section |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 4 `Avatar:*` keys |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `twentySixth … _AddAvatarMessageUsages` |
| api/Elmanhg.Tests/Integration/Authorization/PermissionMatrixPolicyTests.cs | 3 `Avatar.Chat` rows |
| api/Elmanhg.Tests/Infrastructure/AiService/AiServiceTestSettings.cs, HttpAiServiceClientTests.cs, FakeAiServiceClientTests.cs | sources in the request; field-by-field reply assert; 4 new tests |
| api/openapi/v1.json | regenerated by `dotnet build` |
| postman/elmanhg.postman_collection.json | new `Avatar` folder after `Subscriptions` (status, lesson message, global message) |
| ai/src/elmanhg_ai/api/chat/schemas.py, router.py, clients/model.py, clients/anthropic_model.py, clients/fake_model.py, pipelines/chat.py, settings.py | sources in / citations out, `search_result` blocks, limits, v2 default |
| ai/openapi/v1.json | regenerated |
| ai/tests/unit/test_settings.py, test_chat_schemas.py, test_chat_pipeline.py, test_anthropic_model.py, test_fake_model.py, test_prompt_loader.py, ai/tests/integration/test_chat_endpoint.py | the listed modify + new tests |
| .env.example | commented `ELMANHG_AI_CHAT_PROMPT_VERSION=v2`, `Avatar__MessageMaxLength=2000` |
| deploy/api.env.example, deploy/ai.env.example, docs/deployment.md | new keys (orchestrator instruction; see Deviations) |
| web/src/features/quiz/components/AskAvatarButton.tsx | **deleted** (unstaged deletion) |
| web/src/features/quiz/components/FeedbackPanel.tsx, QuizQuestionCard.tsx, QuizReviewItem.tsx, pages/QuizResultPage.tsx, i18n/{ar,en}.json | `ask` context wiring; `avatar.ask`/`avatar.soon` removed, `avatar.questionTitle` added |
| web/src/features/exam/components/ExamReviewItem.tsx, ExamRunner.tsx, pages/ExamResultPage.tsx, i18n/{ar,en}.json | ExamReview wiring; exam bar button (Global) |
| web/src/features/browse/pages/LessonPage.tsx | lesson button after `<Outlet />` |
| web/src/features/shell/components/AppShell.tsx, web/src/routes/student/route.tsx | `assistant` slot; `AvatarProvider` + `AvatarDock` for students |
| web/src/app/i18n.ts, web/src/test/msw/server.ts | `avatar` namespace; default status handler |
| web/src/shared/form/TextField.tsx, SubmitButton.tsx | optional `placeholder` / `disabled` props (see Deviations) |
| web/src/shared/api/generated/** | regenerated by `npm run gen:api` |
| web tests: FeedbackPanel.test.tsx (replace), QuizPage.test.tsx (line 119 + 1 new), LessonPage.test.tsx, ExamResultPage.test.tsx, ExamPage.test.tsx, AppShell.test.tsx (new tests) | as listed |
| docs/PRD.md (§9.2, §9.3, §15, §16), docs/ai-service.md, docs/content-retrieval.md, docs/subscriptions.md, docs/sessions.md, docs/claude-design-prompt.md §4, docs/prototype.md, README.md | as in the plan's Docs table |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Composer uses shared `TextField` with a placeholder and `SubmitButton`, disabled during exam/limit/pending | `TextField` has no `placeholder` prop and `SubmitButton` is only disabled while submitting; neither file is in "Existing code touched" | Added optional, backward-compatible `placeholder?: string` to `web/src/shared/form/TextField.tsx` and `disabled?: boolean` to `SubmitButton.tsx`. The input is disabled through RHF `useForm({ disabled })`. |
| Config keys only in appsettings.example, ApiFactory, code defaults, `.env.example` | Orchestrator instruction: new keys also go in `docs/deployment.md` and `deploy/*.env.example` (#112) | Added an "AI Avatar" table to docs/deployment.md, updated the `ELMANHG_AI_CHAT_PROMPT_VERSION` default row to v2 and added the two source limits; commented keys in deploy/api.env.example and deploy/ai.env.example. |
| D3 `AvatarTestData` has 4 stub/builder methods | Two handler test classes need the same 16-dependency handler setup | Added a nested `AvatarTestData.SendHarness` class (substitutes, clock, lesson/unit/subject stubs, captured `AiChatRequest`, quiz/question registration) in the same file; no new file. |
| E10: `settings = Settings()` then skip unless anthropic | `Settings()` raises `ValidationError` when `ELMANHG_AI_SERVICE_TOKEN` is unset, so `-m eval` would error instead of skipping | Wrapped it: a `ValidationError` also skips, with the reason naming all three variables. |
| B19 `FirstOrDefaultAsync(exam ? x => … : x => …)` | A conditional between two lambdas does not target-type to `Expression<Func<…>>` | One lambda with the captured flag: `x.Id == sid && x.StudentId == studentId && (exam ? x.Kind != Quiz && x.SubmittedAt != null : x.Kind == Quiz)`. Same semantics, EF-translatable. |
| B12 Short: `" ± t"` when `Tolerance > 0`, plus `"%"` when Percent | Ambiguous whether `%` applies without a tolerance ("9.8%" would be wrong) | `%` is appended only after a shown tolerance. |
| F16 scroll "useEffect on a ref" | jsdom has no `scrollIntoView` and the lint rule forbids an optional call on it | The effect sets `scrollTop = scrollHeight` on the log element. The typing line reads the send mutation through `useIsMutating(getSendAvatarMessageMutationKey())` (the plan did not name a mechanism). |
| F15 close button "ghost" | `Button` has no icon size | `variant="ghost" className="min-w-11 px-0"` (44 px target). |
| Test 143 axe | Radix `aria-hidden`s the rest of the page, so an axe scan of `document.body` reports `aria-hidden-focus` on the app behind the modal | The axe scan runs on the dialog element. |
| Integration quiz setup | `Sessions:MinQuizSize` rejects a 2-question quiz (422) | Seeds 5 questions and starts a 5-question quiz. |
| F19 notice link | — | The «اشترك» link also closes the panel on click (the modal would otherwise cover the subscription page), like the citation chips. |
| B14 `Title` switch | Enum switch expressions need a discard arm | `_ => ExplanationLabel`. |

## Build & test
- `dotnet build api/Elmanhg.slnx` → `Build succeeded.` (only the pre-existing core-libraries CS8618/CS8602 warnings). `api/openapi/v1.json` regenerated, and stable on a rebuild.
- `dotnet ef migrations add AddAvatarMessageUsages` (with a dummy design-time connection string in the environment, since the local appsettings has an empty one) → CreateTable `AvatarMessageUsages` + `IX_AvatarMessageUsages_StudentId_CreatedAt`, no drops.
- CI parity: `api/Elmanhg.Api/appsettings.json` moved aside, `dotnet test api/ -c Release` → `Test run summary: Passed!  total: 2817  failed: 0`, exit 0. File restored afterwards.
- `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude core-libraries` → one WHITESPACE finding in `Elmanhg.Tests/Builders/SubscriptionBuilder.cs(57,27)`, a file this story did not touch (pre-existing, CRLF noise per PROGRESS "Local-only noise"); nothing in changed files.
- New .NET tests: 70 unit rows (tests 1–70; 13 validator, 8 handler, 12 context, 5 status, 10 answer text, 3 bundle, 3 source, 3 mapper, 2 fake, 3 HTTP incl. the modified one, 8 domain) and 17 integration tests (71–87) plus the modified policy/migration tests (88–89).
- Mutation checks (each made a listed test fail, then restored): exam guard on another student id → tests 23 + 76 fail; usage recorded before the AI call → test 29 fails; `Distinct()` removed from the citation mapper → test 63 fails; spec `Deadline >= now − grace` → `Deadline > now` → test 3 fails; ai: adapter dedupe removed → test 100 fails; pipeline known-reference filter removed → test 95 fails; web: reducer drops the assistant turn → 4 tests fail (130, 129, 131, 135); composer ignores `examInProgress` → tests 136 and 148 fail.
- ai, as `ai-ci.yml` (`python -m uv`): `uv sync --locked` ok; `ruff format --check .` → 60 files already formatted; `ruff check .` → All checks passed; `mypy src` → Success: no issues found in 35 source files; `pytest -m "not eval" --cov=elmanhg_ai --cov-branch` → 135 passed, 1 deselected, total coverage 98%. `pytest -m eval -rs` → 1 skipped with the key reason. The 58.7k delimiter performance test is unchanged and passes. pip-audit and the Docker build steps were not run (no dependency or Dockerfile change).
- web: `npm run gen:api` (regenerated, stable on rerun); `npm run typecheck` ok; `npm run lint` ok (0 warnings); `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → All matched files use Prettier code style; `npx vitest run` → Test Files 144 passed, Tests 868 passed. `vitest --coverage` and `vite build` were not run separately.

## Notes for review
- Quota is a soft limit (D7): two parallel requests at 4/5 can both pass; hardening is for #115 as planned.
- `AvatarReplyResult.MessagesUsedToday` is `used + 1` from the pre-call count, not a re-count after save.
- `GET /api/avatar/status` is only fetched while the panel is open (`enabled: state.isOpen`); the ExamRunner button opens the Global context titled «امتحان جارٍ», which shares the Global conversation key with the floating button.
- Live eval and live Claude citations smoke test are deferred (no key), as the plan states; the `-m eval` test skips with a reason.
- PRD §9.1 "Global … asks the student to pick a lesson" was not in the Docs table and is left as is; docs/avatar.md and the design prompt say the general assistant asks the student to open the lesson.
