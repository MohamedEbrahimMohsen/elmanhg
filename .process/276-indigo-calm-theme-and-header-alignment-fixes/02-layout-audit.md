# Layout audit — Indigo calm theme and header/alignment fixes (#276)

## 1. Environment

- **Code:** `feature/276-indigo-calm-theme-and-header-alignment-fixes` = base commit `0e8e8a2f` + the uncommitted #276 working tree. **Baseline ("before"):** `git archive 0e8e8a2f web` built the same way.
- **Server:** each tree was built with `vite build` and served with `vite preview` (after on :5276, before on :5277). `API_PROXY_TARGET=http://localhost:8080` points at the running demo stack (Caddy → demo api, every provider fake). `npm run dev` could not be used: in dev mode the app never mounts, on base `0e8e8a2f` as well as on this branch. The error is `getI18n()` returning `undefined` in `registerDashboardLocales` (`src/features/dashboard/locales.ts:7`), because `routes/admin/index.tsx` imports the dashboard barrel before `initI18n()` runs. This happens before #276 and is not caused by it.
- **Driver:** headless Chrome 154 driven over CDP by a Node script. The script resizes with `Emulation.setDeviceMetricsOverride` (width 375 / 768 / 1280, height 900, `mobile` below 900), opens each route with a full load, pastes `layout-audit.js` unchanged, waits for `aria-busy` to clear and calls `__layoutAudit.audit()`. Language: the script's `setLang` imports `/src/app/i18n.ts`, which only exists in dev mode. So the driver switched language by calling `changeLanguage` on the app's own i18next instance, taken from the `I18nextProvider` props in the React tree. Every finding records `dir`, and `ar` → `rtl` and `en` → `ltr` held on every route.
- **Accounts:** the demo stack's seeded admin, teacher and first load-test student, as printed by `scripts/demo.sh` (commit `9780b428`). The credentials are not recorded here. The public role ran signed out.
- **Discovered routes:** the first in-app link found with `__layoutAudit.links(prefix)` on the list page. The quiz session and the unit exam session were started from the UI (practice «٥ أسئلة», «ابدأ الامتحان»). The admin lesson, new-question and import routes reuse the student's lesson id.
- **Not reachable in the demo data:** `/student/thread/:id`, `/teacher/q/:id`, `/teacher/thread/:id` and `/teacher/grade/…`. The demo student has no Ask-a-Teacher add-on (`/student/ask-new` shows the paid add-on upsell), so no thread exists. The teacher's validation queue, inbox and grade-review queue are empty. These pages use the same `AppShell`, `pillTabClassName` and shared buttons as the audited pages.

## 2. Matrix

| role | width | lang | routes audited | findings before | findings after |
|---|---|---|---|---|---|
| public | 375 | ar | 4 | 6 | 4 |
| public | 375 | en | 4 | 6 | 4 |
| public | 768 | ar | 4 | 6 | 4 |
| public | 768 | en | 4 | 6 | 4 |
| public | 1280 | ar | 4 | 6 | 4 |
| public | 1280 | en | 4 | 6 | 4 |
| student | 375 | ar | 19 | 53 | 10 |
| student | 375 | en | 19 | 53 | 10 |
| student | 768 | ar | 19 | 53 | 10 |
| student | 768 | en | 19 | 53 | 10 |
| student | 1280 | ar | 19 | 71 | 10 |
| student | 1280 | en | 19 | 71 | 10 |
| teacher | 375 | ar | 5 | 10 | 0 |
| teacher | 375 | en | 5 | 15 | 0 |
| teacher | 768 | ar | 5 | 10 | 0 |
| teacher | 768 | en | 5 | 10 | 0 |
| teacher | 1280 | ar | 5 | 15 | 0 |
| teacher | 1280 | en | 5 | 15 | 0 |
| admin | 375 | ar | 17 | 62 | 0 |
| admin | 375 | en | 17 | 62 | 0 |
| admin | 768 | ar | 17 | 62 | 0 |
| admin | 768 | en | 17 | 62 | 0 |
| admin | 1280 | ar | 17 | 79 | 0 |
| admin | 1280 | en | 17 | 79 | 0 |

Routes: public `/`, `/login`, `/signup`, `/accept-invite`. Student: the 8 static routes, subject, unit, lesson (+ objectives, summary, practice), quiz-result, exam-result, exam-start, quiz session, exam session. Teacher: the 5 static routes. Admin: the 11 static routes, question detail, student detail, avatar conversation, lesson, new question, import.

Totals by check. Before: header-rows 246, header-height 86, target-size 390, row-heights 96, edges 36, table-align 6, table-scroll 6, tabbar-heights 5. After: edges 36, target-size 48. All 84 remaining findings are rows D1 and D2 below (intended).

## 3. Findings

| # | route | width | dir | check | target | detail | fix (file:line) | status |
|---|---|---|---|---|---|---|---|---|
| F1 | `/student/exam/:id` | all | both | sticky (probe) | exam timer card | before: timer `top` 0 under a 57/101 px bar; after: app bar bottom 56, timer top 64 | `web/src/features/exam/components/ExamHeader.tsx:29` `top-16` | fixed |
| F2 | `/student/exam/:id` | 375 | both | sticky (probe) | submit bar | before: submit bottom 700 with the tab bar top at 643 (hidden); after: submit bottom 636. Overlap (review r1): the assistant dock (`fixed bottom-24`, z-10, 96–144 px above the viewport bottom) covered the top 38 px of the stuck bar (64–134 px above the bottom), including the Ask button (77–121 px). r2: the dock is not rendered on `/student/exam/$sessionId`, so nothing sits in the 64–134 px band; the bar's own «اسأل المساعد» / "Ask the assistant" still opens the panel. Rects are computed from the classes (56 bar + 8; 2 + 24 + 44 bar height), not re-measured in a browser | `web/src/features/exam/components/ExamRunner.tsx:66` `bottom-above-tab-bar`; utility `web/src/styles/app.css`; `web/src/features/avatar/components/AvatarDock.tsx:15` (`useMatch` on the exam route); test `AvatarDock.test.tsx` "hides the dock button while an exam is being taken" | fixed |
| F3 | `/student/exam-result/:id` | all | both | table-align | th «درّب الآن» / "Train now" | header center vs cell start | `web/src/styles/app.css:125` `th { text-align: start }` | fixed |
| F4 | question editors (remove buttons) | — | — | code audit | icon buttons beside fields | 36 px button, `mt-6` hack vs a 44 px field | `size="icon"` + `mt-6.5`: `DiagramItemRow.tsx:55`, `FillBlanksField.tsx:34`, `MathAnswersField.tsx:35`, `MathSolutionField.tsx:35`, `ModelAnswersField.tsx:32`, `RubricLevelsField.tsx:42` | fixed (no DOM repro: the demo has no fill-blanks/math/essay/drag-drop drafts open on these routes) |
| F5 | `/admin/configuration` | all | both | row-heights | setting forms | control heights 44/36 | default size: `NumberSettingForm.tsx:59`, `ChoiceSettingForm.tsx:58`, `BooleanSettingForm.tsx:53`, `ChoiceListSettingForm.tsx:50` | fixed |
| F6 | `/admin/more`, `/admin/blueprints`, placeholders | — | — | code audit | page root | `gap-3` vs `gap-4` | `shell/pages/MorePage.tsx:15`, `shell/pages/PlaceholderPage.tsx:11`, `blueprints/pages/ExamBlueprintsPage.tsx:42` | fixed |
| F7 | every shell route | all | both | header-rows / header-height | `header` | 2 rows, 101 px at 1280 | `shell/components/AppBar.tsx`, `TopTabs.tsx`, `shared/ui/layout.ts:3` | fixed |
| F8 | `/student/lesson/:id/*` | — | — | code audit | lesson tabs | 12 px pills | `browse/components/LessonTabs.tsx:18` `pillTabClassName` | fixed |
| F9 | every shell route | — | — | code audit | skip link | `focus:z-20` under the header | `shell/components/AppShell.tsx:22` `focus:z-50` | fixed |
| F10 | every shell route ≤ 899 | — | — | code audit | tab bar | no safe-area padding | `shell/components/TabBar.tsx:22` `pb-safe-area`; `web/index.html:5` `viewport-fit=cover` | fixed |
| F11 | every route | — | — | code audit | `html` | scroll padding for the 101 px header | `web/src/styles/app.css:122` | fixed |
| F12 | dialogs, assistant | — | — | code audit | overlays | no z-index under sticky bars | `shared/ui/dialog.tsx:14,18`, `avatar/components/AvatarPanel.tsx:25,28` | fixed |
| E1 | every route | all | both | target-size | logo link | 48×22 (ar) / 71×22 (en) | `web/src/shared/ui/layout.ts:8` `inline-flex min-h-11 items-center` | fixed |
| E2 | `/student` | all | both | target-size | «تعديل موادي» / "Edit my subjects" | 81×22 / 119×22 | `web/src/features/mastery/components/HomeSubjects.tsx:31` `inline-flex min-h-11 items-center` | fixed |
| E3 | `/student/lesson/:id` (+ tabs) | all | both | target-size | previous/next lesson link | 343×22, 167×22 | `web/src/features/browse/components/LessonNavigation.tsx:11` `min-h-11` | fixed |
| E4 | `/student/multi-exam` | all | both | target-size | «استكمل الامتحان» / "Continue that exam" | 102×22 / 139×22 | `web/src/features/exam/components/MultiExamSubjectSection.tsx:55`; the same pattern in `ExamStartActions.tsx:35,51` | fixed |
| E5 | `/admin/student/:id` | all | both | target-size | back link «المستخدمون» / "Users" | 20 px tall | `web/src/features/users/pages/StudentDetailPage.tsx:72` `min-h-11` | fixed |
| E6 | `/admin/avatar-conversation/:id` | all | both | target-size | back link | 111×20 / 164×20 | `web/src/features/avatarConversations/pages/AvatarConversationPage.tsx:64` `min-h-11` | fixed |
| E7 | `/admin/question/:id` | all | both | target-size | "Option N is correct" checkbox/radio (aria-label, no wrapping label) | 18×18 | `web/src/features/questions/components/ChoiceOptionRow.tsx:31,47` `mt-2.5 size-6` (24 px, centred on the 44 px field) | fixed |
| E8 | `/admin/question/import/:id` | all | both | target-size | file input | 309×22 | `web/src/features/questions/components/QuestionImportForm.tsx:65` `min-h-11` | fixed |
| E9 | `/admin/configuration` | all | both | table-scroll | providers table | not inside an overflow-x-auto wrapper | `web/src/features/configuration/components/IntegrationTable.tsx:21` wrapped in `<div className="overflow-x-auto">` | fixed |
| E10 | `/teacher/*` | 375 | ltr | tabbar-heights | bottom navigation | 56/56/60/56 ("Student questions" wraps) | `shell/components/TabBar.tsx` (`gap-0.5`, `leading-none` label, 28 px icon pill) | fixed |
| E11 | — | — | — | static check 4 | `NotFound`, `RouteError` | own `mx-auto max-w-layout px-4` container | `web/src/shared/components/NotFound.tsx:10`, `RouteError.tsx:20` `cn(layoutContainerClassName, 'py-6 in-[main]:px-0 lg:in-[main]:px-0')`: the container keeps its gutter at the root (no shell) and drops it inside the shell's `main`, which already has one (review r1) | fixed |
| E12 | `/student/quiz/:id` result, drag-drop review | — | — | contrast (D1) | correct-answer icon on `success.soft` | `text-success` is 2.84:1 on `success.soft` | `web/src/features/questions/components/ChoiceAnswerInputs.tsx:89`, `DiagramItemChip.tsx:60` → `text-success-text` | fixed |
| D1 | `/`, `/student/subscription`, `/student/exam/:id` | all | both | edges | hero / subscribe header / exam timer contents (h1, p, CTA) | starts 20 px (32 px at lg on the hero; 17 px in the exam card) from the page content edge | — | intended: the page root here is an aurora or timer card. Its contents sit inside the card padding (`p-5`, `lg:p-8`, `p-4` + 1 px border). The card itself starts on the content edge. |
| D2 | `/student/subject/:id`, `/student/unit/:id` | all | both | target-size | unit / lesson card-title links | full card width × 22 px | — | intended: block-level links as wide as the card, separated from the next target (mastery bar, caption, unit-exam button) by at least 8 px. They meet WCAG 2.5.8 through the spacing exception (a 24 px circle on the link touches no other target). Making each title 44 px would add 22 px to every card in the list. |

Also verified in the DOM. With `aria-pressed=true`, the `aria-pressed:bg-accent-soft` rule comes after `hover:bg-soft` in the built CSS (`dist/assets/index-*.css`, offset 45728 vs 43089). The active pill keeps its fill on hover. The header-centre, tabbar-centre, overflow, icon-centre and row-centres checks found nothing before or after.

## 4. Static checks (from `web/`)

```
$ grep -rnE '\bbg-text\b|\baccent-text\b|has-checked:border-text' src --include=*.tsx
(empty)
$ grep -rnE 'bg-(success|danger) text-surface|text-surface/80' src --include=*.tsx
(empty)
$ grep -rn 'className="mt-6"' src --include=*.tsx
(empty)
$ grep -rnE 'max-w-layout' src --include=*.tsx --include=*.ts
src/shared/ui/layout.ts:1:export const layoutContainerClassName = 'mx-auto w-full max-w-layout px-4 lg:px-6';
$ grep -rnE '\b(sticky|fixed)\b' src --include=*.tsx   (test files excluded)
features/avatar/components/AvatarDock.tsx:9        fixed … z-10 bottom-24        assistant FAB, sits above the tab bar, under every overlay
features/avatar/components/AvatarPanel.tsx:25      fixed inset-0 z-40 bg-overlay   assistant backdrop
features/avatar/components/AvatarPanel.tsx:28      fixed … z-50                     assistant sheet
features/exam/components/ExamHeader.tsx:29         sticky top-16 z-10               exam timer under the app bar
features/exam/components/ExamRunner.tsx:66         sticky bottom-above-tab-bar      mobile submit bar above the tab bar (lg:static)
features/questions/components/DiagramDragGhost.tsx:9  fixed inset-0 z-50          drag ghost while dragging (pointer-events-none)
features/shell/components/AppShell.tsx:22          focus:fixed focus:z-50           skip link
features/shell/components/TabBar.tsx:22            fixed bottom-0 z-20              tab bar
shared/ui/dialog.tsx:14,18                         overlay z-40, content z-50
(shared/ui/layout.ts:3 appBarClassName: sticky top-0 z-20, the app bar; TopNavMore panel z-30)
```
Every hit matches the z-scale: bars z-20, exam header z-10 `top-16`, More menu z-30, overlays z-40, dialog and sheet content z-50.
