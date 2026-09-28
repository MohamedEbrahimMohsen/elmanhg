# Implementation — [E2.S2] Lesson authoring with explanation, objectives and summary (#61)

## Files created
89 hand-written files, plus 14 new Orval-generated files under `web/src/shared/api/generated/` (`lessons/`, `zod/lessons/` and 11 model files).

| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Lessons/LessonState.cs | 3 | `Draft, Published, Archived` |
| api/Elmanhg.Domain/Lessons/LessonObjectiveContent.cs | 3 | value object `(Guid? Id, string Text)` |
| api/Elmanhg.Domain/Lessons/LessonObjective.cs | 55 | child entity: Create / Update (no-op if unchanged) / Delete |
| api/Elmanhg.Domain/Lessons/Lesson.cs | 73 | aggregate: Create (Draft), Update (full-list objective replace, unknown-id guard runs first) |
| api/Elmanhg.Domain/Lessons/ILessonRepository.cs | 10 | GetWithObjectivesAsync, AnyInUnitAsync, CountByUnitAsync |
| api/Elmanhg.Application/Shared/RichText/IRichTextSanitizer.cs | 6 | port |
| api/Elmanhg.Application/Shared/Storage/IFileStorage.cs | 6 | port |
| api/Elmanhg.Application/Lessons/Shared/{LessonResult,LessonObjectiveResult,LessonDetailResult}.cs | 3 each | results |
| api/Elmanhg.Application/Lessons/Shared/LessonResultGenerator.cs | 21 | Generate / GenerateDetail (objectives ordered) |
| api/Elmanhg.Application/Lessons/CreateLesson/{Command,Result,Validator,Handler}.cs | 11/8/20/33 | `Lesson.Create`, id from result |
| api/Elmanhg.Application/Lessons/UpdateLesson/{Command,Validator,Handler}.cs | 12/33/31 | sanitise both fields, then `Lesson.Update` |
| api/Elmanhg.Application/Lessons/GetLesson/{Query,Validator,Handler}.cs | 6/13/21 | detail read, no tracking |
| api/Elmanhg.Application/Lessons/GetLessons/{Query,Validator,Handler}.cs | 6/13/26 | lessons of a unit, ordered |
| api/Elmanhg.Application/Lessons/UploadLessonImage/LessonImageFormats.cs | 8 | extension + content-type allow-list (WHY comment) |
| api/Elmanhg.Application/Lessons/UploadLessonImage/{Command,Result,Validator,Handler}.cs | 12/3/27/32 | `Lesson.UploadImage`, key `lessons/{id}/{guid:N}{ext}` |
| api/Elmanhg.Infrastructure/Lessons/LessonRepository.cs | 35 | Include objectives, Any, grouped count |
| api/Elmanhg.Infrastructure/Storage/{FileStorageProvider,FileStorageOptions,LocalDiskFileStorage}.cs | 3/17/25 | `FileStorage` options + local disk adapter with root-escape guard |
| api/Elmanhg.Infrastructure/RichText/RichTextSanitizer.cs | 51 | HtmlSanitizer with the D4 allow-list; drops `img[src]` outside PublicBaseUrl |
| api/Elmanhg.Infrastructure/Migrations/20260928010248_AddLessons.cs + .Designer.cs | 91 / 968 | CreateTable Lessons, LessonObjectives + 2 indexes only |
| api/Elmanhg.Api/Controllers/Lessons/Requests.cs | 7 | CreateLessonRequest, LessonObjectiveRequest, UpdateLessonRequest |
| api/Elmanhg.Api/Controllers/Lessons/LessonsController.cs | 68 | 5 actions, all `ContentManage` |
| api/Elmanhg.Api/FileStorage/LocalFileStorageExtensions.cs | 30 | `UseStaticFiles` at `/api/media` with `nosniff` |
| api/Elmanhg.Tests/Domain/Lessons/LessonTests.cs | 119 | rows 1–8 |
| api/Elmanhg.Tests/Domain/Lessons/LessonObjectiveTests.cs | 77 | rows 9–14 |
| api/Elmanhg.Tests/Application/Features/Lessons/*/…HandlerTests.cs (5) | 47–79 | rows 17–31 |
| api/Elmanhg.Tests/Application/Features/Lessons/*/…ValidatorTests.cs (5) | 26–126 | rows 37–41 |
| api/Elmanhg.Tests/Infrastructure/RichText/RichTextSanitizerTests.cs | 89 | rows 42–50 |
| api/Elmanhg.Tests/Infrastructure/Storage/LocalDiskFileStorageTests.cs | 48 | rows 51–52 |
| api/Elmanhg.Tests/Integration/Content/LessonsEndpointTests.cs | 272 | rows 53–63f |
| api/Elmanhg.Tests/Integration/Content/LessonImagesEndpointTests.cs | 116 | rows 63g–63k |
| docs/rich-text.md | 54 | format contract |
| web/src/shared/components/SafeHtml.tsx (+ .test.tsx) | 11 / 25 | DOMPurify sink; W2–W3 |
| web/src/shared/ui/dialog.tsx | 28 | Radix Dialog + DialogContent |
| web/src/routes/admin/lesson.$lessonId.tsx | 11 | editor route |
| web/src/features/content/components/UnitItem.tsx | 59 | unit row: lesson count, actions, lessons disclosure |
| web/src/features/content/components/UnitLessons.tsx (+ .test.tsx) | 72 / 182 | lesson list, states, add lesson; W37–W44 |
| web/src/features/content/components/LessonStateBadge.tsx | 21 | state badge |
| web/src/features/content/hooks/useLessonCreate.ts | 25 | create, then navigate to the editor |
| web/src/features/content/hooks/useLessonEditor.ts | 43 | save + uploadImage |
| web/src/features/content/api/lessonValues.ts (+ .test.ts) | 30 / 67 | mappers + `toSafeVideoUrl`; W4–W7 |
| web/src/features/content/schemas/{lessonSchema,formulaSchema,imageInsertSchema}.ts (+ tests) | 21/8/11 (+41/12/22) | W8–W18 |
| web/src/features/content/components/renderMath.ts (+ .test.ts) | 12 / 24 | KaTeX on `[data-latex]` nodes; W19–W22 |
| web/src/features/content/components/RichTextViewer.tsx | 11 | renderMath then SafeHtml |
| web/src/features/content/pages/LessonEditorPage.tsx (+ .test.tsx) | 54 / 183 | page, loading/error; W23–W32 |
| web/src/features/content/components/LessonEditorForm.tsx | 58 | form, server-error map, two-column layout |
| web/src/features/content/components/ObjectivesField.tsx | 79 | field array: add, move, remove |
| web/src/features/content/components/RichTextField.tsx | 42 | controller + label/error wiring |
| web/src/features/content/components/RichTextEditor.tsx (+ .test.tsx) | 117 / 118 | TipTap editor + formula/image dialogs; W33–W36 |
| web/src/features/content/components/RichTextToolbar.tsx | 85 | toolbar |
| web/src/features/content/components/FormulaInsertForm.tsx | 37 | LaTeX dialog form |
| web/src/features/content/components/ImageInsertForm.tsx | 79 | file + description dialog form |
| web/src/features/content/components/LessonPreview.tsx | 44 | live student-view preview |
| web/src/test/nodeFormData.ts | 8 | **not in plan**, see Deviations #4 |

## Files modified
| Path | Change |
|---|---|
| .gitignore | `/api/Elmanhg.Api/App_Data/` |
| api/Directory.Packages.props, api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj | HtmlSanitizer 9.2.1039 through CPM |
| api/Elmanhg.Domain/Units/CurriculumUnit.cs | `Delete(bool hasLessons, Guid deletedBy)` throws `UNIT_HAS_LESSONS` |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | `UnitHasLessons`, `LessonObjectiveUnknown` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `// LESSONS` group, 15 codes |
| api/Elmanhg.Application/Shared/Options/ContentOptions.cs | 7 lesson caps |
| api/Elmanhg.Application/Units/Shared/UnitResult.cs, UnitResultGenerator.cs | `LessonCount` |
| api/Elmanhg.Application/Subjects/Shared/SubjectResultGenerator.cs, GetSubject/GetSubjectHandler.cs | lesson counts from `CountByUnitAsync` (one grouped query) |
| api/Elmanhg.Application/Units/DeleteUnit/DeleteUnitHandler.cs | asks `AnyInUnitAsync` and passes the answer to `Delete` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | 2 DbSets, `ConfigureLessons`, 2 filter lines |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | FileStorage options + switch factory, sanitiser (singleton), lesson repo |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Api/Program.cs | `using Elmanhg.Api.FileStorage;` + `app.UseLocalFileStorage();` after `UseHttpsRedirection` |
| api/Elmanhg.Api/Resources/Messages.{en,ar}.resx | 17 keys each |
| api/Elmanhg.Api/appsettings.example.json | 7 Content keys + `FileStorage` section (also mirrored into the local gitignored appsettings.json) |
| api/openapi/v1.json | regenerated by the build |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | `MediaRoot`, 10 config keys, delete MediaRoot on dispose |
| api/Elmanhg.Tests/Integration/Content/ContentTestData.cs | `SeedLessonAsync`, `ReadLessonAsync` |
| api/Elmanhg.Tests/Domain/Units/CurriculumUnitTests.cs | row 15 rename + new signature, row 16 |
| api/Elmanhg.Tests/Application/Features/Units/DeleteUnit/DeleteUnitHandlerTests.cs | lesson repo substitute; row 34 |
| api/Elmanhg.Tests/Application/Features/Subjects/GetSubject/GetSubjectHandlerTests.cs | row 32 |
| api/Elmanhg.Tests/Integration/Content/UnitsEndpointTests.cs | row 64 |
| api/Elmanhg.Tests/Integration/Content/SubjectsEndpointTests.cs | row 65 |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | **not in plan**, see Deviations #3 |
| postman/elmanhg.postman_collection.json | "Lessons" folder (5 requests, collection bearer auth like Units) + `lessonId` variable |
| docs/audit-log.md | 3 rows, entities, the "question commands" sentence, the uploads bullet |
| docs/PRD.md | §15 `video_url?` |
| docs/claude-design-prompt.md | §4 lesson-editor line |
| web/package.json, web/package-lock.json | the 8 exact-pinned packages |
| web/src/shared/form/Form.tsx | `event.stopPropagation()` before `handleSubmit` |
| web/src/shared/form/Form.test.tsx | W1 |
| web/src/shared/form/TextField.tsx | optional `dir?: 'ltr'` |
| web/src/test/setup.ts | Range/elementFromPoint jsdom gaps; also `import './nodeFormData'` (Deviation #4) |
| web/src/styles/app.css | `@layer components` `.rich-text` rules (tokens only) |
| web/src/features/content/components/UnitPanel.tsx | renders `UnitItem` |
| web/src/features/content/components/UnitPanel.test.tsx | fixtures gain `lessonCount: 0` |
| web/src/features/content/index.ts | exports `LessonEditorPage`, `RichTextViewer` |
| web/src/features/content/i18n/{en,ar}.json | all listed locale keys |
| web/src/shared/i18n/{en,ar}.json | `validation.url`, `validation.fileRequired`, 16 `errors.*` |
| web/src/routeTree.gen.ts | regenerated by `vite build` |
| web/src/shared/api/generated/** | regenerated by `npm run gen:api` |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| 1. `VideoUrl.ValidateUrl(...).ValidateMaxLength(...)`, and row 38 `Validate_NullVideoUrlAndEmptyContent_Passes` | Core `ValidateUrl` has `.When(x => …x?.ToString())` where `x` is the **command**, not the property. So a null or blank URL fails with `LESSON_VIDEO_URL_INVALID`. | Appended `.When(x => !string.IsNullOrWhiteSpace(x.VideoUrl))` to the VideoUrl rule. Core is left untouched. |
| 2. `RuleFor(x => x.File).ValidateRequired(LessonImageRequired)…` | Core `ValidateRequired(IFormFile?)` is `NotNull().Must(file!.Length > 0)`. `NotNull` gets the default code, and `Must` throws a NullReferenceException on null (row 41 `Validate_NullFile…` crashed). | Prefixed the chain with `.Cascade(CascadeMode.Stop).NotNull().WithErrorCode(ErrorCodes.LessonImageRequired)`. |
| 3. "`AppDbContextTests` stays green". The file is not in *Existing code touched*. | `Migrate_FreshDatabase_LeavesNoPendingMigrations` hard-codes the list of applied migrations, so any new migration fails it. | Appended `sixth => sixth.Should().EndWith("_AddLessons")`, the same edit #60 made. Nothing else in the test changed. This is an unlisted test edit, so the reviewer should confirm it. |
| 4. Create exactly the listed files. setup.ts gets only the ProseMirror gaps. | W35/W36 could not pass. Under vitest-jsdom, `FormData`/`File` are jsdom's, and Node's fetch cannot serialise them (`Cannot read properties of undefined (reading '_buffer')`), so every multipart upload failed as NETWORK_ERROR. The swap must run before `@/app/i18n` imports the content barrel, because `z.instanceof(File)` captures the class at load. | Added `web/src/test/nodeFormData.ts`, which swaps in Node's `FormData`/`File`/`Blob` and has `/// <reference types="node" />`. It is imported first in `setup.ts`. |
| 5. `SafeHtml` is the only `dangerouslySetInnerHTML` | The repo ESLint `no-restricted-syntax` rule bans the attribute everywhere, SafeHtml included. | Added one `eslint-disable-next-line no-restricted-syntax -- …` in SafeHtml.tsx. `eslint.config.js` is not touched. |
| 6. W3 `getByRole('math')`, W30/W33 `getAllByRole('math')` | jsdom `getComputedStyle` crashes on MathML elements (no `style`), and jest-dom `toBeInTheDocument` rejects non-HTML/SVG elements. | These queries use `{ hidden: true }`, and W3 asserts `tagName === 'math'`. |
| 7. `RichTextEditor` prop `describedBy?: string` | `exactOptionalPropertyTypes` rejects passing `undefined` explicitly. | `describedBy?: string \| undefined`. |
| 8. `toLessonValues`: `a.order - b.order` | The generated `order` type is `number \| string`. | `Number(a.order) - Number(b.order)`. |
| 9. ObjectivesField `name={`objectives.${index}.text`}` | `restrict-template-expressions` rejects a number in a template. | `` `objectives.${index.toString()}.text` as `objectives.${number}.text` ``. |
| 10. ImageInsertForm contract (no FormRootError) | Without it, unmapped server errors from the upload (e.g. LESSON_NOT_FOUND, network) would vanish silently. | Added `<FormRootError />` at the top of the form. |
| 11. Toolbar: `aria-pressed` on Bold | — | `aria-pressed` on all five toggles (the selector already computes them), through a small local `Tool` component. |

## Build & test
- `dotnet build api/` → `Build succeeded. 0 Warning(s) 0 Error(s)`.
- `dotnet test api/ -c Release` (normal) → `Test run summary: Passed! total: 465 failed: 0 succeeded: 465 skipped: 0`, exit 0 (Testcontainers PostgreSQL on local Docker).
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside → `Passed! total: 465 failed: 0 succeeded: 465 skipped: 0`, exit 0. The file was restored afterwards and now contains the `FileStorage` section and the 7 Content keys.
- `dotnet format api/Elmanhg.slnx --verify-no-changes` → only the known whitespace noise in `api/core-libraries` (108 lines, all core-libraries). Nothing in Elmanhg.* projects.
- `dotnet list api/Elmanhg.slnx package --vulnerable --include-transitive` → every project "has no vulnerable packages".
- `dotnet ef migrations add AddLessons` → CreateTable ×2, CreateIndex ×2, no Drop/Rename/Alter.
- `npm --prefix web run build` → `✓ built`, exit 0. Warning: the main chunk is 1,215 kB (see notes).
- `npm --prefix web test -- --run --coverage` → `Test Files 42 passed (42) · Tests 211 passed (211)`, exit 0, and the `src/features/**` thresholds are met (All files 93.58% statements / 79.88% branches).
- `npm --prefix web run typecheck` (`tsc -b`) → exit 0. `npm --prefix web run lint` (`eslint . --max-warnings=0`) → exit 0.
- `npx prettier --check` on every new or changed web file → "All matched files use Prettier code style!". The whole-repo `prettier --check .` still reports the known Windows CRLF noise.
- `npm run gen:api` after the API build → it regenerated `lessons/`, the models, the zod schemas and `lessonCount`. Re-running it gives no further diff.

## Notes for review
- **Bundle size:** `app/i18n.ts` imports the `@/features/content` barrel for its locales, and that barrel now re-exports `LessonEditorPage`/`RichTextViewer`. That pulls TipTap, KaTeX and DOMPurify into the main chunk (1.2 MB) instead of the lazy `lesson.$lessonId` route chunk. The fix would be splitting the locale export from the component barrel, which is outside the plan's file list, so I did not do it.
- `nodeFormData.ts` uses `/// <reference types="node" />`. It is inside `tsconfig.app.json`'s `src`, so Node globals become visible to the app type-check. tsc and eslint are clean.
- `RichTextToolbar` line coverage is about 54%: the Test plan has no row that clicks Bold/Italic/lists. Branch coverage for `RichTextEditor`/`RichTextField` is also low (block-math branch, error branch).
- `LessonImageFormats` is a named constant (plan D15), not an option. Constitution §0.3 lists "allowed extensions" under tunables. The plan argues it is a security invariant, so I followed the plan.
- `RichTextSanitizer` is a singleton wrapping one `HtmlSanitizer`. Ganss documents `Sanitize` as thread-safe once configured. The configuration happens once, in the constructor.
- When an external image is stripped, an `<img alt="…">` with no `src` remains. The test only asserts that the source is gone.
- `CreateLesson` ordering uses the "last + 1" pattern from #60, so concurrent creates in one unit can get the same `Order`. The `(UnitId, Order)` index is not unique, same as Units.
- When the Local provider is active, `UseLocalFileStorage` creates `api/Elmanhg.Api/App_Data/media` at host start, including the build-time OpenAPI run. That path is now gitignored.
- New config keys for the dev: `Content:LessonNameMaxLength`, `LessonExplanationMaxLength`, `LessonSummaryMaxLength`, `LessonObjectiveMaxLength`, `LessonObjectivesMaxCount`, `LessonVideoUrlMaxLength`, `LessonImageMaxSizeInMb`, and the `FileStorage` section (`Provider`, `LocalRootPath`, `PublicBaseUrl`). All are validated on start.
- Nothing was committed, pushed or branch-switched.
