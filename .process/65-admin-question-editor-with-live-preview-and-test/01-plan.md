# Plan — [E3.S2] Admin question editor with live preview and test grader (#65)

## Goal
An admin can open the question bank at `#/admin/questions`. The bank is a paged list that filters by status, type, subject, validating teacher, minimum version, rejection-reason text and lesson. From a lesson (or from a row) the admin opens a per-type editor for the five v1 types (mcq, multi, trueFalse, fill, short). The editor shows a live student-view preview built from the same `QuestionView` component the quiz UI (#76) will reuse. Its "Try the answer" box grades the unsaved draft on the server with the real deterministic graders (PRD §6, §6.2). A Rejected question shows its reason, and a single "Save and resubmit for review" applies the edit and returns it to Pending. Before this story the admin had only raw `POST/PUT/GET /api/questions` and no UI.

## Scope
**In:**
- Domain:
  - `Question.RejectionReason`.
  - `Question.Reject(TeacherSubject, reason)`, domain only, so Rejected is reachable for tests (the same pattern #64 used for `Approve`).
  - `Question.Resubmit(...)`.
  - Deterministic graders and the PRD §6.2 answer normaliser (`Elmanhg.Domain.Questions.Grading`), with answer records per type.
- Application:
  - `GetQuestions`: paged and filtered.
  - `ResubmitQuestion`: audited.
  - `GradeQuestionDraft`.
  - `GetTeachers`, which feeds the teacher filter.
  - `IUserRepository`, reused from Morabh.
- API:
  - `GET /api/questions`, `PUT /api/questions/{id}/resubmit`, `POST /api/questions/grade-draft` and `GET /api/teachers`.
  - `rejectionReason` on the question detail.
  - OpenAPI documents enums as strings.
- Plumbing: migration `AddQuestionRejectionReason`, options, resx, Postman, regenerated OpenAPI and Orval.
- Web: a new `features/questions` feature, made of:
  - The list page with filters and pagination.
  - The new-question and edit-question pages, with type-specific forms for the five types.
  - `QuestionView` (the student view), the preview panel and the test-grade box.
  - Resubmit for rejected questions.
- Web, changes outside the new feature:
  - The lesson editor gets links to a new question and to the lesson's questions.
  - The rich-text editor gets a compact variant (no image upload) for option text.
  - Pagination is promoted to `shared/components` (its second use).
- Docs: `docs/question-schemas.md`, `docs/PRD.md` §5.3 and §6, `docs/audit-log.md`, `docs/claude-design-prompt.md` §4 and `docs/rich-text.md`.

**Out:**
- Approve and Reject commands and endpoints, the teacher queue, and the revision and rejection history view: #68.
- Retire and servable status in the list (the prototype's "قابل للعرض" column and "تقاعد" button): #67.
- Bulk import: #66.
- Per-question toggles for each normalisation rule, the Egyptian spelling-variant test corpus, and localized grader feedback text: #70, #71 and #72 (see D5).
- The "Correct answer: …" line under the preview.
- The #64 non-blocking note about validating sanitised option text.

**Deferred:** none. Every item under Out maps to an existing story. No external provider is involved.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | What the "real grading endpoint" is when E4 (#70–#72) is not built | Build the deterministic graders now as pure static classes in `Elmanhg.Domain.Questions.Grading`, following PRD §6 and §6.2 exactly. `POST /api/questions/grade-draft` validates the draft with the existing `QuestionFieldsValidator`, canonicalises it with `QuestionContentFactory`, and grades it with `QuestionGrader.Grade`. Attempts (#74) will call the same `QuestionGrader.Grade(question.Type, question.GradingSpec, question.MaxScore, answer)`. | A stub grader would make the test box lie, and the story says "the real grader". The graders are small pure functions whose rules PRD §6 fully specifies. Putting them in Domain means there is exactly one implementation. |
| D2 | Grade a saved question or the draft | Grade the **draft** in the request body (every `QuestionFields` field plus `answer`). There is no by-id grade endpoint. | Story: "test-grade an answer **before saving**". This also covers new questions, which have no id. |
| D3 | Answer wire shapes (new contract, documented in `docs/question-schemas.md`) | Mcq `{"optionId":"b"}` · Multi `{"optionIds":["a","c"]}` · TrueFalse `{"value":true}` · Fill `{"blanks":[{"id":"1","text":"20"}]}` · Short `{"text":"9.8"}` (numeric and text alike: a student types a string). Missing fields mean "no answer" and score 0. An answer that is not a JSON object, or does not deserialise into the type's record, gets `422 QUESTION_ANSWER_INVALID`. | These mirror the body ids from #64, and #74's attempt `answer` JSON reuses them. Rejecting malformed shapes at validation keeps the graders total. |
| D4 | Grading rules | Mcq and TrueFalse: exact match. Multi: without `partialCredit`, 1 if the selected set equals the correct set, else 0. With it, `max(0, (right − wrong) / |correct|)`, where unknown ids count as wrong and duplicates are ignored. Fill: `hits / blanks`, where a blank hits when its normalised answer equals any normalised accepted answer. Short numeric (`spec.value` present): parse, then `|v − value| ≤ tolerance`, or `≤ |value| × tolerance / 100` for percent. Short text: accepted-list match. An empty normalised answer never matches. The outcome is `Correct` (normalised ≥ 1), `Partial` (> 0) or `Incorrect`. `Score = round(normalised × maxScore, 2)`; `NormalisedScore = round(normalised, 4)`; rounding is `MidpointRounding.AwayFromZero`. | PRD §6 table and §19 Q3 (partial credit off by default, from #64). 4 decimals keep the mastery threshold (0.8, PRD §7.3) exact for 1/3 and 2/3. |
| D5 | Normaliser scope vs #70 | `AnswerNormalizer.Normalize(text, unifyLetterVariants)`: strip tashkeel (U+064B–U+065F, U+0670, U+06D6–U+06ED) and tatweel (U+0640); map Arabic-Indic (U+0660–0669) and Extended (U+06F0–06F9) digits to ASCII; when `unifyLetterVariants`, map أ إ آ ٱ → ا, ة → ه and ى → ي; collapse whitespace, trim, and `char.ToLowerInvariant`. The only per-question toggle is the existing `unifyLetterVariants`. This is a char loop with no regex. | This is exactly PRD §6.2 plus the single toggle #64 stored. #70 keeps "per-question toggles for each rule" and the Egyptian test corpus. The orchestrator must tell the #70/#71/#72 planners to start from `Elmanhg.Domain.Questions.Grading`. |
| D6 | Numeric parsing | Normalise with `unifyLetterVariants: false`, replace `٫` (U+066B) and `,` with `.`, and `−` (U+2212) with `-`, then `decimal.TryParse(…, NumberStyles.Float, CultureInfo.InvariantCulture)`. Anything else, such as trailing units, does not parse and scores 0. | This matches the prototype's separator handling. The #64 schema has no unit field, so there is nothing to strip safely. |
| D7 | Short numeric vs text at grading time | Numeric exactly when `spec.Value is not null`. The body is not read. | Canonical storage (#64) keeps `value` only for numeric specs, so the spec alone decides and graders need only the spec. |
| D8 | Rejected state and the edit-and-resubmit flow | Add a `RejectionReason` column, nullable `text`. `Reject` (domain only here) needs Pending (`QUESTION_NOT_PENDING`), a live assignment for the subject (`QUESTION_VALIDATOR_NOT_ASSIGNED`) and a non-blank reason (`QUESTION_REJECTION_REASON_REQUIRED`). It stores the trimmed reason, and `ValidatedBy`/`ValidatedAt` record the deciding teacher. `Resubmit(type, content, metadata, lesson, by)` needs Rejected (`QUESTION_NOT_REJECTED`), applies `Update` (so a content change bumps the version and writes a revision), then sets Pending and clears `RejectionReason`, `ValidatedBy` and `ValidatedAt`. It does not require a content change. A plain `PUT` on a Rejected question still keeps it Rejected (#64 D8). | PRD §5.3 ("Admin sees the reason and may edit and resubmit") and the prototype `qeSave`: one action clears the reason and goes back to pending. #68 adds the Reject command on top of this domain method. The reason survives in the audit diff of `Question.Resubmit`. |
| D9 | Resubmit endpoint shape | `PUT /api/questions/{questionId}/resubmit` with the same body as update (reuses `UpdateQuestionRequest`). A new `ResubmitQuestionCommand(QuestionId, QuestionFields)`, `IAuditableCommand` `Question.Resubmit`. One call, one `SaveChangesAsync`. | Save and resubmit is atomic. The web shows one "Save and resubmit" button for Rejected questions (prototype). |
| D10 | "Teacher" column and filter | Teacher = `ValidatedBy`, the teacher who approved or rejected. Its name comes from `User.DisplayName`. The filter options come from the new `GET /api/teachers` (policy `Users.Manage`, all users with role Teacher, ordered by name). | PRD §10.1 "teacher". `ValidatedBy` is the only question–teacher link. A teachers list is also needed by #68 and #106. |
| D11 | "Version" filter | `minVersion`: version ≥ n (n ≥ 1). | An exact-version filter is rarely useful. "Edited at least once" (`minVersion=2`) is the admin question. |
| D12 | "Rejection reason" filter | `rejectionReason`: case-insensitive contains, trimmed, max `Content:QuestionFilterMaxLength` (200). Mirrors `GetAuditLogsFilter.actor`. | Free-text search over reasons. Resubmit clears the reason, so it matches only currently rejected questions. |
| D13 | Other list filters and order | Also `status`, `type`, `subjectId` (prototype) and `lessonId` (the entry point from the lesson editor). Paged with `FindPaginatedAsync` (page size 1..`Content:QuestionListMaxPageSize` = 100, default 20). Ordered by `UpdationDate` desc, then `Id` desc. | The admin grid is genuinely paginated (SKILL §5.6). Recently edited and resubmitted questions come first. |
| D14 | List row data | `QuestionListItemResult` includes `LessonName` and `TeacherName`, loaded with two set queries per page (lessons by ids, users by ids). No N+1. | The prototype's table columns. SKILL §6.6. |
| D15 | Where `User` is read | New `IUserRepository : IRepository<User>` plus `UserRepository`, copied from Morabh (`Morabh.Domain/Users/IUserRepository.cs`, `Morabh.Infrastructure/Users/UserRepository.cs`). No custom methods: `FindAsync` covers both uses. | Reuse-first. `UserManager` has no filtered async list without referencing EF in Application. |
| D16 | Enum wire format in OpenAPI | Add `builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));` to `Program.cs`. The OpenAPI document then describes `QuestionType`, `QuestionDifficulty` and `QuestionValidationStatus` as string enums, and Orval generates string unions. | Today the document says `integer` while MVC reads and writes strings (SKILL §8.6, "one JSON contract"). The editor must send `"Mcq"`, and a number-typed client would force casts. |
| D17 | Where the web code lives | New feature `web/src/features/questions/` (namespace `questions`). It imports `RichTextEditor`, `RichTextViewer`, `ContentErrorState` and `ContentListSkeleton` through the content barrel. | Questions are a separate admin area (nav item). Cross-feature imports go through barrels (SKILL §1). |
| D18 | "Student-view preview component reused from the quiz UI" | The quiz UI (#76) does not exist yet. `QuestionView` is built here, exported from the `questions` barrel, and #76 must render it. It is controlled (`question`, `answer`, `onAnswerChange`, `disabled`). | There must be one renderer of the student view, and it exists before its second consumer. |
| D19 | Fill blanks in the student view | The stem renders through `RichTextViewer`, with every `[[id]]` replaced by `<u>&nbsp;(n)&nbsp;</u>`, where `(n)` is `t('view.blankMarker')` (ICU digits). Blank inputs are listed below the stem, labelled "Blank n". There are no inline inputs inside the HTML. | Splitting sanitised HTML into React parts is unsafe, and portals need effects. Numbered markers with labelled inputs are accessible. `u` is on the sanitiser allow-list. |
| D20 | Option text editing | Option text uses `RichTextEditor` in a new `compact` form (`min-h-11`, no image tool) so LaTeX works in options. Stem and explanation use the full editor, and image upload goes through `POST /api/lessons/{lessonId}/images` with the question's lesson. | #64 D5: physics options need LaTeX, and plain inputs would silently drop math on edit. It reuses the existing upload contract. |
| D21 | Web form model | A flat `QuestionValues` holds every type's section (see Files W-S1). `toQuestionRequest` builds the typed body and spec for the selected type only. Type switching is allowed only for a new question; the type select is disabled when editing (`QUESTION_TYPE_IMMUTABLE`). | A flat model keeps RHF simple and preserves input while the admin switches type on a new question. |
| D22 | Option and blank ids from the editor | Options: the first unused letter of `abcdefghij`. Blanks: the smallest unused positive integer as a string. New questions start with 4 options (`a`–`d`) and 1 blank (`1`). | Satisfies `^[a-z0-9-]{1,20}$` and the caps (10). The prototype starts with 4 options. |
| D23 | Two-column editor width | `lg:grid-cols-2`, the lesson editor precedent, not the design prompt's 1.2fr/1fr. | 1.2fr/1fr needs an arbitrary Tailwind value, which the style guide forbids, and there is no token for it. |
| D24 | Pagination component | Move `features/audit/components/AuditLogPagination.tsx` to `shared/components/Pagination.tsx` (`common:pagination.*`, same strings). Audit uses the shared one. | SKILL 6.7: promote on the second use. The strings are unchanged, so the audit tests are unaffected. |
| D25 | Digits in admin UI | Version, score and max score are pre-formatted with `formatNumber(value, lng, 'latin')` and interpolated as strings. | design-system: Latin digits in admin tables. ICU would otherwise print Arabic-Indic digits in `ar`. |
| D26 | Audit | `ResubmitQuestion` is `IAuditableCommand` (`Question.Resubmit`, id from the command). `GetQuestions`, `GradeQuestionDraft` and `GetTeachers` are reads and are not audited. | PRD §17 rule 13. |
| D27 | Morabh reuse | `IUserRepository`/`UserRepository`: Morabh files named in D15. The paged-list shape follows the vendored `Core/Core.Notifications/ListNotifications/ListNotificationsQueryHandler.cs` through Elmanhg's `GetAuditLogs` slice. The graders, normaliser, resubmit and grade-draft are new; Morabh has no equivalent (grep for normaliz/tashkeel/grade found only migrations). | Reuse-first rule. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/Question.cs` | Add `public string? RejectionReason { get; private set; }` directly after `ValidatedAt`. |
| `api/Elmanhg.Domain/Questions/Question.Approval.cs` | Extract the two guards of `Approve` into `private void EnsureValidatorCanDecide(TeacherSubject assignment)`, call it from `Approve`, and add `Reject` (Domain behaviour). |
| `api/Elmanhg.Domain/Questions/Question.Editing.cs` | Add `Resubmit` (Domain behaviour). `Update` is unchanged. |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// QUESTIONS` append `QuestionNotRejected = "QUESTION_NOT_REJECTED"` and `QuestionRejectionReasonRequired = "QUESTION_REJECTION_REASON_REQUIRED"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Under `// QUESTIONS` append `QuestionAnswerInvalid`, `QuestionPageNumberInvalid`, `QuestionPageSizeInvalid`, `QuestionFilterTooLong`, `QuestionVersionFilterInvalid` and `QuestionStatusInvalid` (values in Error codes). |
| `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | Append `[Range(1, int.MaxValue)] public int QuestionListMaxPageSize { get; set; }` and `[Range(1, int.MaxValue)] public int QuestionFilterMaxLength { get; set; }`. |
| `api/Elmanhg.Application/Questions/Shared/QuestionDetailResult.cs` | Append the parameter `string? RejectionReason`. |
| `api/Elmanhg.Application/Questions/Shared/QuestionResultGenerator.cs` | `GenerateDetail` passes `question.RejectionReason` last. Add `public static QuestionListItemResult GenerateListItem(Question question, string lessonName, string? teacherName)` → `new QuestionListItemResult(question.Id, question.LessonId, lessonName, question.SubjectId, question.Type.ToString(), question.Stem, question.Difficulty.ToString(), question.Version, question.ValidationStatus.ToString(), question.ValidatedBy, teacherName, question.RejectionReason, question.UpdationDate)`. |
| `api/Elmanhg.Api/Controllers/Questions/QuestionsController.cs` | Add the 3 actions in API surface (fields built inline, like the existing actions). |
| `api/Elmanhg.Api/Controllers/Questions/Requests.cs` | Append `public sealed record GradeQuestionDraftRequest(QuestionType? Type, string? Stem, JsonElement Body, JsonElement GradingSpec, string? Explanation, QuestionDifficulty? Difficulty, Guid? ObjectiveId, List<string>? Tags, int? MaxScore, JsonElement Answer);` |
| `api/Elmanhg.Api/Controllers/Teachers/TeachersController.cs` | Add the `GetTeachers` action (API surface). |
| `api/Elmanhg.Api/Program.cs` | After the `AddControllers()…` line add `builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));` (D16). |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | The 8 new keys (Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | Append `"QuestionListMaxPageSize": 100, "QuestionFilterMaxLength": 200` to the `"Content"` object, on the same line. Mirror them into the local gitignored `appsettings.json` if it exists; that file is not committed. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | Add `services.AddScoped<IUserRepository, UserRepository>();` before `ISubjectRepository`. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated. No `AppDbContext` edit is needed: a nullable `string` maps to `text`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build api/Elmanhg.Api`. Expect string enums, the 4 new operations, and `rejectionReason` on the detail result. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | After `Content:QuestionMaxScoreMax` add `["Content:QuestionListMaxPageSize"] = "100"` and `["Content:QuestionFilterMaxLength"] = "200"`. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | **modify**: append `ninth => ninth.Should().EndWith("_AddQuestionRejectionReason")` to `Migrate_FreshDatabase_LeavesNoPendingMigrations` (the accepted pattern). |
| `api/Elmanhg.Tests/Integration/OpenApi/OpenApiEndpointTests.cs` | Add row O1. |
| `api/Elmanhg.Tests/Application/Features/Questions/GetQuestion/GetQuestionHandlerTests.cs` | Add row G3. |
| `api/Elmanhg.Tests/Builders/QuestionBuilder.cs` | Add `public User Teacher { get; } = User.CreateTeacher("Teacher", "teacher@example.com");`. `Approved()` uses `Teacher` in `Build` (no behaviour change). Add `private string? _rejectionReason;` and `public QuestionBuilder Rejected(string reason) { _rejectionReason = reason; return this; }`. In `Build`, when `_rejectionReason is not null`, call `question.Reject(TeacherSubject.Create(Teacher, Subject, Guid.NewGuid()), _rejectionReason)`. Add `public QuestionFields McqFields(string stem = "<p>2 + 2 = ?</p>")`, which returns `new QuestionFields(QuestionType.Mcq, stem, Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), Json("""{"correctOptionId":"b"}"""), "<p>Add the numbers.</p>", QuestionDifficulty.Medium, null, [], 1)` (static). |
| `api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs` | Add `public static async Task<Guid> SeedRejectedQuestionAsync(ApiFactory factory, Guid lessonId, User teacher, string reason, CancellationToken cancellationToken)`. It mirrors `SeedQuestionAsync` but, instead of approving, calls `question.Reject(TeacherSubject.Create(teacher, subject, creator), reason)`. The `TeacherSubject` is never saved. |
| `postman/elmanhg.postman_collection.json` | Teachers folder: new **first** request "List teachers", GET `{{baseUrl}}/api/teachers`, test status 200 and `Array.isArray(body)`. Questions folder, appended in this order after "Update question": (1) "List questions", GET `/api/questions?lessonId={{lessonId}}&pageNumber=1&pageSize=20`, tests 200 and `body.items.length >= 1`. (2) "Grade question draft", POST `/api/questions/grade-draft` with the mcq sample plus `"answer":{"optionId":"b"}`, tests 200 and `body.outcome === "Correct"`. (3) "Resubmit question", PUT `/api/questions/{{questionId}}/resubmit` with the update body, tests status 400 and `body.code === "QUESTION_NOT_REJECTED"` (the sample question is Pending). The folder description gains one line explaining that resubmit only works on a Rejected question. |
| `docs/question-schemas.md` | Line 3: after "the per-type rules (…)" insert ", the graders (`Elmanhg.Domain.Questions.Grading`)". Versioning: replace "A Rejected question stays Rejected on edit; resubmission is its own flow." with "A Rejected question stays Rejected on a plain edit (`PUT /api/questions/{id}`); `PUT /api/questions/{id}/resubmit` applies the same edit and returns it to Pending (see Validation status)." Validation status: append 2 bullets (D8 Reject and Resubmit, in prose, with codes). New sections **Answer shapes** (D3: one JSON example per type) and **Grading** (D4, D5, D6, D7 as rules; `POST /api/questions/grade-draft` grades an unsaved draft with the same graders; attempts reuse them). Place both before "Changing a schema". |
| `docs/PRD.md` | §5.3: bullet "Rejection requires a reason. Admin sees the reason and may edit and resubmit." → "Rejection requires a reason. Admin sees the reason and may edit and resubmit: resubmitting applies the edit (a content change still bumps the version), returns the question to Pending and clears the rejection reason." §6: after the line "Per-type `body` and `grading_spec` JSON shapes: `docs/question-schemas.md`." add "Answer shapes and the exact grading rules (normalisation, numeric parsing, rounding) are in the same document." |
| `docs/audit-log.md` | After the `UpdateQuestion` row add `\| ResubmitQuestion \| \`Question.Resubmit\` \| Question \| command (the diff shows \`validationStatus\`, \`rejectionReason\`, \`validatedBy\`, \`validatedAt\`, and the content fields and \`version\` when the content changed) \|`. |
| `docs/claude-design-prompt.md` | §4 line starting "- `#/admin/questions` list with status filter;": replace with the text in Docs DOC1. §4 line starting "- `#/admin/content`": after "a live student-view preview," insert " links to a new question and to the lesson's questions,". |
| `docs/rich-text.md` | Images section: add the bullet "The question editor uploads stem and explanation images through the same endpoint, using the question's lesson. Choice option text uses the compact editor, which has no image tool." |
| `web/src/app/i18n.ts` | Import `questionsLocales` from `@/features/questions/locales`. Add `questions: questionsLocales.ar/en` to resources, and `'questions'` to `ns`. |
| `web/src/routes/admin/questions.tsx` | `createFileRoute('/admin/questions')({ validateSearch: questionListSearchSchema, component: QuestionListPage })`, both imported from `@/features/questions`. |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (`npm --prefix web run build`). |
| `web/src/features/content/index.ts` | Add `export { RichTextEditor } from './components/RichTextEditor';`, `export { ContentErrorState } from './components/ContentErrorState';` and `export { ContentListSkeleton } from './components/ContentListSkeleton';`. |
| `web/src/features/content/components/RichTextEditor.tsx` | Props: `onUploadImage?: ((file: File) => Promise<string>) \| undefined;` and `compact?: boolean \| undefined;`. The editor class string uses `compact ? 'min-h-11' : 'min-h-36'` in place of `min-h-36`. Pass `onOpenImage={onUploadImage ? () => { setDialog('image'); } : undefined}`. Render the image `Dialog` only when `onUploadImage` is set, and call `onUploadImage` from `ImageInsertForm` inside that branch. |
| `web/src/features/content/components/RichTextToolbar.tsx` | `onOpenImage?: (() => void) \| undefined;`. Render the image `Tool` only when `onOpenImage` is defined. |
| `web/src/features/content/pages/LessonEditorPage.tsx` | When `data` is set, render under the title row `<div className="flex flex-wrap gap-2">` with `<Button asChild variant="secondary" size="sm"><Link to="/admin/question/new/$lessonId" params={{ lessonId: data.id }}>{t('lessonEditor.newQuestion')}</Link></Button>` and `<Button asChild variant="ghost" size="sm"><Link to="/admin/questions" search={{ lessonId: data.id }}>{t('lessonEditor.viewQuestions')}</Link></Button>`. |
| `web/src/features/content/pages/LessonEditorPage.test.tsx` | Add row W-LE1. |
| `web/src/features/content/i18n/en.json`, `ar.json` | `lessonEditor.newQuestion`: "New question" / "سؤال جديد". `lessonEditor.viewQuestions`: "Lesson questions" / "أسئلة الدرس". |
| `web/src/features/audit/pages/AuditLogPage.tsx` | Import `Pagination` from `@/shared/components/Pagination` in place of `AuditLogPagination`; the props are unchanged. |
| `web/src/features/audit/components/AuditLogPagination.tsx` | **delete** (moved to shared, D24). |
| `web/src/features/audit/i18n/en.json`, `ar.json` | Remove the `pagination` object. |
| `web/src/shared/i18n/en.json`, `ar.json` | Add `pagination` `{label, previous, next, status}` with the exact strings removed from audit. Add `errors.<CODE>` for all 44 question codes (the 36 from #64 plus the 8 new ones), with text copied verbatim from `Messages.en.resx` / `Messages.ar.resx`: `QUESTION_NOT_FOUND` … `QUESTION_TOLERANCE_INVALID` (the application `// QUESTIONS` group), `QUESTION_OBJECTIVE_NOT_IN_LESSON`, `QUESTION_TYPE_IMMUTABLE`, `QUESTION_NOT_PENDING`, `QUESTION_VALIDATOR_NOT_ASSIGNED`, `QUESTION_NOT_REJECTED`, `QUESTION_REJECTION_REASON_REQUIRED` and the 6 new application codes. |
| `web/src/shared/api/generated/**` | `npm --prefix web run gen:api` after the API build. Never hand-edited. Expect folder `teachers/` to gain `getTeachers`, `questions/` to gain `getQuestions`, `resubmitQuestion` and `gradeQuestionDraft`, and `QuestionType`/`QuestionDifficulty`/`QuestionValidationStatus` to be string unions. If `zod/questions/questions.zod.ts` fails `tsc`, add `GetQuestions: { zod: { generate: { query: false } } }` next to the existing `GetAuditLogs` override in `web/orval.config.ts`, and nothing else. |

## Files to create
Conventions (carried over from #60–#64):
- Every command handler starts with the current-user guard.
- One `SaveChangesAsync` per mutation.
- `.ConfigureAwait(false)` on every await; braces on every `if`; block-bodied methods; no `?? throw` (constitution §1.3).
- Application `ErrorCodes` = `Elmanhg.Application.Exceptions.ErrorCodes`. Domain `ErrorCodes` = `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`. The domain throws `BusinessRuleViolationCoreException`.
- All results are admin-facing plain strings; no `LocalizedText`.
- ≤100 lines per C# file, ≤200 per TS file, ≤120 per component.

### Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| D-1 | `api/Elmanhg.Domain/Identity/IUserRepository.cs` | repo interface | `namespace Elmanhg.Domain.Identity; public interface IUserRepository : IRepository<User> { }` (Morabh `Morabh.Domain/Users/IUserRepository.cs`) |
| D-2 | `api/Elmanhg.Domain/Questions/Schemas/AnswerSchemas.cs` | records | `namespace Elmanhg.Domain.Questions.Schemas;` · `public sealed record McqAnswer(string? OptionId);` · `public sealed record MultiAnswer(List<string>? OptionIds);` · `public sealed record TrueFalseAnswer(bool? Value);` · `public sealed record FillBlankResponse(string? Id, string? Text);` · `public sealed record FillAnswer(List<FillBlankResponse>? Blanks);` · `public sealed record ShortAnswer(string? Text);` |
| D-3 | `api/Elmanhg.Domain/Questions/Grading/GradeOutcome.cs` | enum | `namespace Elmanhg.Domain.Questions.Grading; public enum GradeOutcome { Correct, Partial, Incorrect }` |
| D-4 | `api/Elmanhg.Domain/Questions/Grading/QuestionGrade.cs` | value object | `public sealed record QuestionGrade(decimal Score, decimal NormalisedScore, GradeOutcome Outcome)`. Carries `// Scores display to 2 decimals; the normalised score keeps 4 so mastery thresholds (PRD §7.3) compare 1/3 and 2/3 precisely.` above `private const int ScoreDecimals = 2; private const int NormalisedScoreDecimals = 4;`. `public static QuestionGrade FromNormalised(decimal normalised, int maxScore)` → `var outcome = normalised switch { >= 1m => GradeOutcome.Correct, > 0m => GradeOutcome.Partial, _ => GradeOutcome.Incorrect };` `return new QuestionGrade(Math.Round(normalised * maxScore, ScoreDecimals, MidpointRounding.AwayFromZero), Math.Round(normalised, NormalisedScoreDecimals, MidpointRounding.AwayFromZero), outcome);` |
| D-5 | `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs` | static | D5 algorithm. Named `private const char` code points with one WHY comment (`// PRD §6.2 character classes; Unicode code points are fixed invariants.`): `Tatweel = 'ـ'`, `FathatanFirst = 'ً'`, `DiacriticLast = 'ٟ'`, `SuperscriptAlef = 'ٰ'`, `QuranicMarkFirst = 'ۖ'`, `QuranicMarkLast = 'ۭ'`, `ArabicIndicZero = '٠'`, `ArabicIndicNine = '٩'`, `ExtendedZero = '۰'`, `ExtendedNine = '۹'`, `Alef = 'ا'`, `AlefHamzaAbove = 'أ'`, `AlefHamzaBelow = 'إ'`, `AlefMadda = 'آ'`, `AlefWasla = 'ٱ'`, `TaaMarbuta = 'ة'`, `Haa = 'ه'`, `AlefMaqsura = 'ى'`, `Yaa = 'ي'`. `public static string Normalize(string? text, bool unifyLetterVariants)`: return `string.Empty` for null or empty. Loop with a `StringBuilder` and `pendingSpace`: skip diacritics and tatweel; on whitespace set `pendingSpace = builder.Length > 0` and continue; otherwise append `' '` when `pendingSpace` (then clear it), and append `char.ToLowerInvariant(Map(character, unifyLetterVariants))`. `private static bool IsDiacritic(char character)` (ranges above). `private static char Map(char character, bool unifyLetterVariants)`: a switch expression over digits → `(char)('0' + (character - ArabicIndicZero))` (and Extended), and `AlefHamzaAbove or AlefHamzaBelow or AlefMadda or AlefWasla when unifyLetterVariants => Alef`, `TaaMarbuta when … => Haa`, `AlefMaqsura when … => Yaa`, `_ => character`. |
| D-6 | `api/Elmanhg.Domain/Questions/Grading/ChoiceGrader.cs` | static | `public static decimal GradeMcq(McqGradingSpec spec, McqAnswer answer)` → 1 when `answer.OptionId is not null && string.Equals(answer.OptionId, spec.CorrectOptionId, StringComparison.Ordinal)`, else 0. · `public static decimal GradeTrueFalse(TrueFalseGradingSpec spec, TrueFalseAnswer answer)` → 1 when `answer.Value is not null && answer.Value == spec.CorrectAnswer`. · `public static decimal GradeMulti(MultiGradingSpec spec, MultiAnswer answer)`: `correct = (spec.CorrectOptionIds ?? []).ToHashSet(StringComparer.Ordinal)`; if empty return 0; `selected = (answer.OptionIds ?? []).Where(x => x is not null).ToHashSet(StringComparer.Ordinal)`; without `PartialCredit` return `selected.SetEquals(correct) ? 1m : 0m`; otherwise `right = selected.Count(correct.Contains)`, `wrong = selected.Count - right`, and return `Math.Max(0m, (decimal)(right - wrong) / correct.Count)`. |
| D-7 | `api/Elmanhg.Domain/Questions/Grading/TextGrader.cs` | static | Constants with a WHY comment (`// Students type these separators on Arabic keyboards; they mean the ASCII characters.`): `ArabicDecimalSeparator = '٫'`, `MinusSign = '−'`. · `public static decimal GradeFill(FillGradingSpec spec, FillAnswer answer)`: `blanks = spec.Blanks ?? []`; if empty return 0; `responses` = `(answer.Blanks ?? []).Where(x => x?.Id is not null)`, first per id (ordinal), mapped id → `Text`; `hits` = blanks that have an id, a response, and `Matches(response, blank.AcceptedAnswers ?? [], spec.UnifyLetterVariants)`; return `(decimal)hits / blanks.Count`. · `public static decimal GradeShort(ShortGradingSpec spec, ShortAnswer answer)`: if `spec.Value is not null` return `TryParseNumber(answer.Text, out var number) && Math.Abs(number - spec.Value.Value) <= AllowedDifference(spec) ? 1m : 0m`; else return `Matches(answer.Text, spec.AcceptedAnswers ?? [], spec.UnifyLetterVariants ?? true) ? 1m : 0m`. · `private static decimal AllowedDifference(ShortGradingSpec spec)` → `spec.ToleranceMode == ToleranceMode.Percent ? Math.Abs(spec.Value.GetValueOrDefault()) * spec.Tolerance.GetValueOrDefault() / 100m : spec.Tolerance.GetValueOrDefault()`, with a named `PercentDivisor = 100m`. · `private static bool Matches(string? answer, List<string> accepted, bool unifyLetterVariants)`: normalise; an empty result → false; else `accepted.Any(x => AnswerNormalizer.Normalize(x, unifyLetterVariants) == normalised)`. · `private static bool TryParseNumber(string? text, out decimal value)`: `AnswerNormalizer.Normalize(text, false)`, `.Replace(ArabicDecimalSeparator, '.').Replace(',', '.').Replace(MinusSign, '-')`, then `decimal.TryParse(…, NumberStyles.Float, CultureInfo.InvariantCulture, out value)`. |
| D-8 | `api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs` | static | `public static QuestionGrade Grade(QuestionType type, string gradingSpec, int maxScore, JsonElement answer)` → `var normalised = type switch { QuestionType.Mcq => ChoiceGrader.GradeMcq(ReadSpec<McqGradingSpec>(gradingSpec), ReadAnswer<McqAnswer>(answer)), QuestionType.Multi => …GradeMulti…, QuestionType.TrueFalse => …, QuestionType.Fill => TextGrader.GradeFill(…), QuestionType.Short => TextGrader.GradeShort(…), _ => throw new InvalidOperationException("Unsupported question type."), }; return QuestionGrade.FromNormalised(normalised, maxScore);` · `private static T ReadSpec<T>(string json) where T : class` → `var value = JsonSerializer.Deserialize<T>(json, QuestionJson.SerializerOptions); if (value is null) { throw new InvalidOperationException("Question grading spec is not readable."); } return value;` · `private static T ReadAnswer<T>(JsonElement answer) where T : class`: the same pattern with `answer.Deserialize<T>(QuestionJson.SerializerOptions)` and the message "Question answer was not validated.". |

### Application
| # | Path | Type | Contract |
|---|------|------|----------|
| A-1 | `api/Elmanhg.Application/Questions/Shared/QuestionListItemResult.cs` | result (admin) | `public sealed record QuestionListItemResult(Guid Id, Guid LessonId, string LessonName, Guid SubjectId, string Type, string Stem, string Difficulty, int Version, string ValidationStatus, Guid? ValidatedBy, string? TeacherName, string? RejectionReason, DateTimeOffset UpdatedAt);` |
| A-2 | `api/Elmanhg.Application/Questions/GetQuestions/GetQuestionsQuery.cs` | query | `public sealed record GetQuestionsQuery(QuestionValidationStatus? Status, QuestionType? Type, Guid? SubjectId, Guid? LessonId, Guid? TeacherId, int? MinVersion, string? RejectionReason, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<QuestionListItemResult>>;` |
| A-3 | `.../GetQuestions/GetQuestionsValidator.cs` | validator | ctor `(IOptions<ContentOptions> contentOptions)`; `var options = contentOptions.Value;`. Rules: `PageNumber.ValidateMin(1, ErrorCodes.QuestionPageNumberInvalid)` · `PageSize.ValidateRange(1, options.QuestionListMaxPageSize, ErrorCodes.QuestionPageSizeInvalid)` · `Status.IsInEnum().WithErrorCode(ErrorCodes.QuestionStatusInvalid)` · `Type.IsInEnum().WithErrorCode(ErrorCodes.QuestionTypeInvalid)` · `RuleFor(x => x.MinVersion.GetValueOrDefault()).ValidateMin(1, ErrorCodes.QuestionVersionFilterInvalid).When(x => x.MinVersion.HasValue).OverridePropertyName(nameof(GetQuestionsQuery.MinVersion))` · `RejectionReason.ValidateMaxLength(options.QuestionFilterMaxLength, ErrorCodes.QuestionFilterTooLong)` |
| A-4 | `.../GetQuestions/GetQuestionsFilter.cs` | static | `public static Expression<Func<Question, bool>> Build(GetQuestionsQuery query)`: `var reason = string.IsNullOrWhiteSpace(query.RejectionReason) ? null : query.RejectionReason.Trim().ToLowerInvariant();`, then copy each filter into a local and `return x => (status == null \|\| x.ValidationStatus == status) && (type == null \|\| x.Type == type) && (subjectId == null \|\| x.SubjectId == subjectId) && (lessonId == null \|\| x.LessonId == lessonId) && (teacherId == null \|\| x.ValidatedBy == teacherId) && (minVersion == null \|\| x.Version >= minVersion) && (reason == null \|\| (x.RejectionReason != null && x.RejectionReason.ToLower().Contains(reason)));` (mirrors `GetAuditLogsFilter`). |
| A-5 | `.../GetQuestions/GetQuestionsHandler.cs` | handler | `(IQuestionRepository questionRepository, ILessonRepository lessonRepository, IUserRepository userRepository) : IRequestHandler<GetQuestionsQuery, PageData<QuestionListItemResult>>`. No user guard (it is a query, like `GetQuestion`). 1. `page = questionRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: GetQuestionsFilter.Build(request), orderBy: query => query.OrderByDescending(x => x.UpdationDate).ThenByDescending(x => x.Id), asNoTracking: true)`. 2. `lessonIds` = distinct `LessonId` list. `teacherIds` = distinct `ValidatedBy` values where `HasValue`. 3. `lessons = lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true)`. 4. `teachers = userRepository.FindAsync(x => teacherIds.Contains(x.Id), cancellationToken, asNoTracking: true)`. 5. Build the dictionaries `Id → Name` and `Id → DisplayName`. 6. Return a `PageData<QuestionListItemResult>` with `Items = page.Items.Select(x => QuestionResultGenerator.GenerateListItem(x, lessonNames.GetValueOrDefault(x.LessonId, string.Empty), x.ValidatedBy is null ? null : teacherNames.GetValueOrDefault(x.ValidatedBy.Value))).ToList()` and `PageNumber`/`PageSize`/`TotalItems`/`TotalPages` copied from `page`. |
| A-6 | `api/Elmanhg.Application/Questions/ResubmitQuestion/ResubmitQuestionCommand.cs` | command | `public sealed record ResubmitQuestionCommand(Guid QuestionId, QuestionFields Question) : IRequest, IAuditableCommand` · `AuditAction => "Question.Resubmit"` · `AuditResourceType => "Question"` · `AuditResourceId => QuestionId` |
| A-7 | `.../ResubmitQuestion/ResubmitQuestionValidator.cs` | validator | ctor `(IOptions<ContentOptions> contentOptions)`: `QuestionId.ValidateRequired(ErrorCodes.QuestionIdRequired)` · `RuleFor(x => x.Question).SetValidator(new QuestionFieldsValidator(contentOptions))` |
| A-8 | `.../ResubmitQuestion/ResubmitQuestionHandler.cs` | handler | `(IQuestionRepository questionRepository, ILessonRepository lessonRepository, IRichTextSanitizer richTextSanitizer, ICurrentUserService currentUserService) : IRequestHandler<ResubmitQuestionCommand>`. Steps: 1. user guard (`UserNotAuthenticated`). 2. `question = GetByIdAsync(request.QuestionId, cancellationToken)` (tracked); if null, throw `NotFoundCoreException(QuestionNotFound)`. 3. `lesson = lessonRepository.GetWithObjectivesAsync(question.LessonId, asNoTracking: true, cancellationToken)`; if null, throw `NotFoundCoreException(LessonNotFound)`. 4. `content`/`metadata` from `QuestionContentFactory`. 5. `question.Resubmit(request.Question.Type.GetValueOrDefault(), content, metadata, lesson, currentUserService.UserId.Value)`. 6. `SaveChangesAsync`. |
| A-9 | `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftQuery.cs` | query | `public sealed record GradeQuestionDraftQuery(QuestionFields Question, JsonElement Answer) : IRequest<QuestionGradeResult>;` |
| A-10 | `.../GradeQuestionDraft/QuestionGradeResult.cs` | result (admin) | `public sealed record QuestionGradeResult(decimal Score, decimal NormalisedScore, string Outcome, int MaxScore);` |
| A-11 | `.../GradeQuestionDraft/QuestionAnswerRules.cs` | static | `public static bool CanRead(QuestionType type, JsonElement answer)` → switch expression: `Mcq => QuestionSchemaReader.TryRead<McqAnswer>(answer, out _)`, `Multi => …MultiAnswer…`, `TrueFalse => …TrueFalseAnswer…`, `Fill => …FillAnswer…`, `Short => …ShortAnswer…`, `_ => false`. |
| A-12 | `.../GradeQuestionDraft/GradeQuestionDraftValidator.cs` | validator | ctor `(IOptions<ContentOptions> contentOptions)`: `RuleFor(x => x.Question).SetValidator(new QuestionFieldsValidator(contentOptions))` · `RuleFor(x => x).Must(x => QuestionAnswerRules.CanRead(x.Question.Type.GetValueOrDefault(), x.Answer)).WithErrorCode(ErrorCodes.QuestionAnswerInvalid).OverridePropertyName(nameof(GradeQuestionDraftQuery.Answer)).When(x => x.Question.Type.HasValue && Enum.IsDefined(x.Question.Type.Value))` |
| A-13 | `.../GradeQuestionDraft/GradeQuestionDraftHandler.cs` | handler | `(IRichTextSanitizer richTextSanitizer) : IRequestHandler<GradeQuestionDraftQuery, QuestionGradeResult>`. No repository and no user guard. 1. `content = QuestionContentFactory.CreateContent(request.Question, richTextSanitizer)`. 2. `grade = QuestionGrader.Grade(request.Question.Type.GetValueOrDefault(), content.GradingSpec, content.MaxScore, request.Answer)`. 3. `return Task.FromResult(new QuestionGradeResult(grade.Score, grade.NormalisedScore, grade.Outcome.ToString(), content.MaxScore));`. The method is not `async`. |
| A-14 | `api/Elmanhg.Application/Teachers/Shared/TeacherResult.cs` | result (admin) | `public sealed record TeacherResult(Guid Id, string DisplayName);` |
| A-15 | `api/Elmanhg.Application/Teachers/GetTeachers/GetTeachersQuery.cs` | query | `public sealed record GetTeachersQuery : IRequest<List<TeacherResult>>;` (no validator, like `GetSubjectsQuery`) |
| A-16 | `api/Elmanhg.Application/Teachers/GetTeachers/GetTeachersHandler.cs` | handler | `(IUserRepository userRepository)`: `var teachers = await userRepository.FindAsync(x => x.Role == UserRole.Teacher, cancellationToken, orderBy: query => query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id), asNoTracking: true).ConfigureAwait(false); return teachers.Select(x => new TeacherResult(x.Id, x.DisplayName)).ToList();` (Morabh `Morabh.Application/Users/ListUsers/ListUsersQueryHandler.cs` shape) |

### Infrastructure
| # | Path | Contract |
|---|------|----------|
| I-1 | `api/Elmanhg.Infrastructure/Identity/UserRepository.cs` | `namespace Elmanhg.Infrastructure.Identity; public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository { }` (Morabh `Morabh.Infrastructure/Users/UserRepository.cs`) |
| I-2, I-3 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddQuestionRejectionReason.cs` + `.Designer.cs` | `dotnet ef migrations add AddQuestionRejectionReason -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. `Up` contains only `AddColumn<string>("RejectionReason", "Questions", type: "text", nullable: true)`, and `Down` only the matching `DropColumn`. |

### Tests (api)
| # | Path |
|---|------|
| T-1 | `api/Elmanhg.Tests/Domain/Questions/QuestionRejectionTests.cs` |
| T-2…T-5 | `api/Elmanhg.Tests/Domain/Questions/Grading/{AnswerNormalizer,ChoiceGrader,TextGrader,QuestionGrader}Tests.cs` |
| T-6…T-8 | `api/Elmanhg.Tests/Application/Features/Questions/GetQuestions/{GetQuestionsValidator,GetQuestionsFilter,GetQuestionsHandler}Tests.cs` |
| T-9, T-10 | `api/Elmanhg.Tests/Application/Features/Questions/ResubmitQuestion/{ResubmitQuestionValidator,ResubmitQuestionHandler}Tests.cs` |
| T-11, T-12 | `api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/{GradeQuestionDraftValidator,GradeQuestionDraftHandler}Tests.cs` |
| T-13 | `api/Elmanhg.Tests/Application/Features/Teachers/GetTeachers/GetTeachersHandlerTests.cs` |
| T-14…T-16 | `api/Elmanhg.Tests/Integration/Content/{QuestionListEndpoint,QuestionResubmitEndpoint,QuestionGradeDraftEndpoint}Tests.cs`. Each copies the private helpers of `QuestionsEndpointTests` it needs (`AdminClientAsync`, `TeacherClientAsync`, `SeedLessonAsync`, `ReadCodeAsync`, `McqRequest`). |
| T-17 | `api/Elmanhg.Tests/Integration/Teachers/TeachersEndpointTests.cs` |

Validator tests use `Options.Create(new ContentOptions { … })` with every committed cap plus `QuestionListMaxPageSize = 100` and `QuestionFilterMaxLength = 200`, except where a row sets a limit. Handler tests use NSubstitute for repositories and `ICurrentUserService`. The sanitiser stub returns its input unchanged (the #64 `UpdateQuestionHandlerTests` precedent).

### Web — `web/src/features/questions/`
All paths below are relative to this folder. i18n namespace is `questions`. Constants that mirror server caps carry a `// mirrors Content:<Key>` comment.

**Feature root**
| # | Path | Contract |
|---|------|----------|
| W-R1 | `index.ts` | `export { QuestionListPage } from './pages/QuestionListPage'; export { QuestionEditorPage } from './pages/QuestionEditorPage'; export { NewQuestionPage } from './pages/NewQuestionPage'; export { QuestionView } from './components/QuestionView'; export { questionListSearchSchema, type QuestionListSearch } from './schemas/questionListSearchSchema'; export { emptyAnswer, type QuestionAnswer, type StudentQuestion } from './api/studentQuestion';` |
| W-R2 | `locales.ts` | `import ar from './i18n/ar.json'; import en from './i18n/en.json'; export const questionsLocales = { ar, en };` |
| W-R3 | `i18n/en.json`, `i18n/ar.json` | Keys in the i18n table below, both files. |

**api/**
| # | Path | Contract |
|---|------|----------|
| W-A1 | `api/questionOptions.ts` | `questionTypes = ['Mcq','Multi','TrueFalse','Fill','Short'] as const satisfies readonly QuestionType[]` · `questionDifficulties = ['Easy','Medium','Hard'] as const satisfies readonly QuestionDifficulty[]` · `validationStatuses = ['Pending','Approved','Rejected'] as const satisfies readonly QuestionValidationStatus[]` (types from `@/shared/api/generated/model`). `questionMaxScoreMax = 100` (mirrors Content:QuestionMaxScoreMax) · `questionOptionsMin = 2` · `questionOptionsMax = 10` (mirrors Content:QuestionOptionsMaxCount) · `questionBlanksMax = 10` (mirrors Content:QuestionBlanksMaxCount) · `questionFilterMaxLength = 200` (mirrors Content:QuestionFilterMaxLength) · `questionListPageSize = 20` · `optionIdAlphabet = 'abcdefghij'`. `toQuestionType(value: string): QuestionType` (the value when it is in `questionTypes`, else `'Mcq'`) and `toQuestionDifficulty(value: string): QuestionDifficulty` (else `'Medium'`). |
| W-A2 | `api/richTextContent.ts` | `hasRichTextContent(html: string): boolean` → `const body = new DOMParser().parseFromString(html, 'text/html').body; return (body.textContent ?? '').trim() !== '' \|\| body.querySelector('img, [data-latex]') !== null;` · `countOccurrences(text: string, value: string): number` (the `split(value).length - 1` form). |
| W-A3 | `api/questionValues.ts` | `emptyQuestionValues(type: QuestionType): QuestionValues` (D21/D22 defaults: stem `''`, explanation `''`, difficulty `'Medium'`, objectiveId `''`, tags `''`, maxScore `'1'`, options a–d empty and uncorrect, partialCredit false, trueFalseAnswer `''`, blanks `[{ id: '1', acceptedAnswers: '' }]`, unifyLetterVariants true, answerKind `'numeric'`, numericValue `''`, tolerance `'0'`, toleranceMode `'absolute'`, acceptedAnswers `''`). · `toQuestionValues(detail: QuestionDetailResult): QuestionValues`: starts from `emptyQuestionValues(toQuestionType(detail.type))`, copies the common fields (tags joined with `', '`, maxScore `String(…)`, objectiveId `?? ''`), then per type `safeParse`s body and spec with W-S2 and keeps the defaults when parsing fails (mapping as in D21). · `toQuestionRequest(values: QuestionValues): UpdateQuestionRequest` (per type: D3 of #64; `objectiveId: values.objectiveId === '' ? null : values.objectiveId`; `tags: splitTags(values.tags)`; `maxScore: Number(values.maxScore)`). · `nextOptionId(ids: readonly string[]): string` · `nextBlankId(ids: readonly string[]): string` · `splitLines(value: string): string[]` (split on `\n`, trim, drop empties) · `splitTags(value: string): string[]` (split on `,`, trim, drop empties). |
| W-A4 | `api/studentQuestion.ts` | `export interface StudentQuestion { type: QuestionType; stem: string; options: { id: string; text: string }[]; blankIds: string[]; answerKind: 'numeric' \| 'text' \| null; }` · `export interface QuestionAnswer { optionIds: string[]; trueFalse: boolean \| null; blanks: Record<string, string>; text: string; }` · `emptyAnswer(): QuestionAnswer` · `toStudentQuestion(values: DeepPartialSkipArrayKey<QuestionValues>): StudentQuestion` (every field `?? ''`/`[]`; `answerKind` only for Short) · `fillStemHtml(stem: string, blankIds: readonly string[], marker: (index: number) => string): string` (for each id in order, `split('[[' + id + ']]').join('<u> ' + marker(index) + ' </u>')`) · `toAnswerPayload(question: StudentQuestion, answer: QuestionAnswer): Record<string, unknown>` (D3: Mcq `{ optionId: answer.optionIds[0] ?? null }`, Multi `{ optionIds }`, TrueFalse `{ value: answer.trueFalse }`, Fill `{ blanks: question.blankIds.map((id) => ({ id, text: answer.blanks[id] ?? '' })) }`, Short `{ text: answer.text }`). |
| W-A5 | `api/questionListParams.ts` | `toQuestionListParams(search: QuestionListSearch): GetQuestionsParams` (`pageNumber: search.page ?? 1`, `pageSize: questionListPageSize`, and each defined filter spread in) · `hasActiveFilters(search): boolean` (any of status, type, subjectId, lessonId, teacherId, minVersion, rejectionReason defined) · `export interface QuestionListPage { items: QuestionListItemResult[]; pageNumber: number; totalPages: number; totalItems: number }` · `toQuestionListPage(data: PageDataOfQuestionListItemResult): QuestionListPage` (the same `Number(... ?? …)` coercion as `toAuditLogPage`). |
| W-A6 | `api/stemExcerpt.ts` | `stemExcerptLength = 80` · `stemExcerpt(html: string): string` → text content from `DOMParser`, whitespace collapsed and trimmed; longer than `stemExcerptLength` → the first `stemExcerptLength` chars, trimmed, + `'…'`. |
| W-A7 | `api/questionErrorFields.ts` | `questionErrorFields: ServerErrorFields<QuestionValues>` = `QUESTION_STEM_REQUIRED`, `QUESTION_STEM_TOO_LONG`, `QUESTION_BLANK_PLACEHOLDER_MISSING` → `'stem'` · `QUESTION_EXPLANATION_TOO_LONG` → `'explanation'` · `QUESTION_MAX_SCORE_REQUIRED`, `QUESTION_MAX_SCORE_INVALID` → `'maxScore'` · `QUESTION_TAGS_TOO_MANY`, `QUESTION_TAG_REQUIRED`, `QUESTION_TAG_TOO_LONG` → `'tags'` · `QUESTION_OPTIONS_COUNT_INVALID`, `QUESTION_OPTION_TEXT_REQUIRED`, `QUESTION_OPTION_TEXT_TOO_LONG`, `QUESTION_CORRECT_OPTION_INVALID` → `'options'` · `QUESTION_CORRECT_ANSWER_REQUIRED` → `'trueFalseAnswer'` · `QUESTION_BLANKS_COUNT_INVALID`, `QUESTION_BLANK_ANSWERS_MISMATCH` → `'blanks'` · `QUESTION_NUMERIC_VALUE_REQUIRED` → `'numericValue'` · `QUESTION_TOLERANCE_INVALID` → `'tolerance'` · `QUESTION_OBJECTIVE_NOT_IN_LESSON` → `'objectiveId'`. Any other code goes to root. |

**schemas/**
| # | Path | Contract |
|---|------|----------|
| W-S1 | `schemas/questionEditorSchema.ts` | `z.object({ type: z.enum(questionTypes), stem: z.string(), explanation: z.string(), difficulty: z.enum(questionDifficulties), objectiveId: z.string(), tags: z.string(), maxScore: z.string(), options: z.array(z.object({ id: z.string(), text: z.string(), correct: z.boolean() })), partialCredit: z.boolean(), trueFalseAnswer: z.enum(['', 'true', 'false']), blanks: z.array(z.object({ id: z.string(), acceptedAnswers: z.string() })), unifyLetterVariants: z.boolean(), answerKind: z.enum(['numeric', 'text']), numericValue: z.string(), tolerance: z.string(), toleranceMode: z.enum(['absolute', 'percent']), acceptedAnswers: z.string() }).superRefine(...)`, with `export type QuestionValues = z.infer<…>`. superRefine rules (path → message key): stem `!hasRichTextContent` → `stem`/`validation.required`. maxScore not `/^\d+$/` or outside 1..`questionMaxScoreMax` → `maxScore`/`questions:editor.errors.maxScore`. **Mcq/Multi:** `options.length < questionOptionsMin` → `options`/`…optionsCount`; each option `!hasRichTextContent(text)` → `options.{i}.text`/`validation.required`; Mcq correct count ≠ 1 → `options`/`…correctOption`; Multi correct count 0 → `options`/`…correctOptions`. **TrueFalse:** `trueFalseAnswer === ''` → `trueFalseAnswer`/`…trueFalseAnswer`. **Fill:** `blanks.length === 0` → `blanks`/`…blanksCount`; each blank `splitLines(acceptedAnswers).length === 0` → `blanks.{i}.acceptedAnswers`/`…acceptedAnswers`; any blank with `countOccurrences(stem, '[['+id+']]') !== 1` → `stem`/`…blankPlaceholder` (added once). **Short numeric:** `numericValue` trimmed is not a finite `Number` or is `''` → `numericValue`/`…number`; `tolerance` not finite or `< 0` → `tolerance`/`…tolerance`. **Short text:** `splitLines(acceptedAnswers).length === 0` → `acceptedAnswers`/`…acceptedAnswers`. The sections of other types are never checked. |
| W-S2 | `schemas/questionContentSchemas.ts` | The read schemas for stored JSON: `choiceBodySchema`, `mcqSpecSchema`, `multiSpecSchema`, `trueFalseSpecSchema`, `fillBodySchema`, `fillSpecSchema`, `shortBodySchema` (`answerKind: z.enum(['numeric','text'])`), `shortNumericSpecSchema` (`value`, `tolerance`: `z.number()`; `toleranceMode: z.enum(['absolute','percent'])`) and `shortTextSpecSchema`. Shapes are exactly `docs/question-schemas.md`. |
| W-S3 | `schemas/questionListSearchSchema.ts` | `z.object({ page: z.coerce.number().int().min(1).optional().catch(undefined), status: z.enum(validationStatuses).optional().catch(undefined), type: z.enum(questionTypes).optional().catch(undefined), subjectId: z.guid().optional().catch(undefined), lessonId: z.guid()…, teacherId: z.guid()…, minVersion: z.coerce.number().int().min(1).optional().catch(undefined), rejectionReason: z.string().trim().min(1).max(questionFilterMaxLength).optional().catch(undefined) })`, with `type QuestionListSearch`. |
| W-S4 | `schemas/questionListFiltersSchema.ts` | `z.object({ status: z.union([z.literal(''), z.enum(validationStatuses)]), type: z.union([z.literal(''), z.enum(questionTypes)]), subjectId: z.string(), teacherId: z.string(), minVersion: z.string().trim().refine((v) => v === '' \|\| /^[1-9]\d*$/.test(v), { error: 'questions:list.filters.errors.minVersion' }), rejectionReason: z.string().trim().max(questionFilterMaxLength, { error: 'questions:list.filters.errors.rejectionReasonTooLong' }) })`, with `type QuestionListFiltersValues`. |

**hooks/**
| # | Path | Contract |
|---|------|----------|
| W-H1 | `hooks/useQuestionList.ts` | `useQuestionList(search)` → `useGetQuestions(toQuestionListParams(search), { query: { placeholderData: keepPreviousData, select: toQuestionListPage } })`. |
| W-H2 | `hooks/useQuestionListSearch.ts` | `getRouteApi('/admin/questions')`. Returns `{ search, applyFilters(values: QuestionListFiltersValues), setPage(page), clearFilters() }`. `applyFilters` navigates to `{ page: 1, lessonId: search.lessonId (kept when defined), status/type/subjectId/teacherId when non-empty, minVersion: Number(v) when non-empty, rejectionReason when non-empty after trim }`. `clearFilters` navigates to `{}`. |
| W-H3 | `hooks/useQuestionSave.ts` | `useQuestionSave(lessonId: string, question: QuestionDetailResult \| undefined): (values: QuestionValues) => Promise<void>`. Uses `useCreateQuestion`, `useUpdateQuestion`, `useResubmitQuestion`, `useQueryClient`, `useNavigate`, and `toast` from `sonner` with `t` from the `questions` namespace. **New:** `create.mutateAsync({ data: { lessonId, ...toQuestionRequest(values) } })`, toast `editor.created`, invalidate, then `navigate({ to: '/admin/question/$questionId', params: { questionId: id } })`. **Rejected:** `resubmit.mutateAsync({ questionId, data })`, toast `editor.resubmitted`. **Otherwise:** `update.mutateAsync({ questionId, data })`, toast `editor.saved`. Invalidate `getGetQuestionsQueryKey()`, `getGetLessonsQueryKey()` and, for an existing question, `getGetQuestionQueryKey(question.id)`. Errors propagate to `Form`, which maps them. |
| W-H4 | `hooks/useTestGrade.ts` | `useTestGrade(): { grade: (values: QuestionValues, answer: QuestionAnswer) => void; result: QuestionGradeResult \| undefined; errorCode: string \| null; isPending: boolean }`. It wraps `useGradeQuestionDraft()`: `mutation.mutate({ data: { ...toQuestionRequest(values), answer: toAnswerPayload(toStudentQuestion(values), answer) } })`. `errorCode` is `mutation.error instanceof ApiError ? mutation.error.code : unhandledErrorCode` when there is an error, else null. |
| W-H5 | `hooks/useQuestionImageUpload.ts` | `useQuestionImageUpload(lessonId: string): (file: File) => Promise<string>` → `(await useUploadLessonImage().mutateAsync({ lessonId, data: { file } })).url`. |

**components/**
Styling follows the tokens: cards `rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5`, and the select class copied from `ResourceTypeField`.

| # | Path | Contract |
|---|------|----------|
| W-C1 | `components/SelectField.tsx` | Generic RHF select: `SelectFieldProps<TValues extends FieldValues> { name: Path<TValues>; label: string; options: readonly { value: string; label: string }[]; placeholder?: string \| undefined; description?: string \| undefined; disabled?: boolean \| undefined }`. `Label` + native `<select>` (placeholder renders `<option value="">`), with `aria-invalid`, `aria-describedby`, and the error text like `TextField`. |
| W-C2 | `components/TextAreaField.tsx` | `TextField` clone with a `<textarea rows={3}>` (`min-h-20 w-full rounded-sm border border-border-strong bg-surface px-3 py-2.25 text-ui` plus the focus ring classes of `Input`). Props `{ name, label, description? }`. |
| W-C3 | `components/CheckboxField.tsx` | `{ name: Path<TValues>; label: string }` → `<label className="flex min-h-11 items-center gap-2.5 text-ui"><input type="checkbox" className="size-4.5 accent-text" …/>{label}</label>`, bound through `useController`. |
| W-C4 | `components/QuestionRichTextField.tsx` | Mirrors content `RichTextField` for `QuestionValues`. Props `{ name: 'stem' \| 'explanation' \| \`options.${number}.text\`; label: string; description?: string \| undefined; compact?: boolean; onUploadImage?: ((file: File) => Promise<string>) \| undefined }`. Uses `RichTextEditor` from `@/features/content`. |
| W-C5 | `components/ChoiceOptionsField.tsx` | `useFieldArray<QuestionValues,'options'>` + `useWatch({ name: ['type','options'] })`. A `fieldset` with legend `editor.options.legend`. Per option it renders `ChoiceOptionRow`. The "Add option" button is disabled at `questionOptionsMax` and appends `{ id: nextOptionId(ids), text: '', correct: false }`. The `errors.options?.message` paragraph is `text-caption text-danger`. For Multi it also renders a `CheckboxField` `partialCredit` (`editor.options.partialCredit`). |
| W-C6 | `components/ChoiceOptionRow.tsx` | Props `{ index: number; multiple: boolean; groupName: string; canRemove: boolean; onRemove: () => void }`. It renders the correct control and the compact rich-text field `options.{index}.text` labelled `editor.options.option` {number}. The correct control is a radio (Mcq, `name={groupName}`, checking it sets every `options.{j}.correct` to `j === index` through `setValue`) or a checkbox (Multi), `aria-label` `editor.options.correct` {number}. The remove button is icon-only `Trash2` with `aria-label` `editor.options.remove` {number}, disabled when `!canRemove`. |
| W-C7 | `components/FillBlanksField.tsx` | `useFieldArray` `blanks`. Per blank: `<code dir="ltr" className="font-mono text-mono">[[{id}]]</code>`, a `TextAreaField` `blanks.{i}.acceptedAnswers` labelled `editor.blanks.accepted` {number}, and a remove button (`editor.blanks.remove`, disabled at 1). An add button (disabled at `questionBlanksMax`). A `CheckboxField` `unifyLetterVariants` (`editor.blanks.unify`). The `errors.blanks?.message` paragraph. |
| W-C8 | `components/ShortAnswerFields.tsx` | `SelectField` `answerKind` (numeric/text labels). Numeric: `TextField` `numericValue` (`dir="ltr"`), `TextField` `tolerance` (`dir="ltr"`), `SelectField` `toleranceMode`. Text: `TextAreaField` `acceptedAnswers` + `CheckboxField` `unifyLetterVariants`. |
| W-C9 | `components/TypeSpecificFields.tsx` | `useWatch({ name: 'type' })`. Mcq/Multi → `ChoiceOptionsField`; TrueFalse → `SelectField` `trueFalseAnswer` (placeholder `editor.trueFalse.choose`, options true/false); Fill → `FillBlanksField`; Short → `ShortAnswerFields`. |
| W-C10 | `components/QuestionMetadataFields.tsx` | Props `{ lesson: LessonDetailResult }`. `SelectField` `difficulty` (translated `difficulties.*`), `SelectField` `objectiveId` (placeholder `editor.fields.noObjective`, lesson objectives sorted by `order`), `TextField` `maxScore` (`dir="ltr"`), `TextField` `tags` (description `editor.fields.tagsHint`). |
| W-C11 | `components/QuestionEditorForm.tsx` | Props `{ lesson: LessonDetailResult; question?: QuestionDetailResult \| undefined }`. `useForm<QuestionValues>({ resolver: zodResolver(questionEditorSchema), defaultValues: question ? toQuestionValues(question) : emptyQuestionValues('Mcq') })`. `save = useQuestionSave(lesson.id, question)`; `upload = useQuestionImageUpload(lesson.id)`. `<Form form onSubmit={save} serverErrorFields={questionErrorFields} className="grid gap-4 lg:grid-cols-2 lg:items-start">`. Left card: `FormRootError`, `SelectField` `type` (`disabled={question !== undefined}`, description `editor.fields.typeLocked` when disabled), `QuestionRichTextField` stem (description `editor.fields.stemFillHint` when the type is Fill, `onUploadImage={upload}`), `TypeSpecificFields`, `QuestionMetadataFields`, `QuestionRichTextField` explanation (`onUploadImage={upload}`), and `SubmitButton` labelled `editor.create` when there is no question, `editor.resubmit` when Rejected, else `editor.save`. Right: `QuestionPreviewPanel`. |
| W-C12 | `components/QuestionEditorHeader.tsx` | Props `{ lesson: LessonDetailResult; question?: QuestionDetailResult \| undefined }`. A breadcrumb `nav` (aria-label `editor.breadcrumb`): link `editor.questionsLink` → `/admin/questions`, link lesson name → `/admin/lesson/$lessonId`, and the current page `editor.newTitle`/`editor.editTitle`. `h1` with the same title. For an existing question, `QuestionStatusBadge` + a version badge (`editor.versionBadge` with the Latin-formatted version). Approved: `<p className="rounded-md border border-warning bg-warning-soft px-3.5 py-3 text-caption text-text">{t('editor.approvedWarning')}</p>`. Rejected: the same but `border-danger bg-danger-soft`, with `editor.rejectionReason` {reason}. |
| W-C13 | `components/QuestionView.tsx` | Props `{ question: StudentQuestion; answer: QuestionAnswer; onAnswerChange: (answer: QuestionAnswer) => void; disabled?: boolean }`. Stem: `<div className="text-body font-semibold"><RichTextViewer html={question.type === 'Fill' ? fillStemHtml(question.stem, question.blankIds, (i) => t('view.blankMarker', { number: i + 1 })) : question.stem} /></div>`. Then `ChoiceAnswerInputs` for Mcq/Multi/TrueFalse, or `TextAnswerInputs` for Fill/Short. |
| W-C14 | `components/ChoiceAnswerInputs.tsx` | Same props. A `fieldset` with `<legend className="sr-only">{t('view.answerLegend')}</legend>`. Items are the options (Mcq radio / Multi checkbox) or `[true, false]` (TrueFalse radios labelled `view.true`/`view.false`). Each item is `<label className="flex min-h-12 cursor-pointer items-center gap-2.5 rounded-md border border-border-strong bg-surface px-3.5 py-3 text-ui hover:bg-soft has-checked:border-text has-checked:bg-soft">` + `<input className="size-4.5 accent-text" name={useId()-based} …>` + `RichTextViewer` (options) or text. Changes call `onAnswerChange({ ...answer, optionIds: [id] })` (Mcq), a toggled `optionIds` (Multi), or `{ ...answer, trueFalse: value }`. |
| W-C15 | `components/TextAnswerInputs.tsx` | Fill: per blank, `Label` `view.blank` {number} + `Input` (value `answer.blanks[id] ?? ''`, onChange → `{ ...answer, blanks: { ...answer.blanks, [id]: value } }`). Short: `Label` `view.yourAnswer` + `Input` (numeric: `dir="ltr" inputMode="decimal"`), onChange → `{ ...answer, text }`. |
| W-C16 | `components/QuestionPreviewPanel.tsx` | No props (uses form context). `const values = useWatch<QuestionValues>(); const question = toStudentQuestion(values); const [answer, setAnswer] = useState(emptyAnswer); const { getValues } = useFormContext<QuestionValues>(); const { grade, result, errorCode, isPending } = useTestGrade();`. Renders `<section aria-label={t('preview.title')} className=card>`: `h2` `preview.title`, `QuestionView`, `<Button variant="secondary" disabled={isPending} onClick={() => { grade(getValues(), answer); }}>` labelled `preview.grading` while pending, else `preview.tryAnswer`. On error: `<div role="alert">` with `<p>{t('preview.gradeFailed')}</p><p>{t([\`common:errors.${errorCode}\`, 'common:errors.UNHANDLED_EXCEPTION'])}</p>`. On a result: `GradeResultPanel`. |
| W-C17 | `components/GradeResultPanel.tsx` | Props `{ result: QuestionGradeResult }`. `role="status"`. Correct: `border-success bg-success-soft`, `CircleCheck`, `preview.correct`. Partial: `border-warning bg-warning-soft`, `CircleAlert`, `preview.partial`. Incorrect: `border-danger bg-danger-soft`, `CircleX`, `preview.incorrect`. Icon `size-6.5` in the verdict colour, `aria-hidden`; verdict `font-semibold`; `preview.score` with `{ score, maxScore }` Latin-formatted strings (D25). `rounded-md px-3.5 py-3`. |
| W-C18 | `components/QuestionStatusBadge.tsx` | Props `{ status: string }`. Approved `bg-success text-surface`, Rejected `bg-danger text-surface`, otherwise `bg-soft text-text-muted`. Text `t([\`statuses.${status}\`, 'statuses.Pending'])`. The classes are the same as `LessonStateBadge`. |
| W-C19 | `components/QuestionListFilters.tsx` | Mirrors `AuditLogFilters`. The form schema is W-S4; defaults come from search. Fields, in a grid `grid-cols-1 gap-3 md:grid-cols-3`: `SelectField` status, type, subject (`useGetSubjects`), teacher (`useGetTeachers`), each with placeholder `list.filters.all`; `TextField` `minVersion` (`dir="ltr"`); `TextField` `rejectionReason`. Buttons: `SubmitButton` `list.filters.apply`, and ghost `list.filters.clear`. |
| W-C20 | `components/QuestionTable.tsx` | Mirrors `AuditLogTable`. Header keys `question, lesson, type, status, version, teacher, rejectionReason, actions` under `list.table.*`, with caption `list.table.caption`. |
| W-C21 | `components/QuestionRow.tsx` | Cells: `stemExcerpt(item.stem)`, `item.lessonName`, `t(\`types.${item.type}\`)`, `QuestionStatusBadge`, `list.table.versionValue` (Latin version), `item.teacherName ?? t('list.table.noTeacher')`, `item.rejectionReason ?? ''`, and `<Button asChild size="sm" variant="secondary"><Link to="/admin/question/$questionId" params={{ questionId: item.id }}>` labelled `list.table.editAndResubmit` when Rejected, else `list.table.edit`. |
| W-C22 | `components/QuestionListEmptyState.tsx` | Mirrors `AuditLogEmptyState` (icon `ListChecks`) with `list.empty.noData` / `list.empty.noResults` and a clear button `list.filters.clear`. |

**pages/**
| # | Path | Contract |
|---|------|----------|
| W-P1 | `pages/QuestionListPage.tsx` | Mirrors `AuditLogPage`: `useQuestionListSearch` + `useQuestionList`. `h1` `list.title`, then the count caption `list.count` {count} (when data), then `<p className="text-caption text-text-muted">` `list.hint`. When `search.lessonId` is set: `list.lessonFilter` + `<Button asChild variant="secondary" size="sm"><Link to="/admin/question/new/$lessonId" params={{ lessonId: search.lessonId }}>{t('list.newInLesson')}</Link></Button>`. `QuestionListFilters` is keyed on the filter values. Content states: pending → `ContentListSkeleton label={t('list.loading')}`; error → `ContentErrorState title={t('list.errorTitle')}` with retry; empty → `QuestionListEmptyState` (`no-results` when `hasActiveFilters`); else `QuestionTable` plus `Pagination` (shared) when `totalPages > 1`. |
| W-P2 | `pages/QuestionEditorPage.tsx` | Props `{ questionId: string }`. `questionQuery = useGetQuestion(questionId)`; `lessonQuery = useGetLesson(questionQuery.data?.lessonId ?? '')`. Either pending → `ContentListSkeleton label={t('editor.loading')}`. Question error → `ContentErrorState title={t('editor.errorTitle')}` with retry `questionQuery.refetch`. Lesson error → `editor.lessonErrorTitle` with retry `lessonQuery.refetch`. Loaded → `<section className="flex flex-col gap-4">`, `QuestionEditorHeader`, and `<QuestionEditorForm key={\`${q.id}-${String(q.version)}-${q.validationStatus}\`} lesson question={q} />`. |
| W-P3 | `pages/NewQuestionPage.tsx` | Props `{ lessonId: string }`. `useGetLesson(lessonId)` with the same loading and error states (`editor.lessonErrorTitle`). Loaded → `QuestionEditorHeader lesson` + `QuestionEditorForm lesson`. |

**Shared and routes**
| # | Path | Contract |
|---|------|----------|
| W-X1 | `web/src/shared/components/Pagination.tsx` | The body of `AuditLogPagination`, renamed `Pagination` / `PaginationProps`, using `useTranslation()` and `pagination.*` keys. |
| W-X2 | `web/src/routes/admin/question.$questionId.tsx` | `createFileRoute('/admin/question/$questionId')({ component: QuestionEditorRoute })`, where `QuestionEditorRoute` reads `Route.useParams()` and renders `<QuestionEditorPage questionId={questionId} />` (mirrors `lesson.$lessonId.tsx`). |
| W-X3 | `web/src/routes/admin/question.new.$lessonId.tsx` | `createFileRoute('/admin/question/new/$lessonId')(…)` → `<NewQuestionPage lessonId={lessonId} />`. |

**Tests (web)**
| # | Path |
|---|------|
| WT-1…WT-4 | `schemas/questionEditorSchema.test.ts`, `schemas/questionListSearchSchema.test.ts`, `schemas/questionListFiltersSchema.test.ts` and `api/questionValues.test.ts` |
| WT-5…WT-7 | `api/studentQuestion.test.ts`, `api/questionListParams.test.ts` and `api/stemExcerpt.test.ts` |
| WT-8 | `components/QuestionView.test.tsx` |
| WT-9…WT-11 | `pages/QuestionListPage.test.tsx`, `pages/QuestionEditorPage.test.tsx` and `pages/NewQuestionPage.test.tsx` (all through `renderApp(path, { session: testSessions.admin })` and the generated MSW handlers) |
| WT-12 | `web/src/shared/components/Pagination.test.tsx` |

Test ids in the web tests are real GUIDs (W-S3 uses `z.guid()`), for example `11111111-1111-4111-8111-111111111111`.

### i18n (`questions` namespace)
| Key | en | ar |
|---|---|---|
| `types.Mcq/Multi/TrueFalse/Fill/Short` | Multiple choice / Multiple answers / True or false / Fill in the blank / Short answer | اختيار من متعدد / اختيار متعدد الإجابات / صح أو خطأ / أكمل الفراغ / إجابة قصيرة |
| `statuses.Pending/Approved/Rejected` | Pending review / Approved / Rejected | قيد المراجعة / معتمد / مرفوض |
| `difficulties.Easy/Medium/Hard` | Easy / Medium / Hard | سهل / متوسط / صعب |
| `list.title` | Question bank | بنك الأسئلة |
| `list.count` | `{count, plural, one {# question} other {# questions}}` | `{count, plural, zero {لا أسئلة} one {سؤال واحد} two {سؤالان} few {# أسئلة} many {# سؤالًا} other {# سؤال}}` |
| `list.loading` / `list.errorTitle` | Loading questions / Could not load the questions | جارٍ تحميل الأسئلة / تعذر تحميل الأسئلة |
| `list.hint` | To add a question, open its lesson from the content tree. | لإضافة سؤال افتح الدرس من شجرة المحتوى. |
| `list.lessonFilter` / `list.newInLesson` | Showing one lesson's questions. / New question in this lesson | تعرض أسئلة درس واحد. / سؤال جديد في هذا الدرس |
| `list.empty.noData` / `list.empty.noResults` | No questions yet. / No questions match these filters. | لا توجد أسئلة بعد. / لا توجد أسئلة تطابق هذه الفلاتر. |
| `list.table.caption/question/lesson/type/status/version/teacher/rejectionReason/actions` | Questions / Question / Lesson / Type / Status / Version / Teacher / Rejection reason / Actions | الأسئلة / السؤال / الدرس / النوع / الحالة / الإصدار / المعلّم / سبب الرفض / الإجراءات |
| `list.table.versionValue` / `noTeacher` / `edit` / `editAndResubmit` | v{version} / — / Edit / Edit and resubmit | v{version} / — / تعديل / تعديل وإعادة إرسال |
| `list.filters.status/type/subject/teacher/minVersion/rejectionReason/all/apply/clear` | Status / Type / Subject / Teacher / Version at least / Rejection reason contains / All / Apply filters / Clear filters | الحالة / النوع / المادة / المعلّم / الإصدار على الأقل / سبب الرفض يحتوي على / الكل / تطبيق الفلاتر / مسح الفلاتر |
| `list.filters.errors.minVersion` / `rejectionReasonTooLong` | Enter a whole number of 1 or more. / The search text is too long. | اكتب رقمًا صحيحًا 1 أو أكثر. / نص البحث طويل جدًا. |
| `editor.newTitle/editTitle/breadcrumb/questionsLink/loading/errorTitle/lessonErrorTitle` | New question / Edit question / Breadcrumb / Questions / Loading question / Could not load the question / Could not load the lesson | سؤال جديد / تحرير سؤال / مسار التنقل / الأسئلة / جارٍ تحميل السؤال / تعذر تحميل السؤال / تعذر تحميل الدرس |
| `editor.approvedWarning` | Editing the text, options, answer, explanation or points returns this question to pending review and bumps its version. Changing only difficulty, objective or tags keeps it approved. | تعديل نص السؤال أو الخيارات أو الإجابة أو الشرح أو الدرجة يعيده إلى "قيد المراجعة" ويزيد الإصدار. تعديل الصعوبة أو الهدف أو الوسوم فقط لا يغيّر الحالة. |
| `editor.rejectionReason` / `versionBadge` | Rejection reason: {reason} / v{version} | سبب الرفض: {reason} / v{version} |
| `editor.save/create/resubmit` | Save / Create question / Save and resubmit for review | حفظ / إنشاء السؤال / حفظ وإعادة الإرسال للمراجعة |
| `editor.created/saved/resubmitted` | Question created. / Question saved. / Question resubmitted for review. | تم إنشاء السؤال. / تم حفظ السؤال. / أعيد إرسال السؤال للمراجعة. |
| `editor.fields.type/typeLocked/stem/stemFillHint` | Type / The type of a question cannot change. / Question text / Type [[1]] where blank 1 goes, [[2]] for blank 2, and so on. | النوع / لا يمكن تغيير نوع السؤال. / نص السؤال / اكتب [[1]] مكان الفراغ الأول و[[2]] مكان الثاني وهكذا. |
| `editor.fields.explanation/difficulty/objective/noObjective/maxScore/tags/tagsHint` | Explanation (shown after answering) / Difficulty / Objective / No objective / Points / Tags / Separate tags with commas. | الشرح (يظهر بعد الإجابة) / الصعوبة / الهدف / بدون هدف / الدرجة / الوسوم / افصل الوسوم بفواصل. |
| `editor.options.legend/option/correct/add/remove/partialCredit` | Options / Option {number} / Option {number} is correct / Add option / Remove option {number} / Give partial credit | الخيارات / الخيار {number} / الخيار {number} صحيح / إضافة خيار / حذف الخيار {number} / احتساب درجة جزئية |
| `editor.trueFalse.answer/choose/true/false` | Correct answer / Choose / True / False | الإجابة الصحيحة / اختر / صح / خطأ |
| `editor.blanks.legend/accepted/add/remove/unify` | Blanks / Accepted answers for blank {number} (one per line) / Add blank / Remove blank {number} / Treat letter variants (أ إ آ، ة ه، ى ي) as the same | الفراغات / الإجابات المقبولة للفراغ {number} (إجابة في كل سطر) / إضافة فراغ / حذف الفراغ {number} / اعتبار صور الحروف (أ إ آ، ة ه، ى ي) واحدة |
| `editor.short.answerKind/numeric/text/value/tolerance/toleranceMode/absolute/percent/accepted` | Answer type / Number / Text / Correct value / Tolerance / Tolerance type / ± absolute / ± percent / Accepted answers (one per line) | نوع الإجابة / رقمية / نصية / القيمة الصحيحة / السماحية / نوع السماحية / ± مطلق / ± نسبة مئوية / الإجابات المقبولة (إجابة في كل سطر) |
| `editor.errors.maxScore/optionsCount/correctOption/correctOptions/trueFalseAnswer/blanksCount/acceptedAnswers/blankPlaceholder/number/tolerance` | Enter whole points from 1 to 100. / Add at least two options. / Mark exactly one correct answer. / Mark at least one correct answer. / Choose the correct answer. / Add at least one blank. / Add at least one accepted answer. / Place each blank's [[id]] exactly once in the question text. / Enter a number. / Enter a tolerance of 0 or more. | اكتب درجة صحيحة من 1 إلى 100. / أضف خيارين على الأقل. / حدد إجابة صحيحة واحدة فقط. / حدد إجابة صحيحة واحدة على الأقل. / اختر الإجابة الصحيحة. / أضف فراغًا واحدًا على الأقل. / أضف إجابة مقبولة واحدة على الأقل. / ضع [[id]] لكل فراغ مرة واحدة فقط في نص السؤال. / اكتب رقمًا. / اكتب سماحية 0 أو أكثر. |
| `preview.title/tryAnswer/grading/correct/partial/incorrect/score/gradeFailed` | Student preview / Try the answer / Grading… / Correct / Partially correct / Incorrect / Score {score} / {maxScore} / This draft cannot be graded yet. | معاينة الطالب / جرّب الإجابة / جارٍ التصحيح… / إجابة صحيحة / إجابة صحيحة جزئيًا / إجابة خاطئة / الدرجة {score} / {maxScore} / لا يمكن تصحيح هذه المسودة بعد. |
| `view.answerLegend/true/false/blank/blankMarker/yourAnswer` | Your answer / True / False / Blank {number} / ({number}) / Your answer | إجابتك / صح / خطأ / الفراغ {number} / ({number}) / إجابتك |

### Docs
| # | Path | Contract |
|---|------|----------|
| DOC1 | `docs/claude-design-prompt.md` §4 | The questions line becomes: "- `#/admin/questions` list with status, type, subject, teacher, minimum-version and rejection-reason filters (and a lesson filter reached from the lesson editor); each row shows the question, lesson, type, status, version, teacher and rejection reason, with "تعديل" or, for a rejected question, "تعديل وإعادة إرسال". `#/admin/question/:id` and `#/admin/question/new/:lessonId` editor per type with live student preview and "جرّب الإجابة" running the real grader; a rejected question shows its reason and saves with "حفظ وإعادة الإرسال للمراجعة"; editing content of an approved question returns it to pending and bumps the version." |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| Domain `QuestionNotRejected` | `QUESTION_NOT_REJECTED` | `Question.Resubmit` | `BusinessRuleViolationCoreException` | 400 |
| Domain `QuestionRejectionReasonRequired` | `QUESTION_REJECTION_REASON_REQUIRED` | `Question.Reject` | `BusinessRuleViolationCoreException` | 400 |
| `QuestionAnswerInvalid` | `QUESTION_ANSWER_INVALID` | GradeQuestionDraftValidator | validation | 422 |
| `QuestionPageNumberInvalid` | `QUESTION_PAGE_NUMBER_INVALID` | GetQuestionsValidator | validation | 422 |
| `QuestionPageSizeInvalid` | `QUESTION_PAGE_SIZE_INVALID` | GetQuestionsValidator | validation | 422 |
| `QuestionFilterTooLong` | `QUESTION_FILTER_TOO_LONG` | GetQuestionsValidator | validation | 422 |
| `QuestionVersionFilterInvalid` | `QUESTION_VERSION_FILTER_INVALID` | GetQuestionsValidator | validation | 422 |
| `QuestionStatusInvalid` | `QUESTION_STATUS_INVALID` | GetQuestionsValidator | validation | 422 |
| `QuestionTypeInvalid` (existing) | `QUESTION_TYPE_INVALID` | GetQuestionsValidator | validation | 422 |
| `QuestionNotFound`, `LessonNotFound`, `QuestionIdRequired`, `UserNotAuthenticated` (existing) | — | ResubmitQuestion handler and validator | NotFound / validation / Unauthorized | 404 / 422 / 401 |
| Domain `QuestionNotPending`, `QuestionValidatorNotAssigned` (existing) | — | `Question.Reject` | `BusinessRuleViolationCoreException` | 400 |
| Domain `QuestionTypeImmutable` (existing) | — | `Question.Resubmit` (through `Update`) | `BusinessRuleViolationCoreException` | 400 |

Resx (en | ar; no tashkeel and no hamza on alef, matching the existing ar.resx):
- QUESTION_NOT_REJECTED: Only a rejected question can be resubmitted. | لا يمكن اعادة ارسال الا سؤال مرفوض.
- QUESTION_REJECTION_REASON_REQUIRED: Enter the reason for rejecting this question. | اكتب سبب رفض السؤال.
- QUESTION_ANSWER_INVALID: The answer does not match the question type. | الاجابة لا تطابق نوع السؤال.
- QUESTION_PAGE_NUMBER_INVALID: The page number must be 1 or more. | رقم الصفحة يجب ان يكون 1 او اكثر.
- QUESTION_PAGE_SIZE_INVALID: The page size is outside the allowed range. | حجم الصفحة خارج النطاق المسموح.
- QUESTION_FILTER_TOO_LONG: The search text is too long. | نص البحث طويل جدا.
- QUESTION_VERSION_FILTER_INVALID: The version must be 1 or more. | الاصدار يجب ان يكون 1 او اكثر.
- QUESTION_STATUS_INVALID: Choose pending, approved or rejected. | اختر قيد المراجعة او معتمد او مرفوض.

## Domain behaviour
```csharp
// Question.Approval.cs
public void Approve(TeacherSubject assignment)
{
    EnsureValidatorCanDecide(assignment);
    // unchanged from here: Approved, ValidatedBy, ValidatedAt, UpdatedBy, UpdationDate
}

public void Reject(TeacherSubject assignment, string reason)
{
    EnsureValidatorCanDecide(assignment);
    if (string.IsNullOrWhiteSpace(reason)) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionRejectionReasonRequired); }
    var now = DateTimeOffset.UtcNow;
    ValidationStatus = QuestionValidationStatus.Rejected;
    RejectionReason = reason.Trim();
    ValidatedBy = assignment.TeacherId;
    ValidatedAt = now;
    UpdatedBy = assignment.TeacherId;
    UpdationDate = now;
}

private void EnsureValidatorCanDecide(TeacherSubject assignment)
{
    if (ValidationStatus != QuestionValidationStatus.Pending) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionNotPending); }
    if (assignment.IsDeleted || assignment.SubjectId != SubjectId) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionValidatorNotAssigned); }
}

// Question.Editing.cs
public void Resubmit(QuestionType type, QuestionContent content, QuestionMetadata metadata, Lesson lesson, Guid resubmittedBy)
{
    if (ValidationStatus != QuestionValidationStatus.Rejected) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionNotRejected); }
    Update(type, content, metadata, lesson, resubmittedBy);   // type/objective guards; content change → Version + 1 and a revision; status stays Rejected here
    ValidationStatus = QuestionValidationStatus.Pending;
    RejectionReason = null;
    ValidatedBy = null;
    ValidatedAt = null;
    UpdatedBy = resubmittedBy;
    UpdationDate = DateTimeOffset.UtcNow;
}
```
The guard order in `Reject` is: not Pending, then not assigned, then blank reason. In real code braces go on their own lines.

## API surface
| Method | Route (Name) | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/questions` (`GetQuestions`) | `DefaultCodes.ContentManage` | `[FromQuery] QuestionValidationStatus? status, QuestionType? type, Guid? subjectId, Guid? lessonId, Guid? teacherId, int? minVersion, string? rejectionReason, int pageNumber = 1, int pageSize = 20` | `200 PageData<QuestionListItemResult>` |
| PUT | `/api/questions/{questionId:guid}/resubmit` (`ResubmitQuestion`) | `DefaultCodes.ContentManage` | `UpdateQuestionRequest` | `200` (no body) |
| POST | `/api/questions/grade-draft` (`GradeQuestionDraft`) | `DefaultCodes.ContentManage` | `GradeQuestionDraftRequest` | `200 QuestionGradeResult` |
| GET | `/api/teachers` (`GetTeachers`) | `DefaultCodes.UsersManage` | — | `200 List<TeacherResult>` |
| GET | `/api/questions/{questionId:guid}` (existing) | unchanged | — | `QuestionDetailResult` gains `rejectionReason` |

Every action carries `[ProducesResponseType<…>(StatusCodes.Status200OK)]` like its neighbours. Controllers build `QuestionFields` inline, exactly like `CreateQuestion`/`UpdateQuestion`, and pass `request.Answer` for grading.

## Test plan
### Domain
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| R1 | QuestionRejectionTests | `Reject_AssignedTeacherOnPending_SetsRejectedReasonAndValidator` | reason `"  Wrong unit  "` → Rejected, `RejectionReason` "Wrong unit", `ValidatedBy` = teacher id, `ValidatedAt` set, `UpdatedBy` = teacher |
| R2 | QuestionRejectionTests | `Reject_BlankReason_ThrowsQuestionRejectionReasonRequired` (`[Theory]` `""`, `"   "`) | code; still Pending |
| R3 | QuestionRejectionTests | `Reject_AlreadyApproved_ThrowsQuestionNotPending` | code |
| R4 | QuestionRejectionTests | `Reject_AssignmentForOtherSubject_ThrowsQuestionValidatorNotAssigned` | code; still Pending |
| R5 | QuestionRejectionTests | `Resubmit_Rejected_ReturnsToPendingAndClearsRejection` | Pending; `RejectionReason`, `ValidatedBy` and `ValidatedAt` null; `UpdatedBy` = editor |
| R6 | QuestionRejectionTests | `Resubmit_WithContentEdit_BumpsVersionAndAddsRevision` | `Version` 2, 2 revisions, new stem stored |
| R7 | QuestionRejectionTests | `Resubmit_WithoutChanges_KeepsVersion` | Pending, `Version` 1, 1 revision |
| R8 | QuestionRejectionTests | `Resubmit_NotRejected_ThrowsQuestionNotRejected` | Pending question → code; `Version` 1 |
| R9 | QuestionRejectionTests | `Resubmit_TypeChanged_ThrowsQuestionTypeImmutable` | code; still Rejected with its reason |
| R10 | QuestionRejectionTests | `Update_ContentEditOnRejected_StaysRejected` | Rejected, `Version` 2, reason kept (D8 / #64 D8) |
| R11 | QuestionRejectionTests | `Approve_AfterResubmit_Succeeds` | reject, resubmit, approve → Approved |
| N1 | AnswerNormalizerTests | `Normalize_Tashkeel_IsStripped` | `"مَاءٌ"` → `"ماء"` |
| N2 | AnswerNormalizerTests | `Normalize_Tatweel_IsStripped` | `"مـاء"` → `"ماء"` |
| N3 | AnswerNormalizerTests | `Normalize_UnifyOn_UnifiesLetterVariants` | `"أإآٱ ة ى"` → `"اااا ه ي"` |
| N4 | AnswerNormalizerTests | `Normalize_UnifyOff_KeepsLetterVariants` | `"إلى"` unchanged |
| N5 | AnswerNormalizerTests | `Normalize_ArabicIndicAndExtendedDigits_BecomeAscii` | `"٢٠ ۳"` → `"20 3"` |
| N6 | AnswerNormalizerTests | `Normalize_Whitespace_IsCollapsedAndTrimmed` | `"  a \t  b  "` → `"a b"` |
| N7 | AnswerNormalizerTests | `Normalize_LatinLetters_AreLowerCased` | `"Newton"` → `"newton"` |
| N8 | AnswerNormalizerTests | `Normalize_Null_ReturnsEmpty` | `""` |
| C1 | ChoiceGraderTests | `GradeMcq_CorrectOption_ReturnsOne` | 1 |
| C2 | ChoiceGraderTests | `GradeMcq_WrongOrMissingOption_ReturnsZero` (`[Theory]` `"a"`, `null`) | 0 |
| C3 | ChoiceGraderTests | `GradeTrueFalse_MatchingValue_ReturnsOne` | 1 |
| C4 | ChoiceGraderTests | `GradeTrueFalse_WrongOrMissingValue_ReturnsZero` (`[Theory]` `true`, `null`) | spec false → 0 |
| C5 | ChoiceGraderTests | `GradeMulti_ExactSetWithoutPartialCredit_ReturnsOne` | order-independent → 1 |
| C6 | ChoiceGraderTests | `GradeMulti_SubsetWithoutPartialCredit_ReturnsZero` | 0 |
| C7 | ChoiceGraderTests | `GradeMulti_PartialCredit_ReturnsRightMinusWrongOverTotal` | correct {a,b,c}, selected {a,b,d} → 1/3 |
| C8 | ChoiceGraderTests | `GradeMulti_PartialCreditMoreWrongThanRight_FloorsAtZero` | 0 |
| C9 | ChoiceGraderTests | `GradeMulti_DuplicateSelections_CountOnce` | `["a","a"]` vs {a} → 1 |
| X1 | TextGraderTests | `GradeFill_AllBlanksRight_ReturnsOne` | 1 |
| X2 | TextGraderTests | `GradeFill_OneOfTwoBlanksRight_ReturnsHalf` | 0.5 |
| X3 | TextGraderTests | `GradeFill_SpellingVariantWithUnifyOn_Matches` | accepted `"القاهرة"`, answer `"القاهره"` → 1 |
| X4 | TextGraderTests | `GradeFill_SpellingVariantWithUnifyOff_DoesNotMatch` | → 0 |
| X5 | TextGraderTests | `GradeFill_EmptyAnswer_DoesNotMatch` | answer `"  "` → 0 |
| X6 | TextGraderTests | `GradeShort_NumericWithinAbsoluteTolerance_ReturnsOne` | 9.8 ± 0.1, answer `"٩٫٧٥"` → 1 |
| X7 | TextGraderTests | `GradeShort_NumericOutsideTolerance_ReturnsZero` | `"9.6"` → 0 |
| X8 | TextGraderTests | `GradeShort_NumericWithinPercentTolerance_ReturnsOne` | 200 ± 5 %, `"209"` → 1 |
| X9 | TextGraderTests | `GradeShort_NumericNotANumber_ReturnsZero` | `"9.8 m/s"` → 0 |
| X10 | TextGraderTests | `GradeShort_TextAcceptedAfterNormalisation_ReturnsOne` | accepted `"ماء"`, answer `" مَاء "` → 1 |
| X11 | TextGraderTests | `GradeShort_TextNotAccepted_ReturnsZero` | 0 |
| G1 | QuestionGraderTests | `Grade_McqCorrect_ReturnsFullScoreAndCorrect` | maxScore 2 → Score 2, Normalised 1, Correct |
| G2 | QuestionGraderTests | `Grade_MultiPartial_ReturnsRoundedPartialScore` | 1/3, maxScore 2 → Score 0.67, Normalised 0.3333, Partial |
| G3 | QuestionGraderTests | `Grade_WrongAnswer_ReturnsZeroAndIncorrect` | 0, Incorrect |
| G4 | QuestionGraderTests | `Grade_FillHalf_ReturnsPartial` | Normalised 0.5, Partial |
| G5 | QuestionGraderTests | `Grade_ShortText_UsesTextGrader` | Correct |

### Application
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| V1 | GetQuestionsValidatorTests | `Validate_DefaultQuery_HasNoErrors` | valid |
| V2 | GetQuestionsValidatorTests | `Validate_PageNumberZero_HasQuestionPageNumberInvalid` | code |
| V3 | GetQuestionsValidatorTests | `Validate_PageSizeOutOfRange_HasQuestionPageSizeInvalid` (`[Theory]` 0, 101) | code |
| V4 | GetQuestionsValidatorTests | `Validate_UndefinedStatus_HasQuestionStatusInvalid` | `(QuestionValidationStatus)9` → code |
| V5 | GetQuestionsValidatorTests | `Validate_UndefinedType_HasQuestionTypeInvalid` | code |
| V6 | GetQuestionsValidatorTests | `Validate_MinVersionZero_HasQuestionVersionFilterInvalid` | code |
| V7 | GetQuestionsValidatorTests | `Validate_RejectionReasonOverCap_HasQuestionFilterTooLong` | 201 chars → code |
| F1 | GetQuestionsFilterTests | `Build_NoFilters_MatchesEveryQuestion` | pending, approved and rejected all match (`Build(query).Compile()`) |
| F2 | GetQuestionsFilterTests | `Build_Status_MatchesOnlyThatStatus` | Rejected → only the rejected one |
| F3 | GetQuestionsFilterTests | `Build_Type_MatchesOnlyThatType` | Mcq matches; TrueFalse does not |
| F4 | GetQuestionsFilterTests | `Build_SubjectAndLesson_MatchOnlyThatScope` | matching ids pass; `Guid.NewGuid()` fails |
| F5 | GetQuestionsFilterTests | `Build_TeacherId_MatchesQuestionsThatTeacherDecided` | `builder.Teacher.Id` → approved and rejected; pending excluded |
| F6 | GetQuestionsFilterTests | `Build_MinVersion_MatchesVersionAtLeast` | a question edited to v2 matches `MinVersion = 2`; a v1 does not |
| F7 | GetQuestionsFilterTests | `Build_RejectionReason_MatchesCaseInsensitiveContains` | reason "Wrong UNIT", filter "  unit " → match; "time" → none |
| H1 | GetQuestionsHandlerTests | `Handle_Page_MapsLessonAndTeacherNamesAndPaging` | `FindPaginatedAsync` returns a page of 1 rejected question (TotalItems 1, TotalPages 1); lesson and user `FindAsync` return the builder's lesson and teacher → item `LessonName` "Newton's laws", `TeacherName` "Teacher", `RejectionReason`, `Type` "Mcq", `ValidationStatus` "Rejected", `PageNumber` 1, `TotalItems` 1 |
| H2 | GetQuestionsHandlerTests | `Handle_UndecidedQuestion_HasNoTeacherName` | pending → `TeacherName` null, `ValidatedBy` null |
| G-GQ3 | GetQuestionHandlerTests | `Handle_RejectedQuestion_ReturnsRejectionReason` | `RejectionReason` "Wrong unit", `ValidationStatus` "Rejected" |
| RV1 | ResubmitQuestionValidatorTests | `Validate_ValidCommand_HasNoErrors` | valid |
| RV2 | ResubmitQuestionValidatorTests | `Validate_EmptyQuestionId_HasQuestionIdRequired` | code |
| RV3 | ResubmitQuestionValidatorTests | `Validate_InvalidFields_SurfacesFieldCode` | empty stem → `QUESTION_STEM_REQUIRED` |
| RH1 | ResubmitQuestionHandlerTests | `Handle_RejectedQuestion_ReturnsToPendingAndSaves` | Pending, reason null, save `Received(1)` |
| RH2 | ResubmitQuestionHandlerTests | `Handle_QuestionNotFound_ThrowsQuestionNotFound` | type, code, save `DidNotReceive` |
| RH3 | ResubmitQuestionHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | type, code, save `DidNotReceive` |
| RH4 | ResubmitQuestionHandlerTests | `Handle_PendingQuestion_ThrowsQuestionNotRejected` | `BusinessRuleViolationCoreException` code, save `DidNotReceive` |
| RH5 | ResubmitQuestionHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | type, code, save `DidNotReceive` |
| GV1 | GradeQuestionDraftValidatorTests | `Validate_ValidDraftAndAnswer_HasNoErrors` | mcq + `{"optionId":"b"}` |
| GV2 | GradeQuestionDraftValidatorTests | `Validate_InvalidDraft_SurfacesSchemaCode` | unknown correct option → `QUESTION_CORRECT_OPTION_INVALID` |
| GV3 | GradeQuestionDraftValidatorTests | `Validate_AnswerNotAnObject_HasQuestionAnswerInvalid` | `"b"` → code |
| GV4 | GradeQuestionDraftValidatorTests | `Validate_AnswerWrongShape_HasQuestionAnswerInvalid` | `{"optionId":5}` → code |
| GV5 | GradeQuestionDraftValidatorTests | `Validate_EmptyAnswerObject_HasNoErrors` | `{}` → valid |
| GV6 | GradeQuestionDraftValidatorTests | `Validate_MissingType_SkipsAnswerRule` | type null → `QUESTION_TYPE_REQUIRED` and not `QUESTION_ANSWER_INVALID` |
| GH1 | GradeQuestionDraftHandlerTests | `Handle_CorrectMcq_ReturnsCorrectWithMaxScore` | Score 1, NormalisedScore 1, Outcome "Correct", MaxScore 1 |
| GH2 | GradeQuestionDraftHandlerTests | `Handle_FillOneOfTwo_ReturnsPartial` | maxScore 2 → Score 1, Outcome "Partial" |
| TH1 | GetTeachersHandlerTests | `Handle_Teachers_MapsIdAndDisplayName` | the repository returns 2 teachers → 2 results with id and name, in repository order |

### Integration
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| L1 | QuestionListEndpointTests | `Get_ByLesson_ReturnsLessonQuestionsWithLessonName` | 2 seeded questions → 200; `totalItems` 2; `items[*].lessonName` equals the lesson name; `type` "Mcq" |
| L2 | QuestionListEndpointTests | `Get_RejectedWithReasonFilter_ReturnsTeacherNameAndReason` | seed pending + rejected (seeded teacher, reason "Wrong unit conversion") → `?lessonId&status=Rejected&rejectionReason=UNIT` returns 1 item: `validationStatus` "Rejected", `teacherName` "Teacher", `rejectionReason` |
| L3 | QuestionListEndpointTests | `Get_ByTeacher_ReturnsQuestionsThatTeacherDecided` | 2 seeded teachers → filtered by one → only that one's question |
| L4 | QuestionListEndpointTests | `Get_MinVersion_ReturnsOnlyEditedQuestions` | one question edited through PUT (v2) → `minVersion=2` returns only it |
| L5 | QuestionListEndpointTests | `Get_ByType_ReturnsOnlyThatType` | one Mcq and one TrueFalse created through POST → `type=TrueFalse` returns 1 |
| L6 | QuestionListEndpointTests | `Get_PageSizeOverCap_Returns422QuestionPageSizeInvalid` | 422 + code |
| L7 | QuestionListEndpointTests | `Get_Teacher_Returns403` | 403 |
| L8 | QuestionListEndpointTests | `Get_Anonymous_Returns401` | 401 |
| S1 | QuestionResubmitEndpointTests | `Put_RejectedWithEdit_ReturnsPendingVersionTwoAndAudits` | 200; DB Pending, `Version` 2, `RejectionReason` null, `ValidatedBy` null; `Question.Resubmit` audit Success |
| S2 | QuestionResubmitEndpointTests | `Put_PendingQuestion_Returns400QuestionNotRejected` | 400 + code; DB unchanged |
| S3 | QuestionResubmitEndpointTests | `Put_UnknownQuestion_Returns404QuestionNotFound` | 404 + code |
| S4 | QuestionResubmitEndpointTests | `Put_Teacher_Returns403` | 403; DB still Rejected |
| S5 | QuestionResubmitEndpointTests | `Get_RejectedQuestion_ReturnsRejectionReason` | GET detail → `rejectionReason` "Wrong unit conversion", `validationStatus` "Rejected" |
| DG1 | QuestionGradeDraftEndpointTests | `Post_CorrectMcqAnswer_ReturnsCorrect` | 200; `outcome` "Correct", `score` 1, `maxScore` 1 |
| DG2 | QuestionGradeDraftEndpointTests | `Post_EachV1Type_GradesAnswer` (`[Theory]`, 5 rows: type, stem, body, spec, answer JSON, expected outcome) | Multi partial `{"correctOptionIds":["a","b"],"partialCredit":true}` + `{"optionIds":["a"]}` → "Partial"; TrueFalse → "Correct"; Fill `{"blanks":[{"id":"1","text":"٢٠"}]}` with accepted `["20"]` → "Correct"; Short numeric `{"text":"9.75"}` → "Correct"; Mcq wrong → "Incorrect" |
| DG3 | QuestionGradeDraftEndpointTests | `Post_InvalidDraft_Returns422QuestionCorrectOptionInvalid` | 422 + code |
| DG4 | QuestionGradeDraftEndpointTests | `Post_AnswerWrongShape_Returns422QuestionAnswerInvalid` | 422 + code |
| DG5 | QuestionGradeDraftEndpointTests | `Post_Teacher_Returns403` | 403 |
| DG6 | QuestionGradeDraftEndpointTests | `Post_Anonymous_Returns401` | 401 |
| TE1 | TeachersEndpointTests | `Get_Admin_ReturnsTeachersOnly` | a seeded teacher is contained (`id`, `displayName`); seeded admin and student ids are absent |
| TE2 | TeachersEndpointTests | `Get_Teacher_Returns403` | 403 |
| TE3 | TeachersEndpointTests | `Get_Anonymous_Returns401` | 401 |
| O1 | OpenApiEndpointTests | `Get_OpenApiDocument_DescribesQuestionEnumsAsStrings` | `components.schemas.QuestionType.enum` = `["Mcq","Multi","TrueFalse","Fill","Short"]`; `QuestionValidationStatus.enum` contains "Rejected" |

### Web
| # | Test file | `it(...)` | Asserts |
|---|-----------|-----------|---------|
| WE1 | questionEditorSchema.test.ts | `accepts a complete multiple-choice question` | success |
| WE2 | questionEditorSchema.test.ts | `requires question text` | stem `<p></p>` → issue path `stem`, message `validation.required` |
| WE3 | questionEditorSchema.test.ts | `accepts question text that is only a formula` | success |
| WE4 | questionEditorSchema.test.ts | `rejects points outside 1 to 100` (`it.each` `'0'`, `'101'`, `'1.5'`, `'abc'`) | path `maxScore` |
| WE5 | questionEditorSchema.test.ts | `requires at least two options` | path `options`, `…optionsCount` |
| WE6 | questionEditorSchema.test.ts | `requires text on every option` | path `options.1.text` |
| WE7 | questionEditorSchema.test.ts | `requires exactly one correct option for multiple choice` (`it.each` 0 and 2 correct) | `…correctOption` |
| WE8 | questionEditorSchema.test.ts | `requires a correct option for multiple answers` | `…correctOptions` |
| WE9 | questionEditorSchema.test.ts | `requires the true-or-false answer` | path `trueFalseAnswer` |
| WE10 | questionEditorSchema.test.ts | `requires accepted answers for each blank` | path `blanks.0.acceptedAnswers` |
| WE11 | questionEditorSchema.test.ts | `requires each blank placeholder exactly once` (`it.each` missing, twice) | path `stem`, `…blankPlaceholder` |
| WE12 | questionEditorSchema.test.ts | `requires a numeric value and a non-negative tolerance` | paths `numericValue` and `tolerance` |
| WE13 | questionEditorSchema.test.ts | `requires accepted answers for a text short answer` | path `acceptedAnswers` |
| WE14 | questionEditorSchema.test.ts | `ignores the sections of other types` | a Short text question with empty options → success |
| WS1 | questionListSearchSchema.test.ts | `parses every filter` | valid object round-trips |
| WS2 | questionListSearchSchema.test.ts | `drops invalid values` | status `'Foo'`, page `0`, lessonId `'x'`, minVersion `0` → undefined |
| WF1 | questionListFiltersSchema.test.ts | `accepts empty filters` | success |
| WF2 | questionListFiltersSchema.test.ts | `rejects a minimum version that is not a positive whole number` (`it.each` `'0'`, `'abc'`) | `…errors.minVersion` |
| WF3 | questionListFiltersSchema.test.ts | `rejects a rejection-reason search over 200 characters` | `…errors.rejectionReasonTooLong` |
| WM1 | questionValues.test.ts | `starts a new question with four options, one blank and one point` | the `emptyQuestionValues('Mcq')` fields |
| WM2 | questionValues.test.ts | `builds the request for each type` (`it.each` 5 rows) | exact `body`/`gradingSpec` per D3 of #64 (numeric short: `{value: 9.8, tolerance: 0.1, toleranceMode: 'absolute'}`) |
| WM3 | questionValues.test.ts | `splits tags and accepted answers and drops empty entries` | `' a , ,b '` → `['a','b']`; `'x\n\n y '` → `['x','y']` |
| WM4 | questionValues.test.ts | `sends no objective when none is chosen` | `objectiveId: null` |
| WM5 | questionValues.test.ts | `round-trips a stored question of each type` (`it.each` 5 details) | `toQuestionRequest(toQuestionValues(detail))` body and spec equal the detail's |
| WM6 | questionValues.test.ts | `keeps defaults when the stored body is unreadable` | body `{}` for Mcq → 4 empty options |
| WM7 | questionValues.test.ts | `picks the next free option and blank ids` | `['a','c']` → `'b'`; `['1','2']` → `'3'` |
| WQ1 | studentQuestion.test.ts | `builds the student view from the form values` | options, blankIds, answerKind (Short only) |
| WQ2 | studentQuestion.test.ts | `replaces blank placeholders with numbered markers` | `'<p>v = [[1]] m/s</p>'` → contains `(1)` inside `<u>`; `[[1]]` gone |
| WQ3 | studentQuestion.test.ts | `builds the answer payload for each type` (`it.each` 5) | D3 shapes |
| WP1 | questionListParams.test.ts | `maps page and page size` | defaults page 1, size 20 |
| WP2 | questionListParams.test.ts | `omits unset filters` | exact object |
| WP3 | questionListParams.test.ts | `detects active filters` | `{page:2}` false; `{lessonId}` true |
| WP4 | questionListParams.test.ts | `coerces page numbers` | string numbers → numbers |
| WX1 | stemExcerpt.test.ts | `strips markup and collapses spaces` | `'<p>2 +  2</p><p>= ?</p>'` → `'2 + 2= ?'` (text content) |
| WX2 | stemExcerpt.test.ts | `truncates long text with an ellipsis` | 100 chars → 81 chars ending with `…` |
| WV1 | QuestionView.test.tsx | `reports the chosen option for multiple choice` | click radio "4" → last `onAnswerChange` call has `optionIds: ['b']` |
| WV2 | QuestionView.test.tsx | `toggles options for multiple answers` | two checkboxes → `['a','c']`, then unclick → `['c']` |
| WV3 | QuestionView.test.tsx | `offers true and false` | radios "True" and "False"; click False → `trueFalse: false` |
| WV4 | QuestionView.test.tsx | `numbers the blanks and labels their inputs` | text "(1)" visible; typing in "Blank 1" → `blanks: { '1': '20' }` |
| WV5 | QuestionView.test.tsx | `uses a left-to-right input for numeric answers` | "Your answer" has `dir="ltr"` and `inputmode="decimal"` |
| WV6 | QuestionView.test.tsx | `renders in Arabic` | `lng: 'ar'` → "صح" and "خطأ" visible |
| WV7 | QuestionView.test.tsx | `has no axe violations` | axe clean |
| WL1 | QuestionListPage.test.tsx | `shows questions after loading` | status "Loading questions", then the row has the excerpt, lesson name, "Multiple choice", "Rejected", "v2", teacher name and reason |
| WL2 | QuestionListPage.test.tsx | `links rejected questions to edit and resubmit` | the rejected row link "Edit and resubmit" has href `/admin/question/<id>`; the pending row has "Edit" |
| WL3 | QuestionListPage.test.tsx | `shows the empty state when there are no questions` | "No questions yet." |
| WL4 | QuestionListPage.test.tsx | `offers to clear filters when nothing matches` | route with `?status=Approved` and empty items → "No questions match these filters."; click "Clear filters" → rows shown |
| WL5 | QuestionListPage.test.tsx | `shows an error and recovers on retry` | alert "Could not load the questions" → Retry → row |
| WL6 | QuestionListPage.test.tsx | `sends the chosen filters` | select status "Rejected" and a teacher, set minVersion "2", reason "unit", Apply → the last request URL has `status=Rejected&teacherId=…&minVersion=2&rejectionReason=unit&pageNumber=1` |
| WL7 | QuestionListPage.test.tsx | `offers a new question when filtered to a lesson` | `?lessonId=<guid>` → link "New question in this lesson" with href `/admin/question/new/<guid>`; the request has `lessonId` |
| WL8 | QuestionListPage.test.tsx | `moves to the next page` | totalPages 2 → click "Next page" → request `pageNumber=2` |
| WL9 | QuestionListPage.test.tsx | `renders in Arabic` | `document.documentElement.dir` "rtl"; heading "بنك الأسئلة" |
| WE-P1 | QuestionEditorPage.test.tsx | `shows an approved question with its warning` | badge "Approved", "v3", the approved warning text, type select disabled, option text visible in the preview region "Student preview" |
| WE-P2 | QuestionEditorPage.test.tsx | `saves the question` | change Points to 2 → Save → PUT body `maxScore: 2`, the other fields unchanged; toast "Question saved." |
| WE-P3 | QuestionEditorPage.test.tsx | `resubmits a rejected question` | the reason "Rejection reason: Wrong unit" is visible; the button "Save and resubmit for review" → PUT `/resubmit` receives the body; toast "Question resubmitted for review." |
| WE-P4 | QuestionEditorPage.test.tsx | `shows a server error on its field` | PUT → 422 `{ code: 'QUESTION_OBJECTIVE_NOT_IN_LESSON' }` (the error body shape the other page tests use; `http.ts` splits comma-separated codes) → the resx message under Objective |
| WE-P5 | QuestionEditorPage.test.tsx | `test-grades the chosen answer with the real grader` | choose "4" in the preview → "Try the answer" → the grade request body has `type: 'Mcq'`, `answer: { optionId: 'b' }`; "Correct" and "Score 1 / 1" shown |
| WE-P6 | QuestionEditorPage.test.tsx | `shows partial credit` | grade response `Partial`, score 0.5, max 1 → "Partially correct", "Score 0.5 / 1" |
| WE-P7 | QuestionEditorPage.test.tsx | `explains why the draft cannot be graded` | grade → 422 `{ code: 'QUESTION_CORRECT_OPTION_INVALID' }` → alert has "This draft cannot be graded yet." and the resx text |
| WE-P8 | QuestionEditorPage.test.tsx | `shows an error and recovers on retry` | GET question 500 → alert "Could not load the question" → Retry → form |
| WE-P9 | QuestionEditorPage.test.tsx | `renders in Arabic` | `dir` rtl, heading "تحرير سؤال" |
| WN1 | NewQuestionPage.test.tsx | `creates a true-or-false question and opens it` | choose type "True or false"; fill the stem (click the "Question text" textbox, then `user.paste('Mass is a vector.')`, falling back to `user.type` if ProseMirror ignores paste in jsdom); choose "False"; Create → the POST body has `lessonId`, `type: 'TrueFalse'`, `body: {}`, `gradingSpec: { correctAnswer: false }`; toast "Question created."; the heading "Edit question" appears (navigated) |
| WN2 | NewQuestionPage.test.tsx | `shows the fields of each type and updates the preview` | Mcq: "Option 1"…"Option 4" textboxes; Fill: "Accepted answers for blank 1 (one per line)" and the preview input "Blank 1"; Short: "Answer type", "Correct value", preview "Your answer"; TrueFalse: preview radios "True"/"False" |
| WN3 | NewQuestionPage.test.tsx | `shows required errors on submit` | Create with empty values → "This field is required." (stem) and "Mark exactly one correct answer." |
| WN4 | NewQuestionPage.test.tsx | `adds and removes options within the limits` | "Remove option 1" is enabled with 4 options; after removing twice, "Remove option 1" is disabled; "Add option" adds "Option 3" |
| WN5 | NewQuestionPage.test.tsx | `shows an error when the lesson cannot load` | GET lesson 500 → "Could not load the lesson" → Retry → "New question" heading |
| W-LE1 | LessonEditorPage.test.tsx | `links to a new question and to the lesson questions` | "New question" href `/admin/question/new/l1`; "Lesson questions" href `/admin/questions?lessonId=l1` |
| WPG1 | Pagination.test.tsx | `disables previous on the first page and reports page changes` | "Previous page" disabled; click "Next page" → `onPageChange(2)` |

The existing audit, content and rich-text tests must pass unchanged. The only modified tests are `AppDbContextTests` (migration list) and the additions listed above (O1, G-GQ3, W-LE1).

## Definition of done
- [ ] Every file in Files to create exists with the exact path, namespace, type name and signature. No other new files. `AuditLogPagination.tsx` is deleted.
- [ ] `Question.Reject` guards Pending, then assignment, then blank reason, and stores the trimmed reason and the deciding teacher. `Resubmit` needs Rejected, goes through `Update`, then sets Pending and clears reason, validator and validation date. `Approve` behaviour is unchanged (its tests are untouched and green).
- [ ] Migration `AddQuestionRejectionReason` only adds the nullable `text` column; the snapshot is regenerated; `AppDbContextTests` lists it ninth.
- [ ] Graders and `AnswerNormalizer` live in `Elmanhg.Domain.Questions.Grading`, use no regex, and implement D4–D7 exactly. `QuestionGrade` rounds to 2 and 4 decimals, away from zero.
- [ ] `POST /api/questions/grade-draft` validates with `QuestionFieldsValidator` plus `QUESTION_ANSWER_INVALID`, grades the canonicalised draft, saves nothing, and is not audited.
- [ ] `GET /api/questions` filters by status, type, subject, lesson, teacher (`ValidatedBy`), `minVersion` and rejection-reason contains. It is paged (cap from `Content:QuestionListMaxPageSize`), ordered by `UpdationDate` desc, and loads lesson and teacher names with one query each.
- [ ] `PUT /api/questions/{id}/resubmit` is audited as `Question.Resubmit`. `GET /api/teachers` returns only teachers under `Users.Manage`. Every new endpoint has a policy (`EndpointAuthorizationTests` green).
- [ ] `Program.cs` registers the string-enum converter for HTTP JSON options; `api/openapi/v1.json` shows string enums; the generated web model has string unions.
- [ ] `IUserRepository`/`UserRepository` are copied from Morabh with no custom methods and are registered in DI.
- [ ] The 2 new options are in `ContentOptions`, `appsettings.example.json` and `ApiFactory`. `dotnet test api/ -c Release` passes with `api/Elmanhg.Api/appsettings.json` moved aside.
- [ ] All 8 new codes are in both resx files. All 44 question codes are in the web `common` `errors` (en and ar, copied from resx).
- [ ] Web: `#/admin/questions` (list, filters in the URL, pagination, loading, empty, no-results, error and retry), `#/admin/question/$questionId` and `#/admin/question/new/$lessonId` exist. There are editors for all five types; a Rejected question shows its reason and saves through resubmit; the type is locked on edit.
- [ ] `QuestionView` is exported from the `questions` barrel and renders all five types. The preview updates live from the form, and "Try the answer" calls `gradeQuestionDraft` and shows Correct, Partial or Incorrect with the score.
- [ ] `RichTextEditor` has the `compact` prop and an optional `onUploadImage` (no image tool when absent). Lesson editor behaviour is unchanged. The lesson editor links to a new question and to the lesson's questions.
- [ ] No arbitrary Tailwind values, hex colours or physical-direction utilities in `src/features/questions`. Every visible string is in `questions` en and ar. Admin numbers use Latin digits (D25).
- [ ] Every test row above exists with that exact name and assertion; the only modified existing tests are the ones listed.
- [ ] `dotnet build` has zero new warnings; `dotnet test` is green; `dotnet format --verify-no-changes` is clean outside `core-libraries`.
- [ ] `npm --prefix web run gen:api` produces no diff after commit. `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .`, `npx vitest run` and `npx vite build` all exit 0. `routeTree.gen.ts` is regenerated.
- [ ] Postman has "List teachers" and the three new Questions requests, in the stated order.
- [ ] Docs: `question-schemas.md` (Answer shapes, Grading, Versioning, Validation status), `PRD.md` §5.3 and §6, `audit-log.md`, `claude-design-prompt.md` §4 (DOC1 and the lesson line) and `rich-text.md` are updated as specified.
- [ ] The guard grep is clean: no `DateTime.Now/UtcNow`, `.Result`, `FromSqlRaw`, `async void` or `?? throw` in new code. There is no new `catch` in new code.
