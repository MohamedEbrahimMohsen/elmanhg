# CodeRabbit triage — PR #219 (#114 performance targets)

Source: `05-coderabbit-comments.md` (9 inline comments, 1 review body). Every claim was checked against the working tree on `feature/114-performance-targets` at 49c9b6e.

| # | Verdict | One line |
|---|---|---|
| RC1 | FIX (partial) | No #114 follow-up issue exists, but `docs/performance.md:181` says one tracks the miss |
| RC2 | REJECT | The only caller seeds a throwaway stack that `down -v` removes on any exit |
| RC3 | REJECT | No populated production table exists; CONCURRENTLY breaks the documented all-or-nothing migration guarantee |
| RC4 | FIX | The migration designer's target model is missing `TeacherVoiceDraft` and the `TeacherMessage` audio fields |
| RC5 | FIX | `perf:budget` measures brotli GENERIC mode, but the build ships TEXT mode |
| RC6 | REJECT | The budget is JS+CSS by definition; manifest `assets` holds every font subset in both formats |
| RC7 | FIX | The dock disappears on click and nothing shows until the lazy panel chunk arrives |
| RC8 | REJECT | Every rich-text source is re-serialised with double quotes before it reaches `hasMath` |
| RC9 | REJECT | The test passes deterministically, and its claimed failure mode does not occur |

Counts: 4 FIX, 5 REJECT, 0 ESCALATE.

---

## RC1: FIX (partial)

**Verified.** `gh issue list --state all --search "Follow-ups"` finds no "Follow-ups from #114" issue. Issues exist for #113, #92, #89 and others. `00-acceptance.md:9` (autopilot override) says: record the numbers, keep the budget, and "file a follow-up issue". `docs/performance.md:181` says "A follow-up issue tracks it." That is false today. `docs/deployment.md:402` reads "Performance (#114) | done", with no mention of the missed lesson budget, while `docs/performance.md:176-181` says the lesson budget is missed. The two docs give different answers to the same question.

**Not a product decision.** The acceptance override already decided what happens: the story completes with the miss recorded and a follow-up filed. Keeping the 2 s budget is already fixed.

**Fix.**
1. Orchestrator/lead: create the issue now, in the same shape as #213: title `Follow-ups from #114: lesson p75 budget (2.96 s vs 2 s), ...`, label `deferred`. The body is the measured numbers and the levers list from `docs/performance.md:183-187`.
2. `docs/performance.md:181`: replace "A follow-up issue tracks it." with "#N tracks it." (a link to the real issue).
3. `docs/deployment.md:402`: change the cell to `done ([docs/performance.md](performance.md)); the lesson p75 budget is missed (2.96 s locally, advisory in CI) and tracked in #N; CDN deferred until the live domain`.
4. Optional, same change: replace "the #114 follow-up" with "#N" at `docs/performance.md:73`, `docs/performance.md:99`, `docs/deployment.md:390`, and `deploy/loadtest/lesson-page.js:76` and `:82`.

**Rejected part.** Do not edit `.process/114-performance-targets/02-implementation-r2.md:62`. It is a historical pipeline report, and its note ("The lead should add its number when the issue is created") was accurate when it was written.

## RC2: REJECT

**Verified in part.** `UserManager.CreateAsync` commits each student (`LoadTestUsers.cs:34`). The subscription is saved later (`SeedLoadTestDataHandler.cs:50,55`). `CreateStudentAsync` returns null for an existing email (`LoadTestUsers.cs:27-30`). So a partially applied seed that is then reused would leave students without a subscription.

**Why reject.** That rerun never happens. The only caller is `deploy/load-test.sh:75`. A failed seed calls `fail`, and the EXIT trap (`deploy/load-test.sh:48-59`) always runs `down -v`, which destroys the database. Every run seeds a fresh volume. The "second run adds nothing" guarantee (`docs/performance.md:89`) is about a successful seed, and it holds.

## RC3: REJECT

**Verified.** `20260929173706_AddAttemptStudentCreatedAtIndex.cs:13-16` is a plain `CreateIndex`.

**Why reject.**
- No populated production `Attempts` table exists. `docs/deployment.md` lists "First live deploy" as still pending (it needs a VPS, a domain, DNS and secrets). This migration will run against an empty or near-empty table.
- `CREATE INDEX CONCURRENTLY` cannot run inside a transaction. `docs/deployment.md:341` promises "A migration that failed inside its transaction has changed nothing". A failed concurrent build leaves an INVALID index behind, and the migration history would then be half-applied. That trade is not worth making for a table with no data.
- A policy for large-table indexes belongs in a later story, once there is production data.

## RC4: FIX

**Verified.** Compare the model bodies of `20260929173706_AddAttemptStudentCreatedAtIndex.Designer.cs` and `AppDbContextModelSnapshot.cs` from `#pragma warning disable 612` onward. The Designer is a strict subset: it has no lines the snapshot lacks. The Designer is missing:
- the `TeacherMessage` properties `AudioDurationSeconds`, `AudioUrl` and `TranscriptFinal`, and the filtered `AudioUrl` index (Designer `:1764` against snapshot `:1761`);
- the whole `Elmanhg.Domain.TeacherThreads.TeacherVoiceDraft` entity and its relationship (snapshot `:1891-1970` and `:2592`).

The migration was scaffolded before the merge of origin/main (a374824) brought in `AddTeacherVoiceReplies`. It is the latest migration, so `dotnet ef migrations remove` on the next migration would reset the snapshot to this incomplete model, and the migration after that would try to re-create `TeacherVoiceDrafts`. The same gap in main's `AddAvatarConversations.Designer.cs` is pre-existing and out of scope.

**Fix.** In `20260929173706_AddAttemptStudentCreatedAtIndex.Designer.cs`, replace the body of `BuildTargetModel` (everything from `#pragma warning disable 612, 618` through `#pragma warning restore 612, 618`) with the body of `BuildModel` from `AppDbContextModelSnapshot.cs`. Keep the Designer's `[DbContext]` and `[Migration("20260929173706_AddAttemptStudentCreatedAtIndex")]` attributes, class name and usings. Do not touch the `.cs` migration or the snapshot. Check:
- the diff of the two bodies is empty;
- `dotnet build`;
- `dotnet ef migrations has-pending-model-changes` reports no changes;
- `AppDbContextTests` and `AttemptQueryPlanTests` pass.

## RC5: FIX

**Verified.** `web/scripts/perf/compress.ts:37-41` writes `.br` with quality 11 and `BROTLI_MODE_TEXT`. `web/scripts/perf/budgetCli.ts:11-12` recompresses with quality 11 and the default GENERIC mode. So the report and the gate measure different bytes from those the build ships. The difference is small, but the lesson page has only 16 KB of headroom (224/240).

**Fix (minimal; do not switch to reading `.br` files).** Files under 1 KB, and files where compression does not help, have no `.br` file, so reading them directly would need a fallback. Instead, in `web/scripts/perf/budgetCli.ts:12`, add `[constants.BROTLI_PARAM_MODE]: constants.BROTLI_MODE_TEXT` to the `params` object next to `BROTLI_PARAM_QUALITY`. Then run `npm run build && npm run perf:budget`. If any page's KB figure changes, update the "Bundle sizes after this change" line at `docs/performance.md:189`.

## RC6: REJECT

**Verified that `assets` exists.** The built `dist/.vite/manifest.json` has `assets` on `index.html` (54 files) and on `_katex-*.js` (59 files).

**Why reject.** The budget is defined as JS and CSS only: "A page is the union of the static-import closures of its manifest entries, plus their CSS" (`docs/performance.md:42`). Every `assets` entry in this build is a font: each Readex Pro and Noto Sans Arabic unicode-range subset, in both woff2 and woff, plus all KaTeX fonts. The browser downloads one format and only the subsets a page uses. Adding every asset would add several hundred KB that no page loads. Fonts are already a named lever in the lesson follow-up (`docs/performance.md:185,187`). No module in `web/src` imports an image file.

## RC7: FIX

**Verified.** In `web/src/features/avatar/components/AvatarDock.tsx:18-33`, clicking the dock sets `state.isOpen`, so the button unmounts (`:18`). The panel is a lazy chunk behind `<Suspense fallback={null}>` (`:31`). Until that chunk arrives, nothing is on screen. The chunk holds react-hook-form and the radix dialog (`docs/performance.md:189`), so on the 3G profile the page gives no visible response to the click for a noticeable time.

**Fix.**
1. In `AvatarDock.tsx`, move the dock button's `className` string (`:24`) into a module constant.
2. Replace `fallback={null}` with a non-interactive copy of the dock pill using that constant and the same `Sparkles` icon and `t('dock.open')` label: `<button type="button" disabled aria-busy="true" className={dockClassName}>...</button>`.

No new tokens or i18n keys are needed. Add a case to `AvatarDock.lazy.test.tsx`: after clicking the dock, before `vi.dynamicImportSettled()`, a button named "Assistant" is present and has `aria-busy="true"`.

## RC8: REJECT

**Verified.** `web/src/features/content/components/mathRenderer.ts:1` only matches `data-type="...-math"` with double quotes.

**Why reject.** No rich text reaching `RichTextViewer` can use single quotes or spaced attributes:
- Stored rich text goes through `RichTextSanitizer` (Ganss.Xss HtmlSanitizer on AngleSharp), which re-serialises every attribute as `name="value"`.
- The editor output (`@tiptap/extension-mathematics`, `docs/rich-text.md:27-30`) and `lazyImages` (DOMParser, then innerHTML) are also DOM serialisations.
- The server-built math spans (`QuestionImportText.cs:34`, `LoadTestLessonContent.cs:12-13`) use double quotes.

The single-quoted input in the comment cannot be produced.

## RC9: REJECT

**Verified.** `web/src/features/content/components/RichTextViewer.test.tsx:28` asserts `(await screen.findByText('F=ma')).tagName === 'math'`. `npx vitest run` on `RichTextViewer.test.tsx` and `mathRenderer.test.ts` passes: 5/5.

**Why reject.** The claim is that the assertion "can fail when the formula renders correctly". It does not fail, and it is deterministic. The pinned DOMPurify removes KaTeX's `semantics` and `annotation` and keeps their content, so the TeX source text ends up as a direct child of `<math>`, which is exactly what the test finds. The test also constrains the code: before `renderMath` runs, the fallback holds only an empty span with a `data-latex` attribute, so `findByText('F=ma')` would time out.

Optional hardening (not required for this PR): assert that `.katex math` exists, so the test does not depend on how DOMPurify handles `annotation`.
