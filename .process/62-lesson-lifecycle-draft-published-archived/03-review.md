VERDICT: APPROVED

# Review — [E2.S3] Lesson lifecycle: draft, published, archived (#62)

## Blocking
None.

## Non-blocking
- `postman/elmanhg.postman_collection.json:573` — if you run the Lessons folder top to bottom, "Archive lesson" follows "Unpublish lesson". The lesson is Draft by then, so Archive returns 400 `LESSON_NOT_PUBLISHED` and the request's "status is 200" test fails. The rest of the collection is not built for a straight run either ("Delete subject" comes before "Create unit"), so this is not a sync gap. A fix: put Archive before Unpublish, or add a Publish request before Archive.
- `web/src/features/content/components/LessonItem.tsx:41` — no test clicks "move up". W13 only checks that the button is disabled on the first row. A test that moves l2 up and asserts `{ position: 1 }` is worth adding later.
- `api/Elmanhg.Tests/Application/Features/Lessons/ReorderLesson/ReorderLessonHandlerTests.cs:340` — `FindAsync` is stubbed with `Arg.Any` for the predicate, so a wrong sibling filter would still pass at unit level. Integration test I13 (`LessonManagementEndpointTests.cs:225`) covers it against a real database.
- `api/Elmanhg.Application/Subjects/GetSubject/GetSubjectHandler.cs:25` — no unit test covers a missing role claim. A null claim correctly counts as non-admin (fail closed, per D7), but nothing locks that in.
- `api/Elmanhg.Domain/Lessons/Lesson.cs` (108 lines) and `api/Elmanhg.Api/Controllers/Lessons/LessonsController.cs` (118 lines) are a little over the ~100-line guideline. The plan (D13 and API surface) puts these members there. Accepted.
- `web/src/features/content/components/RichTextEditor.test.tsx` (unchanged file) failed once in my first `--coverage` run. It passed alone and in a second full run, so it looks flaky, not a regression.

## Verified
- **Intent:** everything in the Goal is present.
  - Publish, unpublish, archive, reorder and soft-delete work end to end, and each is audited.
  - Publish stamps `PublishedAt`, and entering or leaving Published raises an event.
  - Actions show in both the tree and the editor, each behind an inline confirmation.
  - `GetSubject` counts only Published lessons for non-admins.
- **Transition table D2:** matches exactly in `Lesson.Lifecycle.cs:8-55`.
  - Every method runs guard, then mutate, then stamp, then raise.
  - Unpublish from Archived raises no event.
  - `PublishedAt` is kept on Unpublish and Archive.
- **D8 delete:** `Lesson.Delete` (`Lesson.cs:92-107`) refuses a Published lesson and cascades the soft delete to loaded objectives. `DeleteLessonHandler` loads with objectives and tracking on.
- **D9 reorder:** `ReorderLessonHandler.cs:24-30` follows the plan step for step, including the position clamp.
- **Contract fidelity:**
  - All 32 files in *Files to create* exist, plus the W-a to W-i web files. Nothing extra was created apart from the generated `lessonPositionRequest.ts`.
  - Signatures, routes, route names and policies (`LessonsPublish` for the lifecycle verbs, `ContentManage` for reorder and delete) match the API surface.
  - `LessonPositionRequest` was appended to `Requests.cs`.
  - The migration contains only the nullable `timestamptz` AddColumn and its DropColumn. The snapshot adds 3 lines.
- **Skill §9 and §8 (dotnet):**
  - Commands are sealed records implementing `IAuditableCommand`. Handlers and validators are sealed.
  - Namespaces are file-scoped and there are no comments.
  - Every non-controller await has `.ConfigureAwait(false)`. Controllers follow the existing convention and skip it.
  - Every command handler starts with the user guard and ends with exactly one `SaveChangesAsync`. No `try`/`catch`.
  - Timestamps use `DateTimeOffset`. There are no ad-hoc `IsDeleted` checks.
  - The guard grep is clean on the diff and on every new `.cs` file.
- **Skill (react) §6:**
  - Every visual value is a token (`rounded-md`, `bg-surface`, `border-border`, `text-ui`). No physical-direction utilities.
  - Every string goes through i18n in both en and ar, with interpolation for names.
  - Mutations use Orval-generated hooks and live in `hooks/`. Tests use MSW.
- **Error codes:** all 6 are in both resx files and both `shared/i18n` files, with the plan's text word for word.
- **Deviations:**
  - The one extra entry in `AppDbContextTests.cs:23` is the accepted recurring migration-list pattern.
  - The extra private seed helpers are test-only.
  - Nothing is hidden.
- **Postman:**
  - All 5 requests are present with the right method, URL and `{{lessonId}}`.
  - Auth is inherited from the collection's Bearer auth, like the sibling requests.
  - Reorder has the body `{"position": 1}`.
  - The folder description was appended word for word.
- **Docs sync:**
  - PRD §5.2 has the Transitions paragraph word for word.
  - `docs/audit-log.md` has the 5 new rows.
  - `docs/claude-design-prompt.md` §4 has the admin bullet as specified.
  - No divergence.
- **OpenAPI and generated client:** the OpenAPI file has the 5 new operations and the `LessonPositionRequest` schema. The generated client has the 5 hooks, the MSW handlers and the zod schemas.
- **Builds and tests (re-run by me):**
  - `dotnet build api/` → 0 warnings, 0 errors.
  - `dotnet test api/ -c Release` with `appsettings.json` moved aside → 535 passed, 0 failed. The file was restored afterwards.
  - `npm --prefix web run build` → built. Only the existing >500 kB chunk warning.
  - `npm --prefix web test -- --run --coverage` → 46 files and 237 tests passed on the second run. The first run had the one unrelated flake noted above.

## Test quality
- **LessonLifecycleTests (A1–A16):** these constrain the code.
  - Every transition and guard is covered, with its error code.
  - Event assertions use `Equal(...)` after `ClearDomainEvents`, so a missing, extra or wrong event fails.
  - A5 asserts `PublishedAt` is kept. A9 and A16 assert the state did not change after a refused transition.
- **Publish, Unpublish, Archive and Delete handler tests:** these constrain the code.
  - They use real `Lesson` aggregates, not substitute return values.
  - Success paths assert the state change plus `Received(1)`. Throwing paths assert exception type, code and `DidNotReceive()`.
- **ReorderLessonHandlerTests:** these constrain the renumbering and the position clamp. The sibling predicate is not constrained here (see Non-blocking); I13 covers it.
- **Validator tests (A37–A39):** every rule has a failing case.
- **GetSubjectHandlerTests A40:** this discriminates. Stubs for `true` and `false` return different counts, so passing the wrong flag fails the test.
- **Integration tests (I1–I22):** these cover real HTTP status codes, database state, audit rows, events dispatched through MediatR (D18), and the 401/403 policy checks. I21 and I22 seed all three states and assert 1 for a student versus 3 for an admin.
- **Web tests (W1–W21):**
  - Tree and editor tests go through `renderApp` and assert on the payloads the MSW handlers receive.
  - W18 proves the refetch after invalidation, because the badge and the offered actions change.
  - W19 and W21 cover navigation after a delete and staying on the page after a refused delete.
  - None is vacuous.
