VERDICT: APPROVED

# Review — Teacher validation queue (#68, E3.S5), round 1

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Application/QuestionValidation/ApproveQuestion/ApproveQuestionHandler.cs:20` to `:35` (same shape in `RejectQuestionHandler.cs` and `BulkApproveQuestionsHandler.cs:29-55`): the reviewed-version check compares against the row as loaded. There is no concurrency token, so an admin content PUT that commits between the load and `SaveChangesAsync` still ends as "Approved" on the unseen v2. EF writes only the changed columns, so `Version` is not in the UPDATE. The window is milliseconds, and plan Scope/Out explicitly deferred the token. `docs/question-schemas.md` §Versioning also says "There is no concurrency token yet". Recommend a `deferred` issue to add a PostgreSQL `xmin` token on `Question` (skill deltas #1, §6.8), mapped to 409.
- `api/Elmanhg.Infrastructure/Migrations/20260928080255_AddQuestionValidationQueue.cs:128`: the decision backfill stamps `q."Version"`. A Rejected question whose content was edited through PUT after rejection (a version bump that stays Rejected) gets the current version, not the rejected one. This affects history display only.
- `…AddQuestionValidationQueue.cs:123`: the `SubmittedAt` backfill uses the latest revision or `CreationDate`. A question resubmitted without a content change gets its last edit time, not its resubmit time. This affects the age filter only, and only for pre-existing rows. The plan chose this rule.
- `web/src/features/questions/components/ValidationQueueItem.tsx:37`: the standalone checkbox is `size-6` (24px). The design-system accessibility rule says touch targets are at least 44px. The plan asked for at least 24px, and `ChoiceOptionRow.tsx:31` sets a precedent. Consider wrapping it in a 44px label.
- `web/src/features/questions/pages/ValidationQueuePage.tsx:47-53`: if the queue fails with `REVIEW_SESSION_NOT_FOUND` (for example, the session row is gone), Retry refetches with the same dead session id and does not call `restart()`.

## Verified
- CI parity, rerun by me:
  - api:
    - `dotnet test api/ -c Release`: 1070/1070 passed.
    - `dotnet build --no-incremental`: `api/openapi/v1.json` hash unchanged (stable).
    - `dotnet list package --vulnerable --include-transitive`: clean.
    - `has-pending-model-changes`: "No changes".
    - `dotnet format --verify-no-changes`: clean.
    - Guard grep: empty.
  - web:
    - `gen:api` run twice: identical tree hash, and the tracked diff has only the validation-queue additions.
    - `typecheck`, `lint`, `format:check` and `build`: clean.
    - `vitest run --coverage`: 70 files, 391/391 passed, thresholds met.
- Access control:
  - All 8 actions carry `QuestionsValidate`, which is Teacher-only (`PermissionMatrixPolicies.cs:20`).
  - An Admin gets 403 on approve, and the question stays Pending (integration `Approve_AsAdmin_Returns403AndStaysPending`).
  - Every by-id handler checks the live `TeacherSubject` and returns 403 `SUBJECT_OUT_OF_SCOPE`.
  - The queue intersects with the caller's subjects.
- ReviewSession IDOR:
  - Queue, opening and bulk all load the session and return 404 `REVIEW_SESSION_NOT_FOUND` when `TeacherId != userId`.
  - Integration tests `RecordOpening_OtherTeachersSession_Returns404…` and `BulkApprove_OtherTeachersSession_Returns404` prove it, plus unit test 42 for the queue.
- Bulk approve:
  - It runs server-side, per question in request order: scope, then `EnsureOpened` (expiry, then an opening at `question.Version`), then `Approve(assignment, question.Version)`.
  - It uses one `SaveChangesAsync`, so it is all-or-nothing.
  - Integration tests 72 and 73 prove that nothing is approved and that an edit after opening voids the opening.
  - The validator caps the count, rejects an empty list, rejects duplicates and rejects `Guid.Empty`.
- Domain:
  - The check order is retired, then pending, then assigned, then version (409 `ConflictCoreException`).
  - Difficulty changes only when it differs, and the version is untouched.
  - A `QuestionDecision` is appended on every approve and reject.
  - `SubmittedAt` is set in `Create`, which covers `CreateImported`. It is also set on a content-changing `Update` and on `Resubmit`.
- Migration:
  - It is additive only.
  - Both backfill SQL statements match the generated column names.
  - Enum values are stored as strings and match `QuestionDecisionOutcome` names.
  - The fresh-DB migration test runs the SQL, and `AppDbContextTests` lists the twelfth migration.
- Every file in the plan's *Files to create* exists (D1–D5, I1–I2, A1–A26, P1–P2, W1–W25). The only file outside the plan, `web/orval.config.ts`, is a disclosed deviation that follows the existing pattern.
- The other deviations match the report:
  - extra i18n keys
  - the selection-key approach in place of a `key`
  - extra test-data helpers
  - the extra Postman opening request
- The 11 new codes, plus `SUBJECT_OUT_OF_SCOPE`, `QUESTION_RETIRED` and `QUESTION_NOT_REJECTED`, are in both resx files and both web `common` locale files.
- `QuestionValidationOptions`:
  - has code defaults
  - is registered with `ValidateDataAnnotations().ValidateOnStart()`
  - is in `appsettings.example.json` and `ApiFactory`
- The three new entities are in the global soft-delete filter.
- Postman: the `QuestionValidation` folder follows the API-surface order and uses the inherited bearer token. Its description says "sign in as a teacher". It has the three new variables, and the scripts fill them.
- Docs sync:
  - PRD §8.1, `question-schemas.md` (Versioning and Validation status), `audit-log.md` (3 rows plus the not-audited reason), `claude-design-prompt.md` §4 and `prototype.md` item 9 all agree with the code.
  - No divergence found. The stats strip in the design prompt is out of scope for this story, which is incompleteness, not a finding.
- Existing tests changed only mechanically, plus the AppShell handlers and the migration list.
- Sign-out calls `queryClient.clear()` (`authSession.ts:28`), so the review session does not survive to the next user.

## Test quality
- All plan rows 1–77 and W1–W11 exist under the planned names.
- Handler tests use real domain objects from `QuestionBuilder` and `ReviewSession.Start`. They assert the resulting state, not substitute echoes. Every throw asserts the exception type, the error code and `DidNotReceive()` save. Success paths assert `Received(1)`.
- `BulkApproveQuestionsHandlerTests` would catch a missing `EnsureOpened`, a version-blind opening check, and a save on failure.
- `ValidationQueueFilterTests` compiles the real expression over builder questions.
- Integration tests read back the database and the audit rows.
- Web page tests capture the real request URL and body (session id, filters, `{version, difficulty}`, `{reviewSessionId, questionIds}`) and the opening POST.
- I found no vacuous tests.
