VERDICT: APPROVED

# Review — [E9.S1] Thread creation with attached context and quota (#94)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Application/TeacherThreads/CreateTeacherThread/CreateTeacherThreadValidator.cs:23-28` — the image is checked by extension and by the client-sent content type, not by magic bytes. A non-image renamed `photo.png` with `Content-Type: image/png` is stored. Impact is limited: the middleware serves it with a fixed type taken from the extension (`TeacherThreadMediaMiddleware.cs:30`), with `nosniff`, and only to authorised viewers, and `ThreadImage.tsx:13` rejects non-image blobs. The result is a broken image, not script execution. The plan did not require a signature check (Decision 8), and `LessonImageFormats` has none either. A shared signature check in `Core.Validation` would be a worthwhile follow-up.
- `api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadContextResolver.Questions.cs:11-18` — an attempt context accepts any of the student's own attempts, including an exam attempt from an exam still in progress (API only; the UI links quiz attempts only). A teacher could be asked about a live exam question. The 24h SLA makes this a weak cheating vector, but #95 may want to restrict it to quiz attempts or finished sessions.
- `docs/ask-teacher.md:85` says "names only" for `GET /api/teacher-threads/context`, but a `questionId` or `attemptId` preview also returns `questionStem`. With no entitlement gate, a Free student who knows a question GUID can read the stem of a servable question in a locked lesson. Consider rewording the doc or gating question previews behind the entitlement.
- `web/src/features/askTeacher/components/LessonPicker.tsx:10` — the lesson select can be disabled but has no `disabled:opacity-45`, which neighbouring selects use (`blueprints/components/SubjectPicker.tsx:25`).
- `postman/elmanhg.postman_collection.json` "Get my thread" — when `teacherThreadId` is empty (create returned 403), the URL becomes `/api/teacher-threads/` and hits the list endpoint. The test still passes (200); this is cosmetic.
- Multipart uploads are fully buffered before the 5 MB validator runs (the default form limits apply). This is the same as the lesson-image upload. A request-size attribute on the action would reject oversize uploads earlier.

## Verified
- **Build and tests, run myself:** `dotnet test api/ -c Release` with `appsettings.json` moved aside gave 2712 passed, 0 failed (the file is restored). The web typecheck, lint (`--max-warnings=0`) and `test -- --run` pass (149 files, 878 tests). `prettier --check --end-of-line auto` is clean. The generated artifacts did not drift after the build.
- **Photo privacy (the plan-gate condition), checked by reading the code:**
  - `TeacherThreadMediaMiddleware` runs before static files. It matches only GET on `/api/media/teacher-threads` (ordinal, segment boundary) and authorises through `CanViewTeacherThreadImageHandler`. Only the owner, an Admin, or a Teacher with `IsAssignedAsync` on the thread's subject gets the file. Anonymous callers are refused before any DB call. Everyone else gets an empty 404.
  - The public mount uses `PublicMediaFileProvider`, which checks the physical path it resolves against the private folder case-insensitively. It blocks case variants, doubled slashes, encoded backslash (a separator on Windows), trailing dots (normalised by GetFullPath) and dot-dot (normalised by Kestrel, and PhysicalFileProvider refuses any path above the root). It blocks tilde 8.3 aliases. Colon/ADS is rejected by PhysicalFileProvider's invalid-char check. An encoded forward slash stays encoded and never resolves. HEAD requests fall through to the filtered provider and are blocked. Directory listing is off.
  - If the middleware does not match (for example because of a config variant), the static provider still blocks, so access fails closed.
  - No response header leaks the path: only Content-Type, Content-Length, nosniff and `Cache-Control: private, no-store`, with no ETag or Last-Modified.
  - Tests: 7 handler tests and `TeacherThreadMediaEndpointTests` (owner, anonymous, other student, scoped and unscoped teacher, admin, 5 non-canonical paths).
- **Upload:** the extension allow-list (case-insensitive, via Core.Validation) and the content-type set exclude SVG and GIF. The 5 MB cap is `AskTeacherOptions.ImageMaxSizeInMb`. The client filename is used only for its validated extension. The key is `teacher-threads/{Guid:N}{ext}`, and `LocalDiskFileStorage` also guards against escaping the storage root.
- **Quota:** `AskTeacherGate.CurrentQuotaMonth` turns the Cairo month bounds into UTC without using DateTime; I checked it by hand for Feb and Dec. The count is a half-open window per student. The gate order is 401, then 403 add-on, then 403 quota (context limit), then 404 context, then the image, then one save. It fails closed: an exception while counting means no thread, and a bad time zone is refused at startup. `AskTeacherGateTests` compile the real predicate against previous-month, next-month and other-student threads, so they constrain the window and the student filter.
- **Context:** an attempt must be the student's own (`GetStudentAttemptAsync` filters on StudentId), else 404 ATTEMPT_NOT_FOUND. A question must pass `ServableQuestionSpecification`, else 404 QUESTION_NOT_FOUND. The lesson must be Published, else 404 LESSON_NOT_FOUND. An attempt uses the stem of the revision it served. All match Decisions 1, 3 and 4.
- **Thread reads:** both the list and by-id filter on the owner's StudentId; another student's thread returns 404 TEACHER_THREAD_NOT_FOUND (integration test 61).
- **Model readiness for #95–#97:** status enum with all three states, xmin Version, SubjectId/Status/SubmittedAt inbox index, Kind Voice reserved, internal CreateText factory, both entities in the soft-delete filter. The migration has only CreateTable, FK and index operations.
- **Plan-gate deviations** (the media middleware, provider and handler; the ThreadImage data URL; the StorageFolder constant; the DateTimeOffset-only MonthStart; the Orval query-zod exclusion) are all listed in 02-implementation.md and justified.
- **Contract fidelity:** every file in "Files to create" exists. The endpoints, policies (AskTeacherSubmit), 12 error codes (resx and web errors), options with ValidateOnStart, and the appsettings.example.json entry all match. Skill absolutes hold: sealed records and handlers, file-scoped namespaces, ConfigureAwait(false), no DateTime, no try/catch, WHY-only comments.
- **Tests:** every row of the plan's test plan (1–103) exists by name.
- **Postman:** the AskTeacher folder has 4 requests in plan order plus the teacherThreadId variable.
- **Docs:** PRD 12.1/15/20, docs/subscriptions.md, docs/claude-design-prompt.md section 4, docs/prototype.md item 7 and the new docs/ask-teacher.md all agree with the code, including private photos. I found no divergence.
- **Design system:** web colours and radii use tokens only, spacing uses the Tailwind scale in line with neighbouring components, the breadcrumb chevron has rtl:rotate-180, and every data view has loading, error-with-retry and empty states plus RTL tests.

## Test quality
- AskTeacherGateTests, CanViewTeacherThreadImageHandlerTests — constrain the implementation (the real predicates are compiled against seeded data).
- CreateTeacherThreadHandlerTests — constrain it: gate order through DidNotReceive on storage and save, the key prefix and extension, Received(1) on save.
- TeacherThreadContextResolverTests — constrain it; the revision version was mutation-checked by the implementer.
- TeacherThreadMediaEndpointTests — the strongest tests: real HTTP through Kestrel with non-canonical paths.
- Web page tests — they capture the actual FormData and assert URL changes, aria-invalid and axe results. None are vacuous.
