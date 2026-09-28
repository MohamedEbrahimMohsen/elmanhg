# Plan — [E2.S2] Lesson authoring with explanation, objectives and summary (#61)

## Goal
An admin can open a unit in `#/admin/content`, see its lesson count and lesson list, add a lesson (it is created as Draft), and land on `#/admin/lesson/:id`. In the editor they can change the name, write the explanation and summary in a rich-text editor (headings, bold, lists, links, images uploaded to object storage, inline and block LaTeX rendered live), edit an ordered objectives list (add, reorder, remove), and set an optional video link. A live student-view preview renders the same sanitised HTML with KaTeX. Every save is sanitised on the server and audited with a field diff, and image uploads are audited too. A unit that has lessons can no longer be deleted.

## Scope
**In:**
- Domain: `Lesson` aggregate (state, explanation, summary, video URL) with `LessonObjective` children (ordered, stable ids, soft delete). `CurriculumUnit.Delete` gets a "has lessons" guard.
- Rich text: sanitised-HTML storage format (`docs/rich-text.md`), server sanitiser `IRichTextSanitizer` (HtmlSanitizer), client `SafeHtml` (DOMPurify).
- Image upload endpoint with a type allow-list and a size cap, behind `IFileStorage` with a `LocalDiskFileStorage` implementation served at `/api/media`.
- 3 audited commands (CreateLesson, UpdateLesson, UploadLessonImage) and 2 queries (GetLessons by unit, GetLesson).
- #142 pickups: `UnitResult.LessonCount`, and the `UNIT_HAS_LESSONS` delete guard.
- EF mapping and migration, options, resx, Postman, and regenerated OpenAPI/Orval output.
- Web: lesson list and "add lesson" inside the unit panel. Lesson editor page with a TipTap editor (KaTeX via `@tiptap/extension-mathematics`), objectives field-array, formula and image dialogs, and a preview.
- Docs: `docs/audit-log.md`, `docs/PRD.md` §15, `docs/claude-design-prompt.md` §4, and the new `docs/rich-text.md`.

**Out:**
- Publish, unpublish and archive, and `published_at` (story #62).
- Lesson reorder, lesson delete and lesson rename from the tree. They are not in #61's sub-tasks, and the name is edited in the editor.
- Student lesson page (browsing story). `RichTextViewer` is exported for it.
- Question list inside the editor (E3). Lesson counts on subject results. Drag-and-drop (stays on #142).
- Cleanup of uploaded images that are never referenced. Video embed player: the preview shows a link.

**Deferred:**
- **S3-compatible `IFileStorage` adapter** (`FileStorageProvider.S3`, bucket/endpoint/credential options). No bucket or credentials exist in this repo, and the constitution rules out an always-on paid resource without approval. `LocalDiskFileStorage` behind `FileStorage:Provider=Local` ships now.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Aggregate shape | `Lesson : AuditEntity, IAuditedEntity` in `Elmanhg.Domain.Lessons` with `UnitId` (FK to `Units`, Restrict). No `SubjectId` column. | Mirrors `CurriculumUnit` (#60 D2: `AggregateRoot` has no audit fields). The subject is reachable through the unit, and no teacher-scoped read exists in this story. |
| D2 | Objectives storage | A child entity `LessonObjective : AuditEntity, IAuditedEntity` (table `LessonObjectives`, FK `LessonId` Restrict). It is exposed as `Lesson.Objectives` (`List<LessonObjective>`) and mutated only through `Lesson.Update`. `Id` is `ValueGeneratedNever()`. | PRD §15 defines `LessonObjective(id, lesson_id, text, order)`. E3 `Question.objective_id` needs stable ids. Owned or jsonb collections are not diffed per item (audit-log "Known limits"). `ValueGeneratedNever` makes EF insert client-keyed children found through the navigation instead of updating them (SKILL §6.3 table). |
| D3 | Objectives update semantics | Full-list replace. The request sends the ordered list of `{ id?, text }`. An existing id keeps its identity and gets the new text and `Order = index + 1`. A null id creates a new objective. An existing objective that is missing from the list is soft-deleted. An id not on the lesson → domain `BusinessRuleViolationCoreException(LESSON_OBJECTIVE_UNKNOWN)` (400). A duplicate id → validator `LESSON_OBJECTIVE_DUPLICATE` (422). | This matches the prototype `saveLesson` (whole list saved, ids kept). The editor sends one PUT per save. |
| D4 | Rich-text storage format | Sanitised HTML (the TipTap `getHTML()` output) stored in `text` columns `Explanation` and `Summary`. Allowed tags: `p br strong em u s code pre blockquote h2 h3 ul ol li a hr img span div`. Allowed attributes: `href src alt start data-type data-latex`. Allowed schemes: `https http mailto`. No `style` or `class`. `img[src]` is kept only when it starts with `FileStorage:PublicBaseUrl + "/"`. Math is stored as `<span data-type="inline-math" data-latex="…">` or `<div data-type="block-math" data-latex="…">`. | A mature allow-list sanitiser can enforce HTML on the server (ProseMirror JSON could not be sanitised without re-implementing the schema). The student viewer renders it without shipping the editor. Audit diffs stay readable. Images limited to our storage mean no tracking pixels or hotlinks. |
| D5 | Sanitise where | Server: `IRichTextSanitizer` (Application port) is implemented by `RichTextSanitizer` (Infrastructure, NuGet `HtmlSanitizer` 9.2.1039, MIT, verified on nuget.org 2026-08-28) and called in `UpdateLessonHandler` before the domain call. Client: `SafeHtml` runs `DOMPurify.sanitize` on render (defence in depth, react-feature §3/§19). | Sub-task "sanitisation on save". This keeps the third-party library out of Domain and Application, following the same port-plus-adapter shape as `ISmsSender`. |
| D6 | Editor and LaTeX | TipTap 3.31.3: `@tiptap/react`, `@tiptap/pm`, `@tiptap/core`, `@tiptap/starter-kit` (heading levels 2 and 3 only), `@tiptap/extension-image` (no base64), and `@tiptap/extension-mathematics` (KaTeX node views, so LaTeX renders live in the editor). `katex` 0.18.9 and `dompurify` 3.4.16. All verified with `npm view` (MIT, or MPL-2.0/Apache-2.0 for DOMPurify). The viewer and preview use `renderMath(html)` (KaTeX `renderToString` on `[data-latex]` nodes) followed by `SafeHtml`. | Sub-task "LaTeX in editor and viewer". Mathematics v3 is MIT and stores LaTeX in an attribute, so there is no `$…$` delimiter parsing. `renderMath` is a pure function and unit-testable. |
| D7 | Image upload | `POST /api/lessons/{lessonId}/images` (multipart, field `file`). Allowed extensions `.png .jpg .jpeg .webp .gif`, matched with the content type. SVG is excluded because it can carry script and is served same-origin. Size cap `Content:LessonImageMaxSizeInMb` = 5. Key `lessons/{lessonId}/{Guid.NewGuid():N}{lowercase ext}`. Returns `{ url }`. No DB row. | Security gotchas "Uploads": size-limited, allow-listed, random names, not under `wwwroot`. The image lives only inside the HTML, so an image table would be dead weight. `Guid.NewGuid` follows the repo's id convention; `IGenerator` (§8.2) is for slugs. |
| D8 | Object storage | Port `IFileStorage.SaveAsync(Stream, string key, CancellationToken) → Task<string url>` in `Application/Shared/Storage`. `LocalDiskFileStorage` writes under `FileStorage:LocalRootPath` (relative paths resolve against `ContentRootPath`). It is selected by `FileStorage:Provider` with the same switch factory as `Sms:Provider`. The API serves the root with `UseStaticFiles` at `RequestPath = FileStorage:PublicBaseUrl` (`/api/media`) and sets `X-Content-Type-Options: nosniff`. Access is public-read by an unguessable key. | The caller asked for local-disk now and S3 deferred. Putting it under `/api` means the existing Vite `/api` proxy and a same-origin deploy both reach it with no web config. The content type comes from the extension and nosniff is set, so no magic-byte sniffing is needed. |
| D9 | Policies | Every lesson endpoint uses `DefaultCodes.ContentManage` (Admin). | PRD §5.2: Draft is invisible to students. PRD §16: create/edit is Admin. All lessons are Draft here, so there is no teacher or student read and no `ISubjectScopedRequest`. |
| D10 | Routes | Top-level `api/lessons`: `GET ?unitId=`, `GET /{lessonId}`, `POST` (body carries `unitId`), `PUT /{lessonId}`, `POST /{lessonId}/images`. A missing unit → 404 `UNIT_NOT_FOUND`. | The editor URL knows only the lesson id. Nesting three levels (`subjects/…/units/…/lessons`) adds no ownership check that the unit lookup does not already give. |
| D11 | Create | `CreateLessonCommand(UnitId, Name)`. The lesson is appended after the highest live sibling (`(last?.Order ?? 0) + 1`, #60 D5), with `State = Draft`, empty explanation and summary, no objectives and no video. The UI then navigates to the editor. | Prototype `addLesson`. The caller said "create lessons as Draft only". |
| D12 | #142 pickups | `UnitResult` gains `int LessonCount`, filled in `GetSubjectHandler` from `ILessonRepository.CountByUnitAsync`. `CurriculumUnit.Delete(bool hasLessons, Guid deletedBy)` throws domain `UNIT_HAS_LESSONS` (400), and `DeleteUnitHandler` asks `AnyInUnitAsync`. Subject results are unchanged. | This is exactly #60's deferral text, mirroring `Subject.Delete(bool hasUnits, …)`. The prototype subject row shows no lesson count. |
| D13 | State | `enum LessonState { Draft, Published, Archived }`, stored as a string column (`HasConversion<string>().HasMaxLength(EnumColumnMaxLength)`). Results expose `string State` (`lesson.State.ToString()`). | PRD §5.2. All three values exist now, so #62 needs no migration for the column. A string in the result mirrors `AuthUserResult.Role` and keeps the OpenAPI schema honest (SKILL §8.6). |
| D14 | Video URL | Optional. Validated with `ValidateUrl` (absolute http/https) plus `ValidateMaxLength`. Blank → stored as null. The preview shows an external link (`toSafeVideoUrl` allow-list), not an iframe. | PRD §10.1 "video embed URL". The embed player belongs to the student lesson page, and raw iframes are outside the `SafeHtml` allow-list. |
| D15 | Caps | `ContentOptions` adds `LessonNameMaxLength` 100, `LessonExplanationMaxLength` 100000, `LessonSummaryMaxLength` 20000, `LessonObjectiveMaxLength` 300, `LessonObjectivesMaxCount` 20, `LessonVideoUrlMaxLength` 2048, `LessonImageMaxSizeInMb` 5. The extension/content-type allow-list is a named constant (`LessonImageFormats`) with a WHY comment. | SKILL §8.1: caps live in options. The allow-list is a security invariant, not a product tunable. |
| D16 | Include from Application | `ILessonRepository.GetWithObjectivesAsync(Guid lessonId, bool asNoTracking, CancellationToken)`. | `Elmanhg.Application` has no EF Core reference, so `include: q => q.Include(...)` cannot be written there. The base repository cannot express this. |
| D17 | Nested forms | The formula and image inserters are Radix dialogs (portal) with their own `Form`. The shared `Form` calls `event.stopPropagation()` before `handleSubmit`, so a dialog submit never submits the enclosing lesson form (React events bubble through portals). | Avoids invalid nested `<form>` DOM and an accidental lesson save. |
| D18 | Image alt text | The image dialog requires a description, which becomes `alt`. | WCAG 1.1.1. The sanitiser keeps `alt`. |
| D19 | Layout | Editor card and preview side by side at `lg` (`grid gap-4 lg:grid-cols-2`) and stacked below that. The preview shows the name (h2) and h3 sections for Explanation, Objectives, Summary and Video. | design-system "two-column editors". Rule 4: lesson content sits on surface white. |
| D20 | Audit | `Lesson.Create` (id from result), `Lesson.Update` (command; the diff lists Lesson fields plus each `LessonObjective` Created/Modified/Deleted), `Lesson.UploadImage` (command; no diff because no entity changes). `docs/audit-log.md` "Uploads" bullet is rewritten. | The caller said "audit every mutation". PRD §17 rule 13: a content image is a content change. |
| D21 | Docs | New `docs/rich-text.md` (format contract). PRD §15 `Lesson` adds `video_url?`. `claude-design-prompt.md` §4 lesson-editor line updated. | docs-sync: these are a format contract, a data-model change and a UI content change. |
| D22 | Lesson list in tree | Each unit row gets `n lessons` and a "Show lessons" disclosure that mounts `UnitLessons` (fetches only while expanded). Lessons are links to the editor with a state badge. The row keeps an "Add lesson" name form. | Same pattern as #60 D19 (`UnitPanel`). The prototype tree lists lessons with a state badge and links to the editor. |

Morabh reuse: the upload command carrying `IFormFile` and a `[FromForm] IFormFile` controller parameter follow Morabh `Morabh.Application/Files/UploadFile/UploadFileCommand.cs` and `Morabh.APIs/Controllers/Files/FilesController.cs` (shape only). `IFileStorage` follows `Core/Core.Azure/Clients/StorageBlobClient.cs` (`UploadAsync(Stream, …, blobName)` → URL), with Azure removed. Everything else is new, with no Morabh equivalent (sanitiser, lesson aggregate, local storage, editor).

## Existing code touched
| File | Change |
|------|--------|
| `api/Directory.Packages.props` | `<PackageVersion Include="HtmlSanitizer" Version="9.2.1039" />` in the main ItemGroup. |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | `<PackageReference Include="HtmlSanitizer" />`. |
| `api/Elmanhg.Domain/Units/CurriculumUnit.cs` | `Delete(Guid deletedBy)` → `Delete(bool hasLessons, Guid deletedBy)` (see Domain behaviour). |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// CONTENT` add `UnitHasLessons = "UNIT_HAS_LESSONS"` and `LessonObjectiveUnknown = "LESSON_OBJECTIVE_UNKNOWN"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | New group `// LESSONS` after `// UNITS` with the 15 application codes in Error codes. |
| `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | Add the 7 `[Range(1, int.MaxValue)] public int …` properties from D15, in that order. |
| `api/Elmanhg.Application/Units/Shared/UnitResult.cs` | `public sealed record UnitResult(Guid Id, Guid SubjectId, string Name, int Order, int LessonCount);` |
| `api/Elmanhg.Application/Units/Shared/UnitResultGenerator.cs` | `public static UnitResult Generate(CurriculumUnit unit, int lessonCount)`. |
| `api/Elmanhg.Application/Subjects/Shared/SubjectResultGenerator.cs` | `GenerateDetail(Subject subject, List<CurriculumUnit> units, Dictionary<Guid, int> lessonCounts)` → `units.Select(x => UnitResultGenerator.Generate(x, lessonCounts.GetValueOrDefault(x.Id))).ToList()`. |
| `api/Elmanhg.Application/Subjects/GetSubject/GetSubjectHandler.cs` | Constructor adds `ILessonRepository lessonRepository`. After loading units: `var lessonCounts = await lessonRepository.CountByUnitAsync(units.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);` then `return SubjectResultGenerator.GenerateDetail(subject, units, lessonCounts);`. |
| `api/Elmanhg.Application/Units/DeleteUnit/DeleteUnitHandler.cs` | Constructor `(ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ICurrentUserService currentUserService)`. After the 404 check: `var hasLessons = await lessonRepository.AnyInUnitAsync(unit.Id, cancellationToken).ConfigureAwait(false);` then `unit.Delete(hasLessons, currentUserService.UserId.Value);`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add `public DbSet<Lesson> Lessons { get; set; }` and `public DbSet<LessonObjective> LessonObjectives { get; set; }`. Call `ConfigureLessons(modelBuilder);` after `ConfigureUnits`. Add filter lines for `Lesson` and `LessonObjective`. See Files #22. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | Add, in this order: `services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` · `services.AddScoped<LocalDiskFileStorage>();` · `services.AddScoped<IFileStorage>(serviceProvider => serviceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value.Provider switch { FileStorageProvider.Local => serviceProvider.GetRequiredService<LocalDiskFileStorage>(), _ => throw new InvalidOperationException("Unsupported FileStorage:Provider."), });` · `services.AddSingleton<IRichTextSanitizer, RichTextSanitizer>();` · `services.AddScoped<ILessonRepository, LessonRepository>();` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated. |
| `api/Elmanhg.Api/Program.cs` | `using Elmanhg.Api.FileStorage;`. Insert `app.UseLocalFileStorage();` immediately after `app.UseHttpsRedirection();`. Nothing else moves. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | The 17 keys in Error codes. |
| `api/Elmanhg.Api/appsettings.example.json` | Append the 7 lesson keys to `"Content"` (values from D15). Add `"FileStorage": { "Provider": "Local", "LocalRootPath": "App_Data/media", "PublicBaseUrl": "/api/media" },` after `"Sms"`. Mirror both into the local gitignored `appsettings.json` if it exists (not committed). |
| `.gitignore` | Under `# .NET` add `/api/Elmanhg.Api/App_Data/`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build api/Elmanhg.Api`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), "elmanhg-tests-media", Guid.NewGuid().ToString("N"));`. Add in-memory keys: `Content:LessonNameMaxLength`=100, `Content:LessonExplanationMaxLength`=100000, `Content:LessonSummaryMaxLength`=20000, `Content:LessonObjectiveMaxLength`=300, `Content:LessonObjectivesMaxCount`=20, `Content:LessonVideoUrlMaxLength`=2048, `Content:LessonImageMaxSizeInMb`=5, `FileStorage:Provider`="Local", `FileStorage:LocalRootPath`=MediaRoot, `FileStorage:PublicBaseUrl`="/api/media". In `DisposeAsync`, before the base call: `if (Directory.Exists(MediaRoot)) { Directory.Delete(MediaRoot, recursive: true); }`. |
| `api/Elmanhg.Tests/Integration/Content/ContentTestData.cs` | Add `static Task<Guid> SeedLessonAsync(ApiFactory factory, Guid unitId, string name, int order, IReadOnlyList<string> objectiveTexts, CancellationToken cancellationToken)`: load the unit, `Lesson.Create(unit, name, order, creator)`, then when `objectiveTexts.Count > 0` call `lesson.Update(name, string.Empty, string.Empty, null, objectiveTexts.Select(x => new LessonObjectiveContent(null, x)).ToList(), creator)`, then `context.Lessons.Add`, then save. Add `static Task<Lesson> ReadLessonAsync(ApiFactory factory, Guid lessonId, CancellationToken cancellationToken)`: `context.Lessons.IgnoreQueryFilters().Include(x => x.Objectives).AsNoTracking().SingleAsync(x => x.Id == lessonId, …)` (IgnoreQueryFilters so soft-deleted objectives are visible to the assertion). |
| `api/Elmanhg.Tests/Domain/Units/CurriculumUnitTests.cs` | **modify** `Delete_Always_SoftDeletesAndSetsUpdater` → rename to `Delete_NoLessons_SoftDeletesAndSetsUpdater` and call `unit.Delete(false, deletedBy)`. Add Test plan row 16. |
| `api/Elmanhg.Tests/Application/Features/Units/DeleteUnit/DeleteUnitHandlerTests.cs` | **modify**: add `ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>()` and pass it to the constructor. Existing tests are otherwise unchanged (`AnyInUnitAsync` returns false by default). Add row 36. |
| `api/Elmanhg.Tests/Application/Features/Subjects/GetSubject/GetSubjectHandlerTests.cs` | **modify**: add an `ILessonRepository` substitute to the constructor. In `Handle_ExistingSubject_ReturnsSubjectWithUnits`, stub `CountByUnitAsync(...)` → `new Dictionary<Guid, int> { [first.Id] = 3 }` and expect `new UnitResult(first.Id, subject.Id, "Mechanics", 1, 3)` and `new UnitResult(second.Id, subject.Id, "Waves", 2, 0)`. |
| `api/Elmanhg.Tests/Integration/Content/UnitsEndpointTests.cs` | Add row 64. |
| `api/Elmanhg.Tests/Integration/Content/SubjectsEndpointTests.cs` | Add row 65. |
| `postman/elmanhg.postman_collection.json` | New folder "Lessons": Get lessons (`?unitId={{unitId}}`), Get lesson, Create lesson, Update lesson, Upload lesson image (formdata `file`). Bearer auth like "Units". |
| `docs/audit-log.md` | "Audited commands": add 3 rows (Audit table below). "Audited entities" → `TeacherSubject`, `Subject`, `CurriculumUnit`, `Lesson`, `LessonObjective`. Replace the sentence "Validation commands (E3) and later content commands (lessons, questions) join this table when they are built." with "Validation commands (E3) and question commands join this table when they are built." Replace the "Uploads" bullet with: "**Evidence uploads** (Ask a Teacher images and voice): audit the decision, not the file. Lesson content images are the exception: they change content (PRD §17 rule 13), so `UploadLessonImage` is audited as `Lesson.UploadImage` with no diff." |
| `docs/PRD.md` | §15 `Lesson(id, unit_id, name, order, state[Draft|Published|Archived], explanation, summary, video_url?, published_at)`. |
| `docs/claude-design-prompt.md` | §4 Admin bullet: replace "`#/admin/lesson/:id` editor (explanation, objectives list, summary, video URL)." with "`#/admin/lesson/:id` editor: name, rich-text explanation and summary (image upload, inline and block LaTeX), ordered objectives list, optional video URL, and a live student-view preview." |
| `web/package.json`, `web/package-lock.json` | dependencies (exact): `@tiptap/core` 3.31.3, `@tiptap/pm` 3.31.3, `@tiptap/react` 3.31.3, `@tiptap/starter-kit` 3.31.3, `@tiptap/extension-image` 3.31.3, `@tiptap/extension-mathematics` 3.31.3, `katex` 0.18.9, `dompurify` 3.4.16. `npm i -E …`. Do not add `@types/dompurify`, because it is deprecated and DOMPurify ships its own types. |
| `web/src/shared/form/Form.tsx` | `onSubmit={(event) => { event.stopPropagation(); void form.handleSubmit(submit)(event); }}` (D17). |
| `web/src/shared/form/Form.test.tsx` | **modify**: add row W1. |
| `web/src/shared/form/TextField.tsx` | New optional prop `dir?: 'ltr'`, passed to `<Input dir={dir}>`. |
| `web/src/test/setup.ts` | After the `scrollTo` line, jsdom gaps for ProseMirror: `Object.defineProperty(Range.prototype, 'getBoundingClientRect', { value: function () { return document.createElement('div').getBoundingClientRect(); }, writable: true });`, the same for `getClientRects` (→ `document.createElement('div').getClientRects()`), and `Object.defineProperty(document, 'elementFromPoint', { value: () => null, writable: true });`. |
| `web/src/styles/app.css` | Append `@layer components { … }` with `.rich-text` rules (see Files W-CSS). |
| `web/src/features/content/components/UnitPanel.tsx` | Replace the inline unit `<li>` with `<UnitItem key={unit.id} subjectId={subjectId} unit={unit} isFirst isLast position={index + 1} />`. The move/rename/delete wiring moves into `UnitItem`. |
| `web/src/features/content/components/UnitPanel.test.tsx` | **modify**: the fixtures `mechanics` and `waves` gain `lessonCount: 0`. No assertion changes. |
| `web/src/features/content/index.ts` | Add `export { LessonEditorPage } from './pages/LessonEditorPage'; export { RichTextViewer } from './components/RichTextViewer';`. |
| `web/src/features/content/i18n/en.json`, `ar.json` | Keys in "Locale keys" below. |
| `web/src/shared/i18n/en.json`, `ar.json` | `validation.url`, `validation.fileRequired`, plus 16 `errors.*` (every code in Error codes except `LESSON_ID_REQUIRED`), with the same text as the resx. |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (`npx vite build`). Never hand-edited. |
| `web/src/shared/api/generated/**` | `npm run gen:api` after the API build adds `lessons/`, the model and zod files, and `lessonCount` on `UnitResult`. |

## Files to create
Conventions (#60 carry-over):
- Every command handler starts with the current-user guard (`UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`).
- Every mutation ends with exactly one `SaveChangesAsync` (except the upload, which has no DB change).
- `.ConfigureAwait(false)` goes on every await, braces on every `if`, and methods are block-bodied.
- Sort `S` = `query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate)`.
- `ErrorCodes` in Application means `Elmanhg.Application.Exceptions.ErrorCodes`. In Domain it means `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`.
- All results are admin-facing plain strings. There is no `LocalizedText` and no `.Localized()`.

### Domain (`api/Elmanhg.Domain/Lessons/`, namespace `Elmanhg.Domain.Lessons`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `LessonState.cs` | enum | `public enum LessonState { Draft, Published, Archived }` |
| 2 | `LessonObjectiveContent.cs` | value object | `public sealed record LessonObjectiveContent(Guid? Id, string Text);` |
| 3 | `LessonObjective.cs` | entity | `public class LessonObjective : AuditEntity, IAuditedEntity` · `Guid LessonId {get; private set;}` · `string Text {get; private set;} = string.Empty` · `int Order {get; private set;}` · `private LessonObjective(Guid id, Guid? createdBy) : base(id, createdBy) { }` · `static LessonObjective Create(Guid lessonId, string text, int order, Guid createdBy)` · `void Update(string text, int order, Guid updatedBy)` · `void Delete(Guid deletedBy)` |
| 4 | `Lesson.cs` | entity | `public class Lesson : AuditEntity, IAuditedEntity` · `Guid UnitId`, `string Name = string.Empty`, `int Order`, `LessonState State`, `string Explanation = string.Empty`, `string Summary = string.Empty`, `string? VideoUrl` (all `{ get; private set; }`) · `List<LessonObjective> Objectives { get; private set; } = [];` · private ctor · `static Lesson Create(CurriculumUnit unit, string name, int order, Guid createdBy)` · `void Update(string name, string explanation, string summary, string? videoUrl, IReadOnlyList<LessonObjectiveContent> objectives, Guid updatedBy)` |
| 5 | `ILessonRepository.cs` | repo interface | `public interface ILessonRepository : IRepository<Lesson>` { `Task<Lesson?> GetWithObjectivesAsync(Guid lessonId, bool asNoTracking, CancellationToken cancellationToken);` `Task<bool> AnyInUnitAsync(Guid unitId, CancellationToken cancellationToken);` `Task<Dictionary<Guid, int>> CountByUnitAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken);` } |

### Application — ports & shared
| # | Path | Type | Contract |
|---|------|------|----------|
| 6 | `api/Elmanhg.Application/Shared/RichText/IRichTextSanitizer.cs` | port | `namespace Elmanhg.Application.Shared.RichText; public interface IRichTextSanitizer { string Sanitize(string? html); }` |
| 7 | `api/Elmanhg.Application/Shared/Storage/IFileStorage.cs` | port | `namespace Elmanhg.Application.Shared.Storage; public interface IFileStorage { Task<string> SaveAsync(Stream content, string key, CancellationToken cancellationToken); }` (returns the public URL) |
| 8 | `api/Elmanhg.Application/Lessons/Shared/LessonResult.cs` | result | `public sealed record LessonResult(Guid Id, Guid UnitId, string Name, int Order, string State);` |
| 9 | `api/Elmanhg.Application/Lessons/Shared/LessonObjectiveResult.cs` | result | `public sealed record LessonObjectiveResult(Guid Id, string Text, int Order);` |
| 10 | `api/Elmanhg.Application/Lessons/Shared/LessonDetailResult.cs` | result | `public sealed record LessonDetailResult(Guid Id, Guid UnitId, string Name, int Order, string State, string Explanation, string Summary, string? VideoUrl, List<LessonObjectiveResult> Objectives);` |
| 11 | `api/Elmanhg.Application/Lessons/Shared/LessonResultGenerator.cs` | static | `static LessonResult Generate(Lesson lesson)` → `State = lesson.State.ToString()` · `static LessonDetailResult GenerateDetail(Lesson lesson)` → objectives `lesson.Objectives.OrderBy(x => x.Order).Select(x => new LessonObjectiveResult(x.Id, x.Text, x.Order)).ToList()` |

### Application — use cases (namespace `Elmanhg.Application.Lessons.<UseCase>`, all under `api/Elmanhg.Application/`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 12 | `Lessons/CreateLesson/CreateLessonCommand.cs` | command | `sealed record CreateLessonCommand(Guid UnitId, string Name) : IRequest<CreateLessonResult>, IAuditableCommand` · `AuditAction => "Lesson.Create"` · `AuditResourceType => "Lesson"` · `AuditResourceId => null` |
| 13 | `Lessons/CreateLesson/CreateLessonResult.cs` | result | `sealed record CreateLessonResult(Guid Id) : IAuditableResult { Guid? IAuditableResult.AuditResourceId => Id; }` |
| 14 | `Lessons/CreateLesson/CreateLessonValidator.cs` | validator | ctor `(IOptions<ContentOptions> contentOptions)`: `UnitId.ValidateRequired(ErrorCodes.UnitIdRequired)` · `Name.ValidateRequired(ErrorCodes.LessonNameRequired).ValidateMaxLength(options.LessonNameMaxLength, ErrorCodes.LessonNameTooLong)` |
| 15 | `Lessons/CreateLesson/CreateLessonHandler.cs` | handler | `(ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ICurrentUserService currentUserService)`. 1. `unit = unitRepository.GetByIdAsync(request.UnitId, ct, asNoTracking: true)`. If null, throw `NotFoundCoreException(ErrorCodes.UnitNotFound)` 2. `last = lessonRepository.FirstOrDefaultAsync(x => x.UnitId == unit.Id, ct, orderBy: q => q.OrderByDescending(x => x.Order), asNoTracking: true)` 3. `lesson = Lesson.Create(unit, request.Name, (last?.Order ?? 0) + 1, userId)` 4. `AddAsync` 5. `SaveChangesAsync` 6. `return new CreateLessonResult(lesson.Id)` |
| 16 | `Lessons/UpdateLesson/UpdateLessonCommand.cs` | command | `sealed record UpdateLessonCommand(Guid LessonId, string Name, string? Explanation, string? Summary, string? VideoUrl, IList<LessonObjectiveContent> Objectives) : IRequest, IAuditableCommand` · `"Lesson.Update"` · `"Lesson"` · `AuditResourceId => LessonId` |
| 17 | `Lessons/UpdateLesson/UpdateLessonValidator.cs` | validator | ctor `(IOptions<ContentOptions>)`: `LessonId.ValidateRequired(LessonIdRequired)` · `Name.ValidateRequired(LessonNameRequired).ValidateMaxLength(options.LessonNameMaxLength, LessonNameTooLong)` · `Explanation.ValidateMaxLength(options.LessonExplanationMaxLength, LessonExplanationTooLong)` · `Summary.ValidateMaxLength(options.LessonSummaryMaxLength, LessonSummaryTooLong)` · `VideoUrl.ValidateUrl(LessonVideoUrlInvalid).ValidateMaxLength(options.LessonVideoUrlMaxLength, LessonVideoUrlTooLong)` · `Objectives.ValidateListMaxItems(options.LessonObjectivesMaxCount, LessonObjectivesTooMany)` · `RuleFor(x => x.Objectives).Must(objectives => objectives is null \|\| objectives.Where(x => x.Id.HasValue).GroupBy(x => x.Id).All(x => x.Count() == 1)).WithErrorCode(ErrorCodes.LessonObjectiveDuplicate)` · `RuleForEach(x => x.Objectives).ChildRules(objective => objective.RuleFor(x => x.Text).ValidateRequired(LessonObjectiveTextRequired).ValidateMaxLength(options.LessonObjectiveMaxLength, LessonObjectiveTextTooLong))` |
| 18 | `Lessons/UpdateLesson/UpdateLessonHandler.cs` | handler | `(ILessonRepository lessonRepository, IRichTextSanitizer richTextSanitizer, ICurrentUserService currentUserService)`. 1. `lesson = lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: false, ct)`. If null, throw `NotFoundCoreException(ErrorCodes.LessonNotFound)` 2. `explanation = richTextSanitizer.Sanitize(request.Explanation)`; `summary = richTextSanitizer.Sanitize(request.Summary)` 3. `lesson.Update(request.Name, explanation, summary, request.VideoUrl, request.Objectives.ToList(), userId)` (the domain throws `LESSON_OBJECTIVE_UNKNOWN`) 4. `SaveChangesAsync` |
| 19 | `Lessons/GetLesson/GetLessonQuery.cs` · `GetLessonValidator.cs` · `GetLessonHandler.cs` | query set | `sealed record GetLessonQuery(Guid LessonId) : IRequest<LessonDetailResult>` · validator `LessonId.ValidateRequired(LessonIdRequired)` · handler `(ILessonRepository)`, no user guard: 1. `GetWithObjectivesAsync(id, asNoTracking: true, ct)`. If null, throw `NotFoundCoreException(LessonNotFound)` 2. `return LessonResultGenerator.GenerateDetail(lesson)` |
| 20 | `Lessons/GetLessons/GetLessonsQuery.cs` · `GetLessonsValidator.cs` · `GetLessonsHandler.cs` | query set | `sealed record GetLessonsQuery(Guid UnitId) : IRequest<List<LessonResult>>` · validator `UnitId.ValidateRequired(UnitIdRequired)` · handler `(ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository)`: 1. `unit = unitRepository.GetByIdAsync(request.UnitId, ct, asNoTracking: true)`. If null, throw `NotFoundCoreException(UnitNotFound)` 2. `lessons = lessonRepository.FindAsync(x => x.UnitId == unit.Id, ct, orderBy: S, asNoTracking: true)` 3. `return lessons.Select(LessonResultGenerator.Generate).ToList()` |
| 21a | `Lessons/UploadLessonImage/LessonImageFormats.cs` | constants | `public static class LessonImageFormats` · `// Raster formats only: SVG can carry script and media is served from the API origin.` · `public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp", ".gif"];` · `public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(["image/png", "image/jpeg", "image/webp", "image/gif"], StringComparer.OrdinalIgnoreCase);` |
| 21b | `Lessons/UploadLessonImage/UploadLessonImageCommand.cs` | command | `sealed record UploadLessonImageCommand(Guid LessonId, IFormFile? File) : IRequest<UploadLessonImageResult>, IAuditableCommand` · `"Lesson.UploadImage"` · `"Lesson"` · `AuditResourceId => LessonId` |
| 21c | `Lessons/UploadLessonImage/UploadLessonImageResult.cs` | result | `sealed record UploadLessonImageResult(string Url);` |
| 21d | `Lessons/UploadLessonImage/UploadLessonImageValidator.cs` | validator | ctor `(IOptions<ContentOptions>)`: `LessonId.ValidateRequired(LessonIdRequired)` · `RuleFor(x => x.File).ValidateRequired(LessonImageRequired).ValidateAllowedExtensions(LessonImageFormats.Extensions, LessonImageTypeInvalid).ValidateMaxFileSize(options.LessonImageMaxSizeInMb, LessonImageTooLarge)` · `RuleFor(x => x.File).Must(file => file is null \|\| LessonImageFormats.ContentTypes.Contains(file.ContentType)).WithErrorCode(ErrorCodes.LessonImageTypeInvalid)` |
| 21e | `Lessons/UploadLessonImage/UploadLessonImageHandler.cs` | handler | `(ILessonRepository lessonRepository, IFileStorage fileStorage, ICurrentUserService currentUserService)`. 1. `lesson = lessonRepository.GetByIdAsync(request.LessonId, ct, asNoTracking: true)`. If null, throw `NotFoundCoreException(LessonNotFound)` 2. `var file = request.File!;` `var key = $"lessons/{lesson.Id}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";` 3. `await using var content = file.OpenReadStream();` `var url = await fileStorage.SaveAsync(content, key, ct)` 4. `return new UploadLessonImageResult(url)`. No `SaveChangesAsync`. |

**Audit** (goes into `docs/audit-log.md`):
| Command | AuditAction | ResourceType | Id source |
|---|---|---|---|
| CreateLesson | `Lesson.Create` | Lesson | result |
| UpdateLesson | `Lesson.Update` | Lesson | command (the diff lists the Lesson fields and every `LessonObjective` created, modified or deleted) |
| UploadLessonImage | `Lesson.UploadImage` | Lesson | command (no diff: only a file is written) |

### Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| 22 | `AppDbContext.cs` (modify, see above): `ConfigureLessons` | mapping | `modelBuilder.Entity<Lesson>(builder => { builder.Property(x => x.Name).IsRequired(); builder.Property(x => x.State).HasConversion<string>().HasMaxLength(EnumColumnMaxLength); builder.Property(x => x.Explanation).IsRequired(); builder.Property(x => x.Summary).IsRequired(); builder.HasOne<CurriculumUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict); builder.HasMany(x => x.Objectives).WithOne().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict); builder.HasIndex(x => new { x.UnitId, x.Order }); });` then `modelBuilder.Entity<LessonObjective>(builder => { builder.Property(x => x.Id).ValueGeneratedNever(); builder.Property(x => x.Text).IsRequired(); builder.HasIndex(x => new { x.LessonId, x.Order }); });` |
| 23 | `api/Elmanhg.Infrastructure/Lessons/LessonRepository.cs` | repo | `public class LessonRepository(AppDbContext context) : Repository<Lesson>(context), ILessonRepository`. `GetWithObjectivesAsync`: `var query = _dbSet.Include(x => x.Objectives.OrderBy(o => o.Order)).AsQueryable(); if (asNoTracking) { query = query.AsNoTracking(); } return await query.FirstOrDefaultAsync(x => x.Id == lessonId, cancellationToken).ConfigureAwait(false);`. `AnyInUnitAsync` and `CountByUnitAsync` are the same shape as `CurriculumUnitRepository` with `UnitId`. |
| 24 | `api/Elmanhg.Infrastructure/Storage/FileStorageProvider.cs` | enum | `namespace Elmanhg.Infrastructure.Storage; public enum FileStorageProvider { Local }` |
| 25 | `api/Elmanhg.Infrastructure/Storage/FileStorageOptions.cs` | options | `public sealed class FileStorageOptions { public const string SectionName = "FileStorage"; [Required] public FileStorageProvider? Provider { get; set; } [Required] public string LocalRootPath { get; set; } = string.Empty; [Required] public string PublicBaseUrl { get; set; } = string.Empty; }` |
| 26 | `api/Elmanhg.Infrastructure/Storage/LocalDiskFileStorage.cs` | adapter | `public sealed class LocalDiskFileStorage(IOptions<FileStorageOptions> fileStorageOptions, IHostEnvironment hostEnvironment) : IFileStorage`. `SaveAsync`: `var options = fileStorageOptions.Value; var root = Path.GetFullPath(options.LocalRootPath, hostEnvironment.ContentRootPath); var path = Path.GetFullPath(Path.Combine(root, key)); if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) { throw new ArgumentException("The storage key escapes the storage root.", nameof(key)); } Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root); await using var file = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous }); await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false); return $"{options.PublicBaseUrl.TrimEnd('/')}/{key}";` |
| 27 | `api/Elmanhg.Infrastructure/RichText/RichTextSanitizer.cs` | adapter | `public sealed class RichTextSanitizer : IRichTextSanitizer`. Private static arrays `AllowedTags` (D4 list), `AllowedAttributes` (`href src alt start data-type data-latex`), `AllowedSchemes` (`https http mailto`), `UriAttributes` (`href src`). Private readonly fields `HtmlSanitizer _sanitizer` and `string _imageUrlPrefix`. Ctor `(IOptions<FileStorageOptions> fileStorageOptions)`: `_imageUrlPrefix = $"{fileStorageOptions.Value.PublicBaseUrl.TrimEnd('/')}/"; _sanitizer = new HtmlSanitizer();` then for each of `AllowedTags`, `AllowedAttributes`, `AllowedSchemes`, `UriAttributes`: `.Clear()` then `.UnionWith(array)`. `_sanitizer.AllowedCssProperties.Clear(); _sanitizer.FilterUrl += FilterImageSource;`. `Sanitize(string? html)` → `if (string.IsNullOrWhiteSpace(html)) { return string.Empty; } return _sanitizer.Sanitize(html);`. `private void FilterImageSource(object? sender, FilterUrlEventArgs args)` → when `string.Equals(args.Tag.NodeName, "IMG", StringComparison.OrdinalIgnoreCase) && !args.OriginalUrl.StartsWith(_imageUrlPrefix, StringComparison.Ordinal)` set `args.SanitizedUrl = null`. |
| 28–29 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddLessons.cs` + `.Designer.cs` | migration | `dotnet ef migrations add AddLessons -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Expect `CreateTable("Lessons")` (FK `Units` Restrict, index `(UnitId, Order)`, `State` varchar(50)) and `CreateTable("LessonObjectives")` (FK `Lessons` Restrict, index `(LessonId, Order)`). No Drop, Rename or AlterColumn. |

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| 30 | `api/Elmanhg.Api/Controllers/Lessons/Requests.cs` | requests | `public sealed record CreateLessonRequest(Guid UnitId, string Name);` `public sealed record LessonObjectiveRequest(Guid? Id, string Text);` `public sealed record UpdateLessonRequest(string Name, string? Explanation, string? Summary, string? VideoUrl, List<LessonObjectiveRequest> Objectives);` |
| 31 | `api/Elmanhg.Api/Controllers/Lessons/LessonsController.cs` | controller | `[ApiController][Route("api/lessons")][Authorize] public class LessonsController(IMediator mediator) : ControllerBase`, with the 5 actions in API surface. Update maps `request.Objectives.Select(x => new LessonObjectiveContent(x.Id, x.Text)).ToList()`. Upload: `[Consumes("multipart/form-data")]`, parameters `([FromRoute] Guid lessonId, IFormFile? file, CancellationToken cancellationToken)`. |
| 32 | `api/Elmanhg.Api/FileStorage/LocalFileStorageExtensions.cs` | pipeline ext | `namespace Elmanhg.Api.FileStorage; public static class LocalFileStorageExtensions { public static WebApplication UseLocalFileStorage(this WebApplication app) }`: `var options = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value; if (options.Provider != FileStorageProvider.Local) { return app; } var root = Path.GetFullPath(options.LocalRootPath, app.Environment.ContentRootPath); Directory.CreateDirectory(root); app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(root), RequestPath = options.PublicBaseUrl, OnPrepareResponse = context => { context.Context.Response.Headers.XContentTypeOptions = "nosniff"; } }); return app;` |

### Tests (api)
| # | Path |
|---|------|
| 33–34 | `api/Elmanhg.Tests/Domain/Lessons/LessonTests.cs`, `LessonObjectiveTests.cs` |
| 35–39 | `api/Elmanhg.Tests/Application/Features/Lessons/{CreateLesson,UpdateLesson,GetLesson,GetLessons,UploadLessonImage}/<UseCase>HandlerTests.cs` |
| 40–44 | `api/Elmanhg.Tests/Application/Features/Lessons/{CreateLesson,UpdateLesson,GetLesson,GetLessons,UploadLessonImage}/<UseCase>ValidatorTests.cs` |
| 45 | `api/Elmanhg.Tests/Infrastructure/RichText/RichTextSanitizerTests.cs` (`Options.Create(new FileStorageOptions { Provider = FileStorageProvider.Local, LocalRootPath = "media", PublicBaseUrl = "/api/media" })`) |
| 46 | `api/Elmanhg.Tests/Infrastructure/Storage/LocalDiskFileStorageTests.cs`. `: IDisposable`. It uses a unique temp dir as `ContentRootPath` through an `IHostEnvironment` substitute and deletes the dir in `Dispose`. |
| 47 | `api/Elmanhg.Tests/Integration/Content/LessonsEndpointTests.cs` (helpers mirror `UnitsEndpointTests`: `AdminClientAsync`, `TeacherClientAsync`, `StudentClientAsync`, `SeedUnitAsync` = seed subject plus unit, `ReadCodeAsync`) |
| 48 | `api/Elmanhg.Tests/Integration/Content/LessonImagesEndpointTests.cs` (multipart helper `static MultipartFormDataContent ImageForm(string fileName, string contentType)`: `ByteArrayContent([0x89, 0x50, 0x4E, 0x47])` with `ContentType` set, added as `"file"`) |

Handler tests use NSubstitute for repositories, `IRichTextSanitizer`, `IFileStorage` and `ICurrentUserService`. Validator tests use `Options.Create(new ContentOptions { …all 9 caps at the D15/#60 values… })`, except where the row states a limit. `IFormFile` in tests is `new FormFile(new MemoryStream(bytes), 0, length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = type }`.

### Docs
| # | Path | Contract |
|---|------|----------|
| 49 | `docs/rich-text.md` | Title "Rich text format". Sections: **Storage** (sanitised HTML in `Lessons.Explanation` and `Lessons.Summary`, produced by TipTap `getHTML()`). **Allowed markup** (the D4 tag, attribute and scheme lists; no `style` or `class`; images only from `FileStorage:PublicBaseUrl`). **Math** (the two node shapes with examples; LaTeX lives in `data-latex`, never in text delimiters). **Sanitisation** (server `RichTextSanitizer` on every save; client `SafeHtml`/DOMPurify on every render). **Rendering** (`renderMath` → KaTeX `renderToString`, `displayMode` for `block-math`, `throwOnError: false`; `RichTextViewer` is the only renderer; LaTeX is `dir="ltr"` and isolated). **Images** (`POST /api/lessons/{id}/images`, the allow-list, the size cap key, the key shape, public-read by unguessable key, local provider at `/api/media`; S3 adapter pending). **Changing the format** (sanitiser allow-list, editor extensions and this doc change together). |

### Web (`web/src/features/content/` unless stated. Glass tokens only. Logical properties only.)
| # | Path | Type | Contract |
|---|------|------|----------|
| W-a | `web/src/shared/components/SafeHtml.tsx` | component | `export interface SafeHtmlProps { html: string; className?: string }` · `export function SafeHtml({ html, className }: SafeHtmlProps)` → `<div className={className} dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(html) }} />` (`import DOMPurify from 'dompurify'`). This is the only `dangerouslySetInnerHTML` in the app. |
| W-b | `web/src/shared/components/SafeHtml.test.tsx` | test | Rows W2–W3. |
| W-c | `web/src/shared/ui/dialog.tsx` | ui | `import { Dialog as DialogPrimitive } from 'radix-ui'` · `export const Dialog = DialogPrimitive.Root;` · `export interface DialogContentProps extends ComponentProps<typeof DialogPrimitive.Content> { title: string }` · `export function DialogContent({ title, children, className, ...props }: DialogContentProps)` → `<DialogPrimitive.Portal><DialogPrimitive.Overlay className="fixed inset-0 bg-overlay" /><DialogPrimitive.Content aria-describedby={undefined} className={cn('fixed inset-0 m-auto flex h-fit w-full max-w-105 flex-col gap-4 rounded-lg bg-surface p-5 shadow-2 focus-visible:outline-hidden', className)} {...props}><DialogPrimitive.Title className="font-display text-h3 font-semibold">{title}</DialogPrimitive.Title>{children}</DialogPrimitive.Content></DialogPrimitive.Portal>` |
| W-CSS | `web/src/styles/app.css` (modify) | css | `@layer components`. `.rich-text`: `font-size: var(--ds-type-body-size); line-height: var(--ds-type-body-line); color: var(--ds-color-text);` · `.rich-text > * + *`: `margin-block-start: var(--ds-space-3);` · `.rich-text h2`: `font-family: var(--font-display); font-size: var(--ds-type-h2-size); line-height: var(--ds-type-h2-line); font-weight: var(--ds-type-h2-weight);` · `.rich-text h3`: the same with the h3 tokens · `.rich-text ul`: `list-style: disc; padding-inline-start: var(--ds-space-6);` · `.rich-text ol`: `list-style: decimal; padding-inline-start: var(--ds-space-6);` · `.rich-text a`: `color: var(--ds-color-accent); text-decoration: underline;` · `.rich-text blockquote`: `background: var(--ds-color-soft); padding: var(--ds-space-3); border-radius: var(--ds-radius-sm);` · `.rich-text img`: `max-inline-size: 100%; block-size: auto; border-radius: var(--ds-radius-md);` · `.rich-text code, .rich-text pre, .rich-text [data-type='inline-math'], .rich-text [data-type='block-math']`: `direction: ltr; unicode-bidi: isolate;` · `.rich-text code, .rich-text pre`: `font-family: var(--font-mono); font-size: var(--ds-type-mono-size);` · `.rich-text pre`: `background: var(--ds-color-soft); padding: var(--ds-space-3); border-radius: var(--ds-radius-sm); overflow-x: auto;` · `.rich-text [data-type='block-math']`: `display: block; text-align: center; overflow-x: auto;` |
| W1 | `web/src/routes/admin/lesson.$lessonId.tsx` | route | `export const Route = createFileRoute('/admin/lesson/$lessonId')({ component: LessonEditorRoute });` · `function LessonEditorRoute() { const { lessonId } = Route.useParams(); return <LessonEditorPage lessonId={lessonId} />; }` (`LessonEditorPage` from `@/features/content`) |
| W2 | `components/UnitItem.tsx` | component | Props `{ subjectId: string; unit: UnitResult; isFirst: boolean; isLast: boolean; position: number }`. Renders an `<li className="flex flex-col gap-2 rounded-md border border-border bg-bg p-3">` containing: the name (`text-ui font-semibold`), `<p className="text-caption text-text-muted">{t('units.lessonCount', { count: unit.lessonCount })}</p>`, and `ItemActions` (the same wiring as today's `UnitPanel`, via `useUnitMutations(subjectId)`, error fields `{UNIT_NAME_REQUIRED:'name', UNIT_NAME_TOO_LONG:'name'}`). It also has a ghost `sm` toggle `Button` (`aria-expanded`, `aria-controls={panelId}` from `useId`, `units.showLessons`/`units.hideLessons`, local `useState`). When expanded it renders `<UnitLessons subjectId unitId={unit.id} unitName={unit.name} id={panelId} />`. |
| W3 | `components/UnitLessons.tsx` | component | Props `{ subjectId: string; unitId: string; unitName: string; id: string }`. `useGetLessons({ unitId })`. Pending → `ContentListSkeleton label=t('lessons.loading')`. Error → `ContentErrorState title=t('lessons.errorTitle')` with retry. `[]` → `ContentEmptyState t('lessons.empty')`. Otherwise `<ol aria-label={t('lessons.listLabel', { name: unitName })}>` of `<li key={lesson.id} className="flex flex-wrap items-center gap-2">` containing `<Link to="/admin/lesson/$lessonId" params={{ lessonId: lesson.id }} className="text-ui font-semibold text-accent underline">{lesson.name}</Link>` and `<LessonStateBadge state={lesson.state} />`. Below the list: `NameForm` (label `lessons.nameLabel`, submit `lessons.add`, fields `{LESSON_NAME_REQUIRED:'name', LESSON_NAME_TOO_LONG:'name'}`, `onSubmit={(name) => create(unitId, name)}`), where `create = useLessonCreate(subjectId)`. Wrapper `<div id={id} className="flex flex-col gap-3 border-t border-border pt-3">`. |
| W4 | `components/LessonStateBadge.tsx` | component | `{ state: string }` → `<span className={cn('rounded-full px-2.5 py-0.5 text-micro font-semibold', state === 'Published' ? 'bg-success text-surface' : 'bg-soft text-text-muted')}>{t([`lessons.state.${state}`, 'lessons.state.Draft'])}</span>` |
| W5 | `hooks/useLessonCreate.ts` | hook | `export function useLessonCreate(subjectId: string): (unitId: string, name: string) => Promise<void>`. `useCreateLesson({ mutation: { onSuccess: async () => { success('lessons.created'); await queryClient.invalidateQueries({ queryKey: getGetLessonsQueryKey() }); await queryClient.invalidateQueries({ queryKey: getGetSubjectQueryKey(subjectId) }); } } })`. Returns `async (unitId, name) => { const { id } = await createLesson.mutateAsync({ data: { unitId, name } }); await navigate({ to: '/admin/lesson/$lessonId', params: { lessonId: id } }); }` (`useNavigate` from `@tanstack/react-router`). |
| W6 | `hooks/useLessonEditor.ts` | hook | `export function useLessonEditor(lessonId: string): { save: (values: LessonValues) => Promise<void>; uploadImage: (file: File) => Promise<string> }`. `useUpdateLesson` `onSuccess`: `success('lessonEditor.saved')`, then invalidate `getGetLessonQueryKey(lessonId)` and `getGetLessonsQueryKey()`. `useUploadLessonImage` `onSuccess`: `success('lessonEditor.image.uploaded')`. `save` → `mutateAsync({ lessonId, data: toUpdateLessonRequest(values) })`. `uploadImage` → `(await mutateAsync({ lessonId, data: { file } })).url`. Both use `mutateAsync` so errors reach the owning `Form`. |
| W7 | `api/lessonValues.ts` | util | `export function toLessonValues(lesson: LessonDetailResult): LessonValues` → `{ name, explanation, summary, videoUrl: lesson.videoUrl ?? '', objectives: [...lesson.objectives].sort((a, b) => a.order - b.order).map((x) => ({ objectiveId: x.id, text: x.text })) }` · `export function toUpdateLessonRequest(values: LessonValues): UpdateLessonRequest` → `{ name: values.name, explanation: values.explanation, summary: values.summary, videoUrl: values.videoUrl === '' ? null : values.videoUrl, objectives: values.objectives.map((x) => ({ id: x.objectiveId, text: x.text })) }` · `export function toSafeVideoUrl(value: string): string \| null` → `URL.canParse(value) && ['http:', 'https:'].includes(new URL(value).protocol) ? value : null` |
| W8 | `api/lessonValues.test.ts` | test | Rows W4–W7. |
| W9 | `schemas/lessonSchema.ts` | schema | `export const lessonSchema = z.object({ name: z.string().trim().min(1, { error: 'validation.required' }), explanation: z.string(), summary: z.string(), videoUrl: z.string().trim().refine((value) => value === '' \|\| z.url({ protocol: /^https?$/ }).safeParse(value).success, { error: 'validation.url' }), objectives: z.array(z.object({ objectiveId: z.string().nullable(), text: z.string().trim().min(1, { error: 'validation.required' }) })) });` · `export type LessonValues = z.infer<typeof lessonSchema>;` |
| W10 | `schemas/lessonSchema.test.ts` | test | Rows W8–W13. |
| W11 | `schemas/formulaSchema.ts` | schema | `z.object({ latex: z.string().trim().min(1, { error: 'validation.required' }), block: z.boolean() })` · `FormulaValues` |
| W12 | `schemas/formulaSchema.test.ts` | test | Rows W14–W15. |
| W13 | `schemas/imageInsertSchema.ts` | schema | `// Mirrors LessonImageFormats on the API; the server is authoritative.` `export const acceptedImageTypes = 'image/png,image/jpeg,image/webp,image/gif';` · `export const imageInsertSchema = z.object({ file: z.instanceof(File, { error: 'validation.fileRequired' }), description: z.string().trim().min(1, { error: 'validation.required' }) });` · `ImageInsertValues` |
| W14 | `schemas/imageInsertSchema.test.ts` | test | Rows W16–W18. |
| W15 | `components/renderMath.ts` | util | `export function renderMath(html: string): string`: `const parsed = new DOMParser().parseFromString(html, 'text/html'); parsed.body.querySelectorAll<HTMLElement>('[data-type="inline-math"], [data-type="block-math"]').forEach((node) => { node.innerHTML = katex.renderToString(node.dataset.latex ?? '', { throwOnError: false, displayMode: node.dataset.type === 'block-math' }); }); return parsed.body.innerHTML;` |
| W16 | `components/renderMath.test.ts` | test | Rows W19–W22. |
| W17 | `components/RichTextViewer.tsx` | component | `import 'katex/dist/katex.min.css'` · `{ html: string }` → `<SafeHtml html={renderMath(html)} className="rich-text" />` |
| W18 | `pages/LessonEditorPage.tsx` | page | `export interface LessonEditorPageProps { lessonId: string }`. `useGetLesson(lessonId)`. `<section className="flex flex-col gap-4">`: `<nav aria-label={t('lessonEditor.breadcrumb')}><ol className="flex gap-2 text-caption text-text-muted"><li><Link to="/admin/content" className="text-accent underline">{t('lessonEditor.contentLink')}</Link></li>{data ? <li aria-current="page">{data.name}</li> : null}</ol></nav>`, then `<div className="flex flex-wrap items-center gap-2"><h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('lessonEditor.title')}</h1>{data ? <LessonStateBadge state={data.state} /> : null}</div>`. Pending → `ContentListSkeleton label=t('lessonEditor.loading')`. Error → `ContentErrorState title=t('lessonEditor.errorTitle')` with retry. Success → `<LessonEditorForm key={data.id} lesson={data} />`. |
| W19 | `components/LessonEditorForm.tsx` | component | `{ lesson: LessonDetailResult }`. `useForm<LessonValues>({ resolver: zodResolver(lessonSchema), defaultValues: toLessonValues(lesson) })`. `const { save, uploadImage } = useLessonEditor(lesson.id)`. `<Form form={form} onSubmit={save} serverErrorFields={lessonErrorFields} className="grid gap-4 lg:grid-cols-2 lg:items-start">`. Left: `<div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">` with `FormRootError`, `TextField name="name" label=t('lessonEditor.fields.name')`, `TextField name="videoUrl" label=t('lessonEditor.fields.videoUrl') dir="ltr"`, `RichTextField name="explanation" label=t('lessonEditor.fields.explanation') onUploadImage={uploadImage}`, `ObjectivesField`, `RichTextField name="summary" …`, `SubmitButton` (`lessonEditor.save`). Right: `<LessonPreview />`. `lessonErrorFields` (module const): `LESSON_NAME_REQUIRED`/`LESSON_NAME_TOO_LONG` → `name`; `LESSON_VIDEO_URL_INVALID`/`LESSON_VIDEO_URL_TOO_LONG` → `videoUrl`; `LESSON_EXPLANATION_TOO_LONG` → `explanation`; `LESSON_SUMMARY_TOO_LONG` → `summary`; `LESSON_OBJECTIVES_TOO_MANY`/`LESSON_OBJECTIVE_TEXT_REQUIRED`/`LESSON_OBJECTIVE_TEXT_TOO_LONG`/`LESSON_OBJECTIVE_DUPLICATE` → `objectives`. |
| W20 | `components/ObjectivesField.tsx` | component | `useFieldArray<LessonValues, 'objectives'>({ name: 'objectives' })` and `useFormState({ name: 'objectives' })`. `<fieldset className="flex flex-col gap-2"><legend className="text-caption text-text-muted">{t('lessonEditor.fields.objectives')}</legend>`. If there are no fields: `<p className="text-caption text-text-muted">{t('lessonEditor.objectives.empty')}</p>`. Otherwise `<ol className="flex flex-col gap-2">` with `<li key={field.id} className="flex flex-col gap-2">`: `TextField name={`objectives.${index}.text`} label={t('lessonEditor.objectives.itemLabel', { number: index + 1 })}`, then `sm` secondary icon buttons: `ChevronUp` (`aria-label` `objectives.moveUp`, `disabled={index === 0}`, `move(index, index - 1)`), `ChevronDown` (`moveDown`, disabled on last, `move(index, index + 1)`), `Trash2` (`objectives.remove`, `remove(index)`). A secondary `Button` `objectives.add` → `append({ objectiveId: null, text: '' })`. Root error: when `errors.objectives?.message` is set, render `<p className="text-caption text-danger">{t([message, 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}</p>`. |
| W21 | `components/RichTextField.tsx` | component | `{ name: 'explanation' \| 'summary'; label: string; onUploadImage: (file: File) => Promise<string> }`. `useController<LessonValues>({ name })`, `useId` → `labelId`, `errorId`. `<div className="flex flex-col gap-1.5"><span id={labelId} className="text-caption text-text-muted">{label}</span><RichTextEditor value={field.value} onChange={field.onChange} onBlur={field.onBlur} labelledBy={labelId} describedBy={fieldState.error ? errorId : undefined} invalid={fieldState.invalid} fieldLabel={label} onUploadImage={onUploadImage} />{fieldState.error ? <p id={errorId} className="text-caption text-danger">{t([fieldState.error.message ?? '', 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}</p> : null}</div>` |
| W22 | `components/RichTextEditor.tsx` | component | `import 'katex/dist/katex.min.css'`. Props `{ value: string; onChange: (html: string) => void; onBlur: () => void; labelledBy: string; describedBy?: string; invalid: boolean; fieldLabel: string; onUploadImage: (file: File) => Promise<string> }`. `const editor = useEditor({ extensions: [StarterKit.configure({ heading: { levels: [2, 3] }, link: { openOnClick: false } }), Image.configure({ allowBase64: false }), Mathematics.configure({ katexOptions: { throwOnError: false } })], content: value, editorProps: { attributes: { role: 'textbox', 'aria-multiline': 'true', 'aria-labelledby': labelledBy, ...(describedBy ? { 'aria-describedby': describedBy } : {}), 'aria-invalid': String(invalid), class: 'rich-text min-h-36 rounded-sm border border-border-strong bg-surface px-3 py-2.5 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden aria-invalid:border-danger' } }, onUpdate: ({ editor: current }) => { onChange(current.getHTML()); }, onBlur: () => { onBlur(); } }, [invalid, describedBy]);`. Local state `dialog: 'formula' \| 'image' \| null`. Render: `<RichTextToolbar editor fieldLabel onOpenFormula onOpenImage />`, `<EditorContent editor={editor} />`, then two `<Dialog open={dialog === 'x'} onOpenChange={(open) => { if (!open) { setDialog(null); } }}>`. The formula dialog is `DialogContent title=t('lessonEditor.formula.title')` → `FormulaInsertForm onInsert={({ latex, block }) => { editor.chain().focus()[block ? 'insertBlockMath' : 'insertInlineMath']({ latex }).run(); setDialog(null); }}` (write it as an explicit `if (block)`/else, not a computed member). The image dialog is `lessonEditor.image.title` → `ImageInsertForm onUpload={onUploadImage} onInsert={(src, alt) => { editor.chain().focus().setImage({ src, alt }).run(); setDialog(null); }}`. Both get `onCancel={() => { setDialog(null); }}`. Named imports: `StarterKit` from `@tiptap/starter-kit`, `Image` from `@tiptap/extension-image`, `Mathematics` from `@tiptap/extension-mathematics`, and `useEditor`, `EditorContent` from `@tiptap/react`. |
| W23 | `components/RichTextToolbar.tsx` | component | `{ editor: Editor; fieldLabel: string; onOpenFormula: () => void; onOpenImage: () => void }`. `useEditorState({ editor, selector: ({ editor: current }) => ({ bold: current.isActive('bold'), italic: current.isActive('italic'), heading: current.isActive('heading', { level: 2 }), bulletList: current.isActive('bulletList'), orderedList: current.isActive('orderedList') }) })`. `<div role="toolbar" aria-label={t('lessonEditor.toolbar.label', { field: fieldLabel })} className="flex flex-wrap gap-1">` with `sm` secondary icon `Button`s, each with an `aria-label` from `lessonEditor.toolbar.*`: Bold (`aria-pressed`, `toggleBold`), Italic (`Italic`, `toggleItalic`), Heading2 (`toggleHeading({ level: 2 })`), List (`toggleBulletList`), ListOrdered (`toggleOrderedList`), Sigma (`formula` → `onOpenFormula`), ImagePlus (`image` → `onOpenImage`). Each toggle runs `editor.chain().focus().toggleX().run()`. Icons are `aria-hidden className="size-4"`. |
| W24 | `components/FormulaInsertForm.tsx` | component | `{ onInsert: (values: FormulaValues) => void; onCancel: () => void }`. RHF with `zodResolver(formulaSchema)`, defaults `{ latex: '', block: false }`. `Form` → `TextField name="latex" label=t('lessonEditor.formula.latex') dir="ltr"`, then `<label className="flex items-center gap-2 text-ui"><input type="checkbox" {...form.register('block')} className="size-5 accent-text" />{t('lessonEditor.formula.block')}</label>`, then `SubmitButton` `lessonEditor.formula.insert` and a secondary cancel `Button` `actions.cancel`. |
| W25 | `components/ImageInsertForm.tsx` | component | `{ onUpload: (file: File) => Promise<string>; onInsert: (src: string, alt: string) => void; onCancel: () => void }`. RHF with `zodResolver(imageInsertSchema)`, defaults `{ description: '' }`. Server fields `{ LESSON_IMAGE_REQUIRED: 'file', LESSON_IMAGE_TYPE_INVALID: 'file', LESSON_IMAGE_TOO_LARGE: 'file' }`. `onSubmit` → `const url = await onUpload(values.file); onInsert(url, values.description);`. The file field uses `useController({ name: 'file' })`: `<label htmlFor={id} className="text-caption text-text-muted">{t('lessonEditor.image.file')}</label><input id={id} type="file" accept={acceptedImageTypes} aria-invalid={invalid} aria-describedby={error ? errorId : undefined} onChange={(event) => { field.onChange(event.target.files?.[0]); }} onBlur={field.onBlur} ref={field.ref} className="text-ui" />` plus the error `<p id={errorId}>` translated with `{ ns: 'common' }`. Then `TextField name="description" label=t('lessonEditor.image.description')`, `SubmitButton` `lessonEditor.image.insert`, and cancel. |
| W26 | `components/LessonPreview.tsx` | component | `const values = useWatch<LessonValues>();` `<section aria-label={t('lessonEditor.preview.title')} className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">`: `<h2 className="font-display text-h2 font-bold">{values.name}</h2>`, `<h3>{t('lessonEditor.fields.explanation')}</h3><RichTextViewer html={values.explanation ?? ''} />`, `<h3>{t('lessonEditor.fields.objectives')}</h3>` + `<ol className="list-decimal ps-6">` of objectives with non-empty trimmed text, each `<li key={`${String(objective.objectiveId)}-${objective.text}`}>{objective.text}</li>`, `<h3>{t('lessonEditor.fields.summary')}</h3><RichTextViewer html={values.summary ?? ''} />`, and when `toSafeVideoUrl(values.videoUrl ?? '')` is non-null: `<a href={url} target="_blank" rel="noopener noreferrer" dir="ltr" className="text-ui text-accent underline">{t('lessonEditor.preview.video')}</a>`. Every h3 has `className="font-display text-h3 font-semibold"`. |
| W27 | `pages/LessonEditorPage.test.tsx` | test | Rows W23–W32. |
| W28 | `components/RichTextEditor.test.tsx` | test | Rows W33–W36 (through `renderApp('/admin/lesson/l1', { session: testSessions.admin })`). |
| W29 | `components/UnitLessons.test.tsx` | test | Rows W37–W44 (through `renderApp('/admin/content', …)`, expanding "Show units" then "Show lessons"). |

Locale keys (`content` namespace, en | ar):
- `units.lessonCount` `{count, plural, one {# lesson} other {# lessons}}` | `{count, plural, zero {لا دروس} one {درس واحد} two {درسان} few {# دروس} many {# درسًا} other {# درس}}`
- `units.showLessons` Show lessons | عرض الدروس · `units.hideLessons` Hide lessons | إخفاء الدروس
- `lessons.listLabel` Lessons of {name} | دروس {name} · `lessons.loading` Loading lessons | جارٍ تحميل الدروس · `lessons.errorTitle` Could not load lessons | تعذّر تحميل الدروس · `lessons.empty` No lessons in this unit yet. | لا توجد دروس في هذه الوحدة بعد. · `lessons.nameLabel` Lesson name | اسم الدرس · `lessons.add` Add lesson | إضافة درس · `lessons.created` Lesson added. | تمت إضافة الدرس.
- `lessons.state.Draft` Draft | مسودة · `lessons.state.Published` Published | منشور · `lessons.state.Archived` Archived | مؤرشف
- `lessonEditor.title` Edit lesson | تحرير الدرس · `lessonEditor.breadcrumb` Breadcrumb | مسار التنقل · `lessonEditor.contentLink` Content | المحتوى · `lessonEditor.loading` Loading lesson | جارٍ تحميل الدرس · `lessonEditor.errorTitle` Could not load the lesson | تعذّر تحميل الدرس · `lessonEditor.save` Save | حفظ · `lessonEditor.saved` Lesson saved. | تم حفظ الدرس.
- `lessonEditor.fields.name` Name | الاسم · `.explanation` Explanation | الشرح · `.objectives` Objectives | الأهداف · `.summary` Summary | الملخص · `.videoUrl` Video link (optional) | رابط فيديو (اختياري)
- `lessonEditor.objectives.itemLabel` Objective {number} | الهدف {number} · `.add` Add objective | إضافة هدف · `.moveUp` Move objective {number} up | نقل الهدف {number} لأعلى · `.moveDown` Move objective {number} down | نقل الهدف {number} لأسفل · `.remove` Remove objective {number} | حذف الهدف {number} · `.empty` No objectives yet. | لا توجد أهداف بعد.
- `lessonEditor.preview.title` Preview | معاينة · `lessonEditor.preview.video` Watch the video | شاهد الفيديو
- `lessonEditor.toolbar.label` {field} formatting | تنسيق {field} · `.bold` Bold | عريض · `.italic` Italic | مائل · `.heading` Heading | عنوان · `.bulletList` Bulleted list | قائمة نقطية · `.orderedList` Numbered list | قائمة مرقمة · `.formula` Insert formula | إدراج معادلة · `.image` Insert image | إدراج صورة
- `lessonEditor.formula.title` Insert formula | إدراج معادلة · `.latex` LaTeX | صيغة LaTeX · `.block` Show on its own line | عرض في سطر مستقل · `.insert` Insert | إدراج
- `lessonEditor.image.title` Insert image | إدراج صورة · `.file` Image file | ملف الصورة · `.description` Image description | وصف الصورة · `.insert` Upload and insert | رفع وإدراج · `.uploaded` Image inserted. | تم إدراج الصورة.

Common (`shared/i18n`): `validation.url` Enter a link that starts with http:// or https://. | اكتب رابطًا يبدأ بـ http:// أو https://. · `validation.fileRequired` Choose a file. | اختر ملفًا.

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.LessonNotFound` | `LESSON_NOT_FOUND` | Update/GetLesson/UploadLessonImage handlers | `NotFoundCoreException` | 404 |
| `ErrorCodes.LessonIdRequired` | `LESSON_ID_REQUIRED` | Update/GetLesson/UploadLessonImage validators | validation | 422 |
| `ErrorCodes.LessonNameRequired` | `LESSON_NAME_REQUIRED` | Create/UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonNameTooLong` | `LESSON_NAME_TOO_LONG` | Create/UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonExplanationTooLong` | `LESSON_EXPLANATION_TOO_LONG` | UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonSummaryTooLong` | `LESSON_SUMMARY_TOO_LONG` | UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonVideoUrlInvalid` | `LESSON_VIDEO_URL_INVALID` | UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonVideoUrlTooLong` | `LESSON_VIDEO_URL_TOO_LONG` | UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonObjectivesTooMany` | `LESSON_OBJECTIVES_TOO_MANY` | UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonObjectiveTextRequired` | `LESSON_OBJECTIVE_TEXT_REQUIRED` | UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonObjectiveTextTooLong` | `LESSON_OBJECTIVE_TEXT_TOO_LONG` | UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonObjectiveDuplicate` | `LESSON_OBJECTIVE_DUPLICATE` | UpdateLessonValidator | validation | 422 |
| `ErrorCodes.LessonImageRequired` | `LESSON_IMAGE_REQUIRED` | UploadLessonImageValidator | validation | 422 |
| `ErrorCodes.LessonImageTypeInvalid` | `LESSON_IMAGE_TYPE_INVALID` | UploadLessonImageValidator | validation | 422 |
| `ErrorCodes.LessonImageTooLarge` | `LESSON_IMAGE_TOO_LARGE` | UploadLessonImageValidator | validation | 422 |
| `ErrorCodes.UnitIdRequired` (existing) | `UNIT_ID_REQUIRED` | CreateLesson/GetLessons validators | validation | 422 |
| `ErrorCodes.UnitNotFound` (existing) | `UNIT_NOT_FOUND` | CreateLesson/GetLessons handlers | `NotFoundCoreException` | 404 |
| Domain `ErrorCodes.UnitHasLessons` | `UNIT_HAS_LESSONS` | `CurriculumUnit.Delete` | `BusinessRuleViolationCoreException` | 400 |
| Domain `ErrorCodes.LessonObjectiveUnknown` | `LESSON_OBJECTIVE_UNKNOWN` | `Lesson.Update` | `BusinessRuleViolationCoreException` | 400 |
| Domain `ErrorCodes.ContentOrderInvalid` (existing) | `CONTENT_ORDER_INVALID` | `Lesson.Create`, `LessonObjective.Create/Update` | `BusinessRuleViolationCoreException` | 400 |

Resx (en | ar, no tashkeel, same spelling style as the existing ar.resx):
- LESSON_NOT_FOUND: Lesson not found. | الدرس غير موجود.
- LESSON_ID_REQUIRED: Choose a lesson. | اختر الدرس.
- LESSON_NAME_REQUIRED: Enter the lesson name. | اكتب اسم الدرس.
- LESSON_NAME_TOO_LONG: Lesson name is too long. | اسم الدرس طويل جدا.
- LESSON_EXPLANATION_TOO_LONG: The explanation is too long. | الشرح طويل جدا.
- LESSON_SUMMARY_TOO_LONG: The summary is too long. | الملخص طويل جدا.
- LESSON_VIDEO_URL_INVALID: Enter a video link that starts with http:// or https://. | اكتب رابط فيديو يبدا بـ http:// او https://.
- LESSON_VIDEO_URL_TOO_LONG: The video link is too long. | رابط الفيديو طويل جدا.
- LESSON_OBJECTIVES_TOO_MANY: This lesson has too many objectives. | عدد اهداف الدرس اكبر من المسموح.
- LESSON_OBJECTIVE_TEXT_REQUIRED: Every objective needs text. | كل هدف يحتاج الى نص.
- LESSON_OBJECTIVE_TEXT_TOO_LONG: An objective is too long. | احد الاهداف طويل جدا.
- LESSON_OBJECTIVE_DUPLICATE: An objective appears more than once. | يوجد هدف مكرر.
- LESSON_OBJECTIVE_UNKNOWN: An objective no longer exists. Reload the lesson and try again. | احد الاهداف لم يعد موجودا. اعد تحميل الدرس وحاول مرة اخرى.
- LESSON_IMAGE_REQUIRED: Choose an image. | اختر صورة.
- LESSON_IMAGE_TYPE_INVALID: Use a PNG, JPEG, WebP or GIF image. | استخدم صورة PNG او JPEG او WebP او GIF.
- LESSON_IMAGE_TOO_LARGE: The image is too large. | الصورة كبيرة جدا.
- UNIT_HAS_LESSONS: This unit has lessons and cannot be deleted. | لا يمكن حذف الوحدة لانها تحتوي على دروس.

Web `shared/i18n/{en,ar}.json` `errors.*`: the same text for all 17 codes above except `LESSON_ID_REQUIRED`.

## Domain behaviour
```csharp
// Lesson : AuditEntity, IAuditedEntity
public static Lesson Create(CurriculumUnit unit, string name, int order, Guid createdBy)
{
    if (order < 1) { throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid); }
    return new Lesson(Guid.NewGuid(), createdBy) { UnitId = unit.Id, Name = name.Trim(), Order = order, State = LessonState.Draft };
}

public void Update(string name, string explanation, string summary, string? videoUrl, IReadOnlyList<LessonObjectiveContent> objectives, Guid updatedBy)
{
    var keptIds = objectives.Where(x => x.Id.HasValue).Select(x => x.Id.GetValueOrDefault()).ToHashSet();
    if (keptIds.Any(id => Objectives.All(x => x.Id != id))) { throw new BusinessRuleViolationCoreException(ErrorCodes.LessonObjectiveUnknown); }

    foreach (var removed in Objectives.Where(x => !keptIds.Contains(x.Id)).ToList()) { removed.Delete(updatedBy); }

    for (var index = 0; index < objectives.Count; index++)
    {
        var content = objectives[index];
        if (content.Id is null) { Objectives.Add(LessonObjective.Create(Id, content.Text, index + 1, updatedBy)); }
        else { Objectives.Single(x => x.Id == content.Id).Update(content.Text, index + 1, updatedBy); }
    }

    Name = name.Trim(); Explanation = explanation; Summary = summary;
    VideoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim();
    UpdatedBy = updatedBy; UpdationDate = DateTimeOffset.UtcNow;
}

// LessonObjective : AuditEntity, IAuditedEntity
public static LessonObjective Create(Guid lessonId, string text, int order, Guid createdBy)
{
    if (order < 1) { throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid); }
    return new LessonObjective(Guid.NewGuid(), createdBy) { LessonId = lessonId, Text = text.Trim(), Order = order };
}
public void Update(string text, int order, Guid updatedBy)
{
    if (order < 1) { throw new BusinessRuleViolationCoreException(ErrorCodes.ContentOrderInvalid); }
    var trimmed = text.Trim();
    if (Text == trimmed && Order == order) { return; }
    Text = trimmed; Order = order; UpdatedBy = updatedBy; UpdationDate = DateTimeOffset.UtcNow;
}
public void Delete(Guid deletedBy) { SoftDelete(); UpdatedBy = deletedBy; UpdationDate = DateTimeOffset.UtcNow; }

// CurriculumUnit (modify)
public void Delete(bool hasLessons, Guid deletedBy)
{
    if (hasLessons) { throw new BusinessRuleViolationCoreException(ErrorCodes.UnitHasLessons); }
    SoftDelete(); UpdatedBy = deletedBy; UpdationDate = DateTimeOffset.UtcNow;
}
```
The code above is compressed. Write it with braces on their own lines, following the repo style. Order is guard, then mutate, then stamp; the unknown-objective guard runs before any mutation. `ErrorCodes` here is the Domain one. `using Core.Errors;` for the exception, mirroring `CurriculumUnit`.

## API surface
| Method | Route (Name) | Policy | Body | Response |
|---|---|---|---|---|
| GET | `/api/lessons?unitId={guid}` (`GetLessons`) | `DefaultCodes.ContentManage` | — | `200 List<LessonResult>` |
| GET | `/api/lessons/{lessonId:guid}` (`GetLesson`) | `ContentManage` | — | `200 LessonDetailResult` |
| POST | `/api/lessons` (`CreateLesson`) | `ContentManage` | `CreateLessonRequest` | `200 CreateLessonResult` |
| PUT | `/api/lessons/{lessonId:guid}` (`UpdateLesson`) | `ContentManage` | `UpdateLessonRequest` | `200` |
| POST | `/api/lessons/{lessonId:guid}/images` (`UploadLessonImage`) | `ContentManage` | multipart `file` | `200 UploadLessonImageResult` |
| GET | `/api/media/{**key}` | anonymous (static files, not a controller) | — | image bytes, `X-Content-Type-Options: nosniff` |
| GET | `/api/subjects/{subjectId}` (existing) | unchanged | — | `units[].lessonCount` added |

Every action has `[Authorize(Policy = …)]`, a `Name`, `[ProducesResponseType]` and `CancellationToken cancellationToken`, and returns `Ok(result)`/`Ok()`. List uses `[FromQuery] Guid unitId`.

## Test plan
### Domain (no doubles)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | LessonTests | `Create_Always_SetsUnitNameOrderAndDraftState` | UnitId, trimmed Name, Order, `State == Draft`, empty Explanation/Summary, null VideoUrl, no objectives, CreatedBy |
| 2 | LessonTests | `Create_OrderZero_ThrowsContentOrderInvalid` | type + code |
| 3 | LessonTests | `Update_NewContent_SetsFieldsAndUpdater` | trimmed Name, Explanation, Summary, trimmed VideoUrl, UpdatedBy |
| 4 | LessonTests | `Update_BlankVideoUrl_StoresNull` | `"  "` → null |
| 5 | LessonTests | `Update_NewObjectives_AddsThemInListOrder` | 2 objectives, Orders 1,2, LessonId, trimmed Text, CreatedBy = updater |
| 6 | LessonTests | `Update_ExistingObjectiveMoved_KeepsIdAndUpdatesTextAndOrder` | same Id, new Text, Order 1 |
| 7 | LessonTests | `Update_ObjectiveLeftOut_SoftDeletesIt` | omitted objective `IsDeleted`, UpdatedBy |
| 8 | LessonTests | `Update_UnknownObjectiveId_ThrowsLessonObjectiveUnknown` | type + code; Name unchanged; no objective deleted |
| 9 | LessonObjectiveTests | `Create_Always_SetsLessonTextOrderAndCreator` | trimmed Text |
| 10 | LessonObjectiveTests | `Create_OrderZero_ThrowsContentOrderInvalid` | |
| 11 | LessonObjectiveTests | `Update_Changed_SetsTextOrderAndUpdater` | |
| 12 | LessonObjectiveTests | `Update_Unchanged_LeavesUpdaterUnchanged` | UpdatedBy still equals creator |
| 13 | LessonObjectiveTests | `Update_OrderZero_ThrowsContentOrderInvalid` | Order unchanged |
| 14 | LessonObjectiveTests | `Delete_Always_SoftDeletesAndSetsUpdater` | |
| 15 | CurriculumUnitTests | `Delete_NoLessons_SoftDeletesAndSetsUpdater` (**modify**: renamed from `Delete_Always_…`, new signature) | IsDeleted, UpdatedBy |
| 16 | CurriculumUnitTests | `Delete_HasLessons_ThrowsUnitHasLessons` | type + code, IsDeleted false |

### Handlers (success asserts result/state plus `SaveChangesAsync` `Received(1)`; throw paths assert type, code and `SaveChangesAsync` `DidNotReceive()`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 17 | CreateLessonHandlerTests | `Handle_ExistingLessons_AddsDraftLessonAfterLastAndSaves` | last Order 2 → added lesson Order 3, UnitId, State Draft, result Id == added Id |
| 18 | CreateLessonHandlerTests | `Handle_NoLessons_AddsLessonAtOrderOne` | Order 1 |
| 19 | CreateLessonHandlerTests | `Handle_UnitNotFound_ThrowsUnitNotFound` | `AddAsync` DidNotReceive |
| 20 | CreateLessonHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 21 | UpdateLessonHandlerTests | `Handle_ExistingLesson_SanitisesContentUpdatesAndSaves` | sanitiser stubbed `"raw-e"`→`"clean-e"`, `"raw-s"`→`"clean-s"`; lesson Explanation `"clean-e"`, Summary `"clean-s"`, Name, one objective with its Text |
| 22 | UpdateLessonHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | sanitiser DidNotReceive |
| 23 | UpdateLessonHandlerTests | `Handle_UnknownObjective_ThrowsLessonObjectiveUnknown` | `BusinessRuleViolationCoreException`, domain code |
| 24 | UpdateLessonHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | |
| 25 | GetLessonHandlerTests | `Handle_ExistingLesson_ReturnsDetailWithOrderedObjectives` | every field incl. `State "Draft"`, objectives `(Id, Text, Order)` in order |
| 26 | GetLessonHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | |
| 27 | GetLessonsHandlerTests | `Handle_ExistingUnit_ReturnsMappedLessons` | `LessonResult` list equals expected (Id, UnitId, Name, Order, "Draft") |
| 28 | GetLessonsHandlerTests | `Handle_UnitNotFound_ThrowsUnitNotFound` | lesson `FindAsync` DidNotReceive |
| 29 | UploadLessonImageHandlerTests | `Handle_ExistingLesson_StoresUnderLessonKeyAndReturnsUrl` | file name `"Diagram.PNG"`; `fileStorage.Received(1).SaveAsync(Arg.Any<Stream>(), Arg.Is<string>(k => k.StartsWith($"lessons/{lesson.Id}/") && k.EndsWith(".png")), Arg.Any<CancellationToken>())`; result Url equals the stored URL |
| 30 | UploadLessonImageHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | `SaveAsync` DidNotReceive |
| 31 | UploadLessonImageHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `SaveAsync` DidNotReceive |
| 32 | GetSubjectHandlerTests | `Handle_ExistingSubject_ReturnsSubjectWithUnits` (**modify**) | units carry LessonCount 3 and 0 |
| 33 | DeleteUnitHandlerTests | existing 4 tests (**modify**: constructor only) | unchanged |
| 34 | DeleteUnitHandlerTests | `Handle_UnitHasLessons_ThrowsUnitHasLessons` | `AnyInUnitAsync` → true; `BusinessRuleViolationCoreException` + domain code; unit not deleted; DidNotReceive save |

### Validators (assert `Errors.Select(x => x.ErrorCode)` contains the code)
| # | Test class | Test methods |
|---|-----------|-------------|
| 37 | CreateLessonValidatorTests | `Validate_ValidCommand_Passes` · `Validate_EmptyUnitId_FailsWithUnitIdRequired` · `Validate_EmptyName_FailsWithLessonNameRequired` · `Validate_NameOverMax_FailsWithLessonNameTooLong` (101 chars) |
| 38 | UpdateLessonValidatorTests | `Validate_ValidCommand_Passes` · `Validate_NullVideoUrlAndEmptyContent_Passes` · `Validate_EmptyLessonId_FailsWithLessonIdRequired` · `Validate_EmptyName_FailsWithLessonNameRequired` · `Validate_NameOverMax_FailsWithLessonNameTooLong` · `Validate_ExplanationOverMax_FailsWithLessonExplanationTooLong` (100001) · `Validate_SummaryOverMax_FailsWithLessonSummaryTooLong` (20001) · `Validate_VideoUrlNotHttp_FailsWithLessonVideoUrlInvalid` (`"javascript:alert(1)"`) · `Validate_VideoUrlOverMax_FailsWithLessonVideoUrlTooLong` (`"https://example.com/" + 2030 × 'a'`) · `Validate_TooManyObjectives_FailsWithLessonObjectivesTooMany` (21) · `Validate_EmptyObjectiveText_FailsWithLessonObjectiveTextRequired` · `Validate_ObjectiveTextOverMax_FailsWithLessonObjectiveTextTooLong` (301) · `Validate_DuplicateObjectiveIds_FailsWithLessonObjectiveDuplicate` |
| 39 | GetLessonValidatorTests | `Validate_ValidQuery_Passes` · `Validate_EmptyLessonId_FailsWithLessonIdRequired` |
| 40 | GetLessonsValidatorTests | `Validate_ValidQuery_Passes` · `Validate_EmptyUnitId_FailsWithUnitIdRequired` |
| 41 | UploadLessonImageValidatorTests | `Validate_PngImage_Passes` · `Validate_EmptyLessonId_FailsWithLessonIdRequired` · `Validate_NullFile_FailsWithLessonImageRequired` · `Validate_SvgFile_FailsWithLessonImageTypeInvalid` (`x.svg`, `image/svg+xml`) · `Validate_ContentTypeMismatch_FailsWithLessonImageTypeInvalid` (`x.png`, `text/html`) · `Validate_OverMaxSize_FailsWithLessonImageTooLarge` (options `LessonImageMaxSizeInMb = 1`, length `1024 * 1024 + 1`) |

### Infrastructure (pure adapters, no DB)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 42 | RichTextSanitizerTests | `Sanitize_ScriptTag_RemovesIt` | output lacks `<script` and `alert` |
| 43 | RichTextSanitizerTests | `Sanitize_EventHandlerAttribute_RemovesIt` | `<p onclick="x()">Hi</p>` → `<p>Hi</p>` |
| 44 | RichTextSanitizerTests | `Sanitize_JavascriptLink_RemovesHref` | lacks `javascript:` |
| 45 | RichTextSanitizerTests | `Sanitize_StyleAndClassAttributes_RemovesThem` | lacks `style=` and `class=` |
| 46 | RichTextSanitizerTests | `Sanitize_AllowedFormatting_KeepsMarkup` | `<h2>`, `<strong>`, `<ul><li>`, `<a href="https://example.com">` kept verbatim |
| 47 | RichTextSanitizerTests | `Sanitize_MathNodes_KeepsLatexAttributes` | inline and block nodes keep `data-type` and `data-latex="\frac{a}{b}"` (HTML-encoded form as emitted) |
| 48 | RichTextSanitizerTests | `Sanitize_ImageFromStorage_KeepsSourceAndAlt` | `src="/api/media/lessons/x.png"`, `alt` kept |
| 49 | RichTextSanitizerTests | `Sanitize_ExternalImage_RemovesSource` | lacks `evil.example` |
| 50 | RichTextSanitizerTests | `Sanitize_BlankInput_ReturnsEmpty` | `null` and `"  "` → `""` (`[Theory]`) |
| 51 | LocalDiskFileStorageTests | `SaveAsync_ValidKey_WritesFileAndReturnsPublicUrl` | bytes on disk under `root/lessons/a/b.png`; returns `/api/media/lessons/a/b.png` |
| 52 | LocalDiskFileStorageTests | `SaveAsync_KeyEscapingRoot_ThrowsArgumentException` | key `../escape.png`; no file outside root |

### Integration (real PostgreSQL through `ApiFactory`, shared DB: assert on seeded ids only)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 53 | LessonsEndpointTests | `Post_Admin_CreatesDraftLessonAtEndAndAudits` | existing lesson Order 1 → new lesson 200, id, DB Order 2, `State == Draft`; audit `Lesson.Create` Success |
| 54 | LessonsEndpointTests | `Post_UnknownUnit_Returns404UnitNotFound` | status + code |
| 55 | LessonsEndpointTests | `Post_EmptyName_Returns422LessonNameRequired` | status + code contains |
| 56 | LessonsEndpointTests | `Post_Teacher_Returns403` | teacher assigned to the subject |
| 57 | LessonsEndpointTests | `Post_Anonymous_Returns401` | |
| 58 | LessonsEndpointTests | `GetList_Admin_ReturnsUnitLessonsInOrder` | seeded Orders 2,1 → body names in order 1,2, `state` "Draft" |
| 59 | LessonsEndpointTests | `GetList_UnknownUnit_Returns404UnitNotFound` | |
| 60 | LessonsEndpointTests | `GetById_Admin_ReturnsLessonWithOrderedObjectives` | objectives texts in seeded order, `videoUrl` null |
| 61 | LessonsEndpointTests | `GetById_UnknownLesson_Returns404LessonNotFound` | |
| 62 | LessonsEndpointTests | `GetById_Student_Returns403` | |
| 63 | LessonsEndpointTests | `Put_Admin_SanitisesContentAndAudits` | explanation `<p>Force</p><script>alert(1)</script><span data-type="inline-math" data-latex="F=ma"></span>` → DB contains `data-latex="F=ma"`, lacks `<script`; video URL stored; audit `Lesson.Update` Success with Diff not null |
| 63b | LessonsEndpointTests | `Put_ObjectiveList_KeepsIdsAddsNewAndSoftDeletesRemoved` | seed [A,B]; PUT [{B.id,"B2"},{null,"C"}] → A IsDeleted, B Order 1 Text "B2", a new objective Order 2 "C" |
| 63c | LessonsEndpointTests | `Put_UnknownObjectiveId_Returns400LessonObjectiveUnknown` | status + code; name unchanged in DB |
| 63d | LessonsEndpointTests | `Put_EmptyName_Returns422LessonNameRequired` | |
| 63e | LessonsEndpointTests | `Put_UnknownLesson_Returns404LessonNotFound` | |
| 63f | LessonsEndpointTests | `Put_Teacher_Returns403` | |
| 63g | LessonImagesEndpointTests | `Post_Admin_StoresServesAndAuditsImage` | 200; `url` starts `/api/media/lessons/{lessonId}/` and ends `.png`; `GET url` → 200, same bytes, `X-Content-Type-Options: nosniff`; audit `Lesson.UploadImage` Success |
| 63h | LessonImagesEndpointTests | `Post_SvgFile_Returns422LessonImageTypeInvalid` | |
| 63i | LessonImagesEndpointTests | `Post_UnknownLesson_Returns404LessonNotFound` | |
| 63j | LessonImagesEndpointTests | `Post_Teacher_Returns403` | |
| 63k | LessonImagesEndpointTests | `Post_Anonymous_Returns401` | |
| 64 | UnitsEndpointTests | `Delete_UnitWithLessons_Returns400UnitHasLessonsAndAuditsFailure` | 400 + code; unit not deleted; audit `Unit.Delete` Failure with ErrorCode `UNIT_HAS_LESSONS` |
| 65 | SubjectsEndpointTests | `GetById_Admin_ReturnsUnitLessonCounts` | unit with 2 seeded lessons → `lessonCount` 2; other unit 0 |

### Web (Vitest + RTL + MSW, using the generated `lessons.msw` / `subjects.msw` / `units.msw` handlers plus `http.*` overrides; `userEvent.setup()`)
| # | File | `it(...)` | Asserts |
|---|------|-----------|---------|
| W1 | Form.test.tsx | `does not submit an enclosing form` | inner `Form` rendered with `createPortal` inside an outer `<form onSubmit={spy}>`; submitting the inner form calls its `onSubmit` and not `spy` |
| W2 | SafeHtml.test.tsx | `removes scripts and event handlers` | `Hi` paragraph has no `onclick`; img `pic` has no `onerror`; `document.body` does not have text content `alert(1)` |
| W3 | SafeHtml.test.tsx | `keeps allowed markup and MathML` | `Bold` is a `STRONG` element; `getByRole('math')` present |
| W4 | lessonValues.test.ts | `maps a lesson to form values in objective order` | null video → `''`; objectives sorted by order → `{ objectiveId, text }` |
| W5 | lessonValues.test.ts | `maps form values to the update request` | `''` video → `null`; `objectiveId` → `id` |
| W6 | lessonValues.test.ts | `accepts http and https video links` | returns the value |
| W7 | lessonValues.test.ts | `rejects javascript and malformed video links` | `null` |
| W8 | lessonSchema.test.ts | `accepts a complete lesson` | |
| W9 | lessonSchema.test.ts | `rejects an empty name with validation.required` | |
| W10 | lessonSchema.test.ts | `rejects a whitespace-only name` | |
| W11 | lessonSchema.test.ts | `accepts an empty video link` | |
| W12 | lessonSchema.test.ts | `rejects a non-http video link with validation.url` | `javascript:alert(1)`, `ftp://x` |
| W13 | lessonSchema.test.ts | `rejects an empty objective with validation.required` | |
| W14 | formulaSchema.test.ts | `accepts LaTeX and trims it` | |
| W15 | formulaSchema.test.ts | `rejects empty LaTeX with validation.required` | |
| W16 | imageInsertSchema.test.ts | `accepts a file with a description` | |
| W17 | imageInsertSchema.test.ts | `rejects a missing file with validation.fileRequired` | |
| W18 | imageInsertSchema.test.ts | `rejects an empty description with validation.required` | |
| W19 | renderMath.test.ts | `renders inline math with KaTeX` | output contains `class="katex"` and `<math`, not `katex-display` |
| W20 | renderMath.test.ts | `renders block math in display mode` | contains `katex-display` |
| W21 | renderMath.test.ts | `leaves html without math unchanged` | `<p>Plain</p>` |
| W22 | renderMath.test.ts | `keeps invalid LaTeX as an error without throwing` | contains `katex-error` |
| W23 | LessonEditorPage.test.tsx | `shows the lesson after loading` | status `Loading lesson`, then Name field value, video field value with `dir="ltr"`, `Objective 1`/`Objective 2` values in order, badge `Draft`, preview region `Preview` shows the explanation text |
| W24 | LessonEditorPage.test.tsx | `shows an error and recovers on retry` | 500 → alert `Could not load the lesson` → Retry → Name field shown |
| W25 | LessonEditorPage.test.tsx | `shows that the lesson was not found` | 404 `LESSON_NOT_FOUND` → alert contains `Lesson not found.` |
| W26 | LessonEditorPage.test.tsx | `saves the edited lesson` | rename then Save → captured PUT body equals `{ name, explanation (unchanged loaded HTML), summary, videoUrl, objectives: [{ id: 'o1', … }, { id: 'o2', … }] }`; toast `Lesson saved.` |
| W27 | LessonEditorPage.test.tsx | `edits the objectives list` | Add objective → type `Explain inertia` in `Objective 3` → `Move objective 3 up` → `Remove objective 1` → Save → body objectives `[{ id: null, text: 'Explain inertia' }, { id: 'o2', text: 'Apply F = ma' }]`; preview list follows the edits |
| W28 | LessonEditorPage.test.tsx | `shows required errors without calling the API` | clear Name and Objective 1 → Save → two `This field is required.`; no PUT |
| W29 | LessonEditorPage.test.tsx | `shows a server error under the video link` | PUT 422 `LESSON_VIDEO_URL_INVALID` → `Enter a video link that starts with http:// or https://.` described by the video field |
| W30 | LessonEditorPage.test.tsx | `renders LaTeX from the explanation in the preview` | explanation fixture with an inline-math node → `within(preview).getAllByRole('math')` length 1 |
| W31 | LessonEditorPage.test.tsx | `renders right-to-left in Arabic` | heading `تحرير الدرس`, `document.documentElement` `dir="rtl"` |
| W32 | LessonEditorPage.test.tsx | `has no axe violations` | after load |
| W33 | RichTextEditor.test.tsx | `inserts an inline formula without saving the lesson` | Explanation toolbar → `Insert formula` → dialog → LaTeX `a^2+b^2` → Insert → dialog closed; no PUT yet; preview `math` count 2; Save → PUT explanation contains `data-latex="a^2+b^2"` |
| W34 | RichTextEditor.test.tsx | `keeps the formula dialog open on empty LaTeX` | Insert with empty → `This field is required.` inside the dialog |
| W35 | RichTextEditor.test.tsx | `uploads an image and inserts it with its description` | `Insert image` → `user.upload(Image file, diagram.png)` → description `Force diagram` → `Upload and insert` → POST `/api/lessons/l1/images` hit; `within(preview).getByRole('img', { name: 'Force diagram' })` has `src` `/api/media/lessons/l1/a.png`; toast `Image inserted.` |
| W36 | RichTextEditor.test.tsx | `shows the upload error inside the image dialog` | POST 422 `LESSON_IMAGE_TOO_LARGE` → `The image is too large.` within the dialog; dialog stays open |
| W37 | UnitLessons.test.tsx | `shows the lesson count of each unit` | `2 lessons` |
| W38 | UnitLessons.test.tsx | `shows the lessons of a unit with links to the editor` | `Show lessons` → `aria-expanded=true`; list `Lessons of Mechanics` in order; link `Newton's laws` `href="/admin/lesson/l1"`; badge `Draft` |
| W39 | UnitLessons.test.tsx | `shows the empty state for a unit without lessons` | `No lessons in this unit yet.` |
| W40 | UnitLessons.test.tsx | `shows an error and recovers on retry` | alert `Could not load lessons` → Retry → list |
| W41 | UnitLessons.test.tsx | `adds a lesson and opens the editor` | POST `/api/lessons` body `{ unitId: 'u1', name: 'Energy' }`; toast `Lesson added.`; `router.state.location.pathname === '/admin/lesson/l9'` (a `GetLesson` handler for l9 is registered) |
| W42 | UnitLessons.test.tsx | `shows the required error without calling the API` | empty Add lesson → `This field is required.`; no POST |
| W43 | UnitLessons.test.tsx | `shows an error toast when a unit with lessons cannot be deleted` | Delete → Yes, delete → DELETE 400 `UNIT_HAS_LESSONS` → toast `This unit has lessons and cannot be deleted.` |
| W44 | UnitLessons.test.tsx | `has no axe violations when lessons are shown` | |

## Definition of done
- [ ] `Lesson` and `LessonObjective` exist in `Elmanhg.Domain.Lessons` as `AuditEntity, IAuditedEntity`. Method bodies match Domain behaviour (guard, then mutate, then stamp). The unknown-objective guard runs before any mutation.
- [ ] `CurriculumUnit.Delete(bool hasLessons, Guid)` throws `UNIT_HAS_LESSONS`. `DeleteUnitHandler` asks `ILessonRepository.AnyInUnitAsync`.
- [ ] `UnitResult.LessonCount` is filled from `CountByUnitAsync` in one grouped SQL query (no N+1).
- [ ] 3 commands implement `IAuditableCommand` with the Action/ResourceType/Id in the Audit table. `CreateLessonResult` implements `IAuditableResult` explicitly.
- [ ] `UpdateLessonHandler` sanitises both rich-text fields through `IRichTextSanitizer` before `Lesson.Update`. No raw request HTML reaches the domain.
- [ ] `RichTextSanitizer` enforces exactly the D4 allow-list, and `img[src]` outside `PublicBaseUrl` is dropped. It is registered as a singleton.
- [ ] Upload: extension and content type are allow-listed (no SVG), size comes from `ContentOptions.LessonImageMaxSizeInMb`, and the key is `lessons/{lessonId}/{guid:N}{ext}`. The file is served at `/api/media/…` with `nosniff`. `LocalDiskFileStorage` rejects keys that escape the root.
- [ ] `IFileStorage` is selected by `FileStorage:Provider` through the switch factory. `FileStorageOptions` and the 7 new `ContentOptions` caps are bound with `ValidateDataAnnotations().ValidateOnStart()`, present in `appsettings.example.json` and `ApiFactory`, and announced to the dev as new config keys.
- [ ] Tests pass without `api/Elmanhg.Api/appsettings.json`. ApiFactory media goes to a temp dir that is deleted on dispose. `/api/Elmanhg.Api/App_Data/` is gitignored.
- [ ] Every handler guards the current user first (queries excepted). There is at most one `SaveChangesAsync`, no try/catch, `ConfigureAwait(false)` everywhere, and `asNoTracking: true` on every read that does not mutate.
- [ ] `ILessonRepository` adds only `GetWithObjectivesAsync`, `AnyInUnitAsync` and `CountByUnitAsync`.
- [ ] `AppDbContext`: two DbSets `{ get; set; }`, a named `ConfigureLessons`, Restrict FKs, `(UnitId, Order)` and `(LessonId, Order)` indexes, `State` as string(50), `LessonObjective.Id` `ValueGeneratedNever`, and 2 filter lines.
- [ ] Migration `AddLessons` has only CreateTable/CreateIndex. The snapshot is updated and `AppDbContextTests` stays green.
- [ ] All 17 codes are in both resx files. Web `errors.*` has 16 codes in en and ar, plus `validation.url` and `validation.fileRequired`.
- [ ] `LessonsController` is thin. Each action has `[Authorize(Policy = DefaultCodes.ContentManage)]`, a `Name`, `[ProducesResponseType]` and a `CancellationToken`. `EndpointAuthorizationTests` is green. `Program.cs` changes only by the one `UseLocalFileStorage()` line after `UseHttpsRedirection`.
- [ ] `HtmlSanitizer` 9.2.1039 is added through CPM. `dotnet list package --vulnerable --include-transitive` is clean. npm packages are pinned exactly as listed, and the lockfile is committed.
- [ ] `api/openapi/v1.json`, `web/src/shared/api/generated/**` and `web/src/routeTree.gen.ts` are regenerated. `npm run gen:api` produces no diff.
- [ ] `dangerouslySetInnerHTML` appears only in `shared/components/SafeHtml.tsx`. Every lesson HTML render goes through `RichTextViewer` → `renderMath` → `SafeHtml`.
- [ ] The editor renders LaTeX live (Mathematics extension). The preview renders the same HTML with KaTeX. LaTeX, URL and code are `dir="ltr"`/isolated.
- [ ] The formula and image dialogs are Radix dialogs. The shared `Form` stops submit propagation (W1). Submitting a dialog never saves the lesson (W33).
- [ ] Every data view (lesson list, editor) has loading, empty/not-found and error-with-retry states. Every mutation toasts on success, and errors are shown inline or as a toast.
- [ ] No hex, px, arbitrary Tailwind values or physical-direction utilities in `src/features` (the §14/§16 greps are clean). The only new CSS is `.rich-text`, and it uses `--ds-*` tokens only.
- [ ] Every new string exists in en and ar. Icon-only buttons have `aria-label`. The editor textbox is labelled. axe tests pass.
- [ ] The test files match the Test plan exactly. The only existing tests edited are those marked **modify**.
- [ ] Docs: `docs/audit-log.md` (3 rows, entities, uploads bullet, the "lessons" sentence), `docs/PRD.md` §15 `video_url?`, `docs/claude-design-prompt.md` §4 lesson-editor line, and the new `docs/rich-text.md`.
- [ ] Postman has a "Lessons" folder with 5 requests.
- [ ] `dotnet build` has zero new warnings. `dotnet test` is green. `dotnet format --verify-no-changes` exits 0. In `web/`: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .` and `npx vitest run --coverage` (thresholds met) all exit 0.
