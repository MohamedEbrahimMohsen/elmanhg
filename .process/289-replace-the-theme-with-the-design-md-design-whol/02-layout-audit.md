# Layout audit — Replace the theme with the DESIGN.md design (#289)

## 1. Environment

- **Code ("after"):** branch `feature/289-replace-the-theme-with-the-design-md-design-whol` = base commit `0bc2a3b1` (origin/main, contains #286 and #288) + the uncommitted #289 working tree. **Baseline ("before"):** `git archive 0bc2a3b1 web` built the same way.
- **Server:** each tree was built for production (`tsc -b && vite build`) and served with `vite preview` (after on :5289, before on :5290). `API_PROXY_TARGET=http://localhost:8080` proxies `/api` to the running demo stack (Caddy → demo api, every provider fake).
- **Driver:** headless Chrome over CDP from a Node script (not committed). It sets the width with `Emulation.setDeviceMetricsOverride` (375 / 768 / 1280, height 900, `mobile` below 900), loads the role's first route, pastes `layout-audit.js` unchanged, waits for `aria-busy` to clear and `document.fonts.ready`, then moves between routes with `__layoutAudit.go()` and calls `__layoutAudit.audit()`. English: `/src/app/i18n.ts` does not exist in a production build, so the driver imported the built `en.json` chunks listed in `dist/.vite/manifest.json`, added them to the app's own i18next instance (taken from the `I18nextProvider` props in the React tree) and called `changeLanguage('en')`. Every finding records `dir`; `ar` → `rtl` and `en` → `ltr` held on every route, and the English text is translated (for example `/` reads "450 questions checked by real teachers").
- **Accounts:** the demo stack's seeded admin, teacher and first load-test student, as printed by `scripts/demo.sh` (commit `9780b428`). The credentials are not recorded here. The public role ran with cookies cleared.
- **Discovered routes:** first in-app link from `__layoutAudit.links(prefix)` (with a short retry). Quiz session: practice size «5» clicked on `/student/lesson/:id/practice`. Exam session: «ابدأ الامتحان» / «استكمل» on `/student/exam-start/:id`, then submitted («تسليم الامتحان» → «تسليم») so the assistant is not in exam mode on the next pass. Multi-exam: the first two enabled unit option cards checked (#286). Admin lesson, new-question and import routes use the student's lesson id (the content tree is collapsed, so it exposes no lesson link).
- **Assistant routes:** the first full pass left an exam in progress, which puts `/student/assistant` in exam mode (no list, so no `/student/assistant/:id` link). The exam was submitted and a supplementary pass re-audited `/student/assistant` and `/student/assistant/:id` for both builds in all six combinations; the final "after" pass also submits its exam, so it reached both routes everywhere.
- **Not reachable in the demo data:** `/student/thread/:id`, `/teacher/q/:id`, `/teacher/thread/:id` and `/teacher/grade/…`, as in #276 (no Ask-a-Teacher add-on, empty teacher queues). They use the same `AppShell`, buttons and pill tabs as the audited pages.
- **Fonts (`__layoutAudit.fonts()`):** `/student` at 1280 ar → `Poppins 400, Poppins 700, Poppins 800, Almarai 400, Almarai 700, Almarai 800`. `/admin` at 1280 en → the same six faces (the admin pages render Arabic seed content, so Almarai loads in English too). The required `Almarai 400`, `Almarai 700`, `Poppins 400`, `Poppins 700` are present on both.

## 2. Matrix

| role | width | lang | routes audited | findings before | findings after |
|---|---|---|---|---|---|
| public | 375 | ar | 4 | 4 | 4 |
| public | 375 | en | 4 | 4 | 4 |
| public | 768 | ar | 4 | 4 | 4 |
| public | 768 | en | 4 | 4 | 4 |
| public | 1280 | ar | 4 | 4 | 5 |
| public | 1280 | en | 4 | 4 | 5 |
| student | 375 | ar | 22 | 10 | 2 |
| student | 375 | en | 22 | 10 | 2 |
| student | 768 | ar | 22 | 10 | 2 |
| student | 768 | en | 22 | 10 | 2 |
| student | 1280 | ar | 22 | 10 | 2 |
| student | 1280 | en | 22 | 136 | 2 |
| teacher | 375 | ar | 5 | 0 | 0 |
| teacher | 375 | en | 5 | 0 | 0 |
| teacher | 768 | ar | 5 | 0 | 0 |
| teacher | 768 | en | 5 | 0 | 0 |
| teacher | 1280 | ar | 5 | 0 | 0 |
| teacher | 1280 | en | 5 | 0 | 0 |
| admin | 375 | ar | 17 | 1 | 1 |
| admin | 375 | en | 17 | 1 | 1 |
| admin | 768 | ar | 17 | 1 | 1 |
| admin | 768 | en | 17 | 1 | 1 |
| admin | 1280 | ar | 17 | 1 | 1 |
| admin | 1280 | en | 17 | 1 | 1 |

Routes: public `/`, `/login`, `/signup`, `/accept-invite`. Student: the 9 static routes (`/student`, `/student/progress`, `/student/multi-exam`, `/student/ask`, `/student/ask-new`, `/student/subscription`, `/student/more`, `/onboarding`, `/student/assistant`), `/student/assistant/:id`, `/student/multi-exam` with two units chosen, quiz-result, exam-result, subject, unit, lesson (+ objectives, summary, practice), quiz session, exam-start, exam session. Teacher: the 5 static routes. Admin: the 11 static routes, question detail, student detail, avatar conversation, lesson, new question, import.

Totals by check. Before: edges 36, target-size 54, header-centre 126 (the new `primary` and `hero` checks find nothing on the old build: it has no mint and no `.bg-hero`). After: edges 38, target-size 6, `primary-count` 0, `hero-*` 0. All 44 remaining findings are rows D1–D4 below.

Manual check at 900 px (Risks): student, teacher and admin, ar and en, both builds. After: student 900 ar 2 (D1), student 900 en 128 (126 header-centre = D4, 2 = D1), admin 1 (D3) each, teacher 0. Before: student 900 ar 10, 900 en 136, admin 1, teacher 0. No overflow finding at 900 in either build.

## 3. Findings

| # | route | width | dir | check | target | detail | fix (file:line) | status |
|---|---|---|---|---|---|---|---|---|
| F1 | `/admin/configuration` | all | both | primary-count | every setting's «حفظ» / "Save" and «إضافة فترة امتحانات» | 23 mint buttons (one Save per setting form) | per-setting Save → `variant="secondary"`: `web/src/features/configuration/components/NumberSettingForm.tsx:59`, `ChoiceSettingForm.tsx:58`, `BooleanSettingForm.tsx:53`, `ChoiceListSettingForm.tsx:50`. «إضافة فترة امتحانات» stays the page's one mint | fixed |
| F2 | `/admin/blueprints` | all | both | primary-count | blueprint «حفظ» / "Save" | 4 mint buttons (one per blueprint card) | `web/src/features/blueprints/components/BlueprintEditor.tsx:85` `SubmitButton variant="secondary"` (same action repeated per card, as subscribe per plan card, D4) | fixed |
| F3 | `/student/assistant`, `/student/assistant/:id` | 375 (whenever the toolbar and composer are both shown) | rtl | primary-count | «محادثة جديدة» and «إرسال» | 2 mint buttons | `web/src/features/avatar/components/AssistantToolbar.tsx:28` new chat → `variant="secondary"`; «إرسال» stays the one mint | fixed |
| F4 | every route with a button | all | both | probe (visual check) | `Button` label | computed 16 px instead of `text-label` 14 px: `tailwind-merge` did not know the new `text-label` size, read it as a colour and dropped it next to `text-text` / `text-accent-text` | `web/src/shared/lib/utils.ts:17` `'label'` added to the `text` theme list (`shadow: ['1']`, `2` removed with the token) | fixed (re-measured: 14 px / 700) |
| F5 | `/student/subject/:id`, `/student/unit/:id` | all | both | target-size | unit / lesson title links | before: 313×22 (#276 D2) | none needed: `type.ui` line height is now 24 px | fixed by the token change |
| F6 | every student route | 1280 | ltr | header-centre | top nav links | before: 7.5 px off the row centre (the English nav overflowed the 1040 container and showed its scrollbar) | none needed: `layout.max` 1200 gives the nav room at 1280 | fixed by the token change (still at 900 px, see D4) |
| F7 | `/admin/users` Teachers and Admins tabs | all | rtl | primary-count (r2) | «دعوة معلّم» / «دعوة مدير» and filter «تطبيق» | 2 mint buttons (missed in r1: only the Students tab was audited) | every filter form's Apply → `SubmitButton variant="secondary"`: `web/src/features/users/components/UserFiltersForm.tsx:55`, `audit/components/AuditLogFilters.tsx:43`, `payments/components/PaymentLogFilters.tsx:60`, `questions/components/QuestionListFilters.tsx:69`, `avatarConversations/components/AvatarConversationFilters.tsx:46`, `questions/components/ValidationQueueFilters.tsx:81` | fixed |
| F8 | `/admin/users?tab=teachers&q=…` (filtered empty) | all | rtl | primary-count (r2) | «دعوة معلّم» and empty-state «مسح الفلاتر» | 2 mint buttons | `web/src/features/users/components/UserListEmptyState.tsx:18` Clear → `variant="secondary"`; Invite stays the users page's one mint (the Students tab has none) | fixed |
| F9 | `/admin/audit`, `/admin/payments`, `/admin/questions`, `/admin/avatar-conversations`, `/teacher` (filtered empty) | all | rtl | primary-count (r2) | filter «تطبيق» and empty-state «مسح الفلاتر» | 2 mint buttons | F7's Apply change; «مسح الفلاتر» stays the one mint | fixed |
| F10 | `/admin/content` with subjects and units expanded | all | rtl | primary-count (static, r2) | «إضافة مادة», «إضافة وحدة» per subject, «إضافة درس» per unit, inline rename «حفظ» | one mint per NameForm | `web/src/features/content/components/NameForm.tsx` new `submitVariant` prop, default `secondary`; `content/pages/ContentPage.tsx` passes `primary` for «إضافة مادة» | fixed (re-measured: 85 buttons visible, 1 mint) |
| F11 | `/admin/question/import/:lessonId` after a clean check | all | both | primary-count (static, r2) | «افحص الملف» and «استيراد N سؤال» | 2 mint buttons | `web/src/features/questions/components/QuestionImportForm.tsx:78` Check → `variant="secondary"`; the confirm button is the one mint once a clean result shows | fixed (not driven in the browser: needs an .xlsx upload) |
| F12 | `/student` for a Free student | all | both | primary-count (static, r2) | plan line «اشترك» and next-lesson «درّب الآن» | 2 mint buttons | `web/src/features/subscription/components/PlanSummaryLine.tsx:25` → `variant="secondary"` | fixed (not driven: every seeded demo student is subscribed) |
| D1 | `/`, `/student/subscription`, `/student/exam/:id` | all | both | edges | hero / subscribe header / exam timer contents (h1, p, CTA) | starts 20 px (32 px at lg) from the content edge; 17 px in the exam card | — | intended, as #276 D1: the page root is a hero banner or the timer card; its contents sit inside the banner padding (`p-5`, `lg:p-8`, which is also the 20 px end-edge rule of D10). The banner itself starts on the content edge |
| D2 | `/` | 1280 | both | edges | `span ""` (hero halo) | starts 1008 px from the content edge | — | intended: the decorative halo (`aria-hidden`, `absolute -end-12 -bottom-12`, lg only) sits at the banner's bottom-end corner by design and is clipped by the banner (`overflow-hidden`) |
| D3 | `/admin/payments` | all | both | target-size | student-name filter button in the payment table | 78×21 (78×20 before; pre-existing, also in the before build) | — | intended, not introduced here: an inline text button inside a dense table cell (admin data views exception). The 24 px target circle centred on it touches no other target (the contact line under it is plain text), so it meets WCAG 2.5.8 through the spacing exception |
| D4 | every student route | 900 | ltr | header-centre | top nav links | 7.5 px off the row centre | — | pre-existing (before: the same 126 findings at 900 en, and also at 1280 en). The six English student labels are wider than the nav at 900 px; the `ul` keeps `overflow-x-auto` (#276), and headless Chrome's classic 15 px scrollbar lifts the items. Overlay scrollbars (mobile, macOS) do not. Out of scope for this story |

`hero` check: `.bg-hero` reported `linear-gradient(270deg, rgb(59, 30, 144) 0%, …)` in RTL and `linear-gradient(90deg, …)` in LTR on all three banners, inline-end padding 20 px (32 px from lg), so `hero-direction` and `hero-padding` found nothing. The nested `&:where([dir='rtl'], [dir='rtl'] *)` in `@utility bg-hero` compiles and applies.

## 4. Static checks (from `web/`)

```
$ grep -rnE '\bfont-(semibold|medium|light|black|thin)\b' src --include=*.ts --include=*.tsx
(empty)
$ grep -rnP '(?<![-\w])text-accent(?![-\w])' src --include=*.ts --include=*.tsx
src/shared/ui/layout.ts:8:  'inline-flex min-h-11 shrink-0 items-center rounded-sm font-display text-h2 font-bold text-accent …'
$ grep -rnE 'shadow-2|accent-hover|accent-pressed|aurora|Readex|Noto Sans|variant="accent"' src scripts
scripts/tokens/generateTokensCss.test.ts:44:    const css = generateTokensCss(fixture({ typography: '| type.h1 | Readex Pro | 26/31 · 30/36 | 700 | title |' }));
scripts/tokens/generateTokensCss.test.ts:55:        typography: '| type.body | Noto Sans Arabic | 16/27 (lesson text 16/29) | 400 (question stem 600) | x |',
scripts/tokens/generateTokensCss.test.ts:67:      fixture({ typography: '| type.display | Readex Pro | 36/38 · 44/46, tracking -0.02em | 700 | x |' }),
$ grep -rln 'heroClassName' src --include=*.tsx
src/features/landing/components/LandingHero.tsx
src/features/mastery/components/HeadlineCounterCard.tsx
src/features/subscription/components/SubscribeHeader.tsx
$ grep -n fontsource src/main.tsx
1:import '@fontsource/poppins/latin-400.css';
2:import '@fontsource/poppins/latin-700.css';
3:import '@fontsource/poppins/latin-800.css';
4:import '@fontsource/almarai/arabic-400.css';
5:import '@fontsource/almarai/arabic-700.css';
6:import '@fontsource/almarai/arabic-800.css';
```
The "old theme gone" grep is not empty only because of the three fixture strings in `generateTokensCss.test.ts`, which the plan keeps unchanged ("uses its own fixtures and is unchanged"); they test the generator's parser, not the theme. `src` is clean.

## 5. Visual check against `.process/design-sample-busuu.html` (`/student`, 1280, ar)

Measured with `getComputedStyle` / `getBoundingClientRect` on the production build; sample values from the sample's CSS.

| element | sample | app | intended? |
|---|---|---|---|
| page ground | `#f2f7fd` | `rgb(242, 247, 253)` = `#F2F7FD` | yes |
| app bar | white, sticky, `rgba(0,0,0,.1) 0 1px 2px 0`, 64 px | white, sticky, the same shadow, no bottom border, 56 px | yes: 56 px keeps every #276 sticky offset (D23) |
| logo | Signal Blue 18 px 700 | `#116EEE`, 18 px, 700 | yes |
| nav items | charcoal, active Signal Blue | inactive `#252B2F` 14 px 700; active `#0E64DA` on `#EAF2FE` pill (45 px radius) | yes: blue text uses accent.text for AA (D2, D25) |
| hero banner | full-bleed 90deg gradient, 64 px vertical padding, illustration | contained banner, 1152 × 169, `linear-gradient(270deg, #3B1E90, #5A3CC4 30%, #3A6EF0)` in RTL, radius 16, padding 32, no shadow, halo circle | yes: contained (D18), mirrored in RTL (D10), no art assets (Scope Out) |
| hero headline | 40 px / 1.14, 800, white | 40 px / 46 px, 800, white, Poppins/Almarai stack | yes |
| hero CTA | mint pill | none in the banner; the screen's one mint is «درّب الآن» on the next-lesson card | yes (D19) |
| primary button | `#11ee92`, charcoal, 700, 14–16 px, radius 45, padding 20 | `rgb(17, 238, 146)`, `rgb(37, 43, 47)`, 14 px 700, radius 45, padding-inline 20, height 44; 1 mint on the page | yes |
| cards | white, radius 16, padding 20, subtle shadow | white, radius 16, padding 20, `rgba(0,0,0,.1) 0 1px 2px 0`, hairline `#E6ECF2` | yes: hairline kept because surface vs canvas is 1.08:1 (D8) |
| circle icon | 60 px mist circle, Signal Blue | 56 px `#F2F7FD` circle, initial in `#0E64DA` 18 px 800 | yes (D24) |
| assistant button | — (not in the sample) | white pill, 2 px `#116EEE` outline, `#0E64DA` label, the one shadow | yes (D7) |
| content width | 1200 | `main` max-width 1200 | yes |

## 6. Rework r2: non-default tabs, filtered-empty states and dialogs

The r1 audit only visited each page's default state, so `primary-count` never saw the Users Teachers/Admins tabs or a filtered "no results" list (review finding 1). r2 re-ran the audit on the after build (same environment as §1: `vite build` + `vite preview` on :5289 proxied to the demo stack, headless Chrome over CDP, `layout-audit.js` pasted unchanged except for the dialog scope below), Arabic only, at 375 / 768 / 1280.

`primary()` change: when a modal dialog is open, it counts the mint buttons inside the top-most visible `[role=dialog]` / `[role=alertdialog]` only; a modal is its own screen state (the page behind it is under the overlay and inert). Without a dialog it counts the whole document, as before.

| role | state (route) | mint buttons after (every width) | findings (all checks) |
|---|---|---|---|
| admin | Users Teachers tab `/admin/users?tab=teachers` | «دعوة معلّم» | 0 |
| admin | Users Admins tab `?tab=admins` | «دعوة مدير» | 0 |
| admin | Users Students, filtered empty `?q=zzzqqq` | none | 0 |
| admin | Users Teachers, filtered empty `?tab=teachers&q=zzzqqq` | «دعوة معلّم» | 0 |
| admin | Users Admins, filtered empty `?tab=admins&q=zzzqqq` | «دعوة مدير» | 0 |
| admin | Invite dialog open over the Teachers tab | «إنشاء الدعوة» (in the dialog) | 0 |
| admin | Audit filtered empty `?actor=zzzqqq` | «مسح الفلاتر» | 0 |
| admin | Payments filtered empty `?reference=zzzqqq` | «مسح الفلاتر» | 0 |
| admin | Payments «بحاجة لمراجعة» tab `?view=review` | none | 0 |
| admin | Questions filtered empty `?minVersion=999` | «مسح الفلاتر» | 0 |
| admin | Avatar conversations filtered empty `?search=zzzqqq` | «مسح الفلاتر» | 0 |
| admin | Content with every subject and unit expanded (85 buttons) | «إضافة مادة» | 0 |
| admin | Configuration | «إضافة فترة امتحانات» | 0 |
| teacher | Validation queue filtered empty `/teacher?difficulty=Hard` | «مسح الفلاتر» | 0 |
| teacher | Inbox «Mine» / «Unclaimed» tabs | none | 0 |
| teacher | Grade review | none | 0 |
| student | Progress filtered by kind (`?kind=Exam`, `?kind=Quiz`; filtered empty for demo-student-003) | «عرض الكل» when empty, else none | 0 |
| student | Home (demo-student-003, subscribed) | «درّب الآن» | 0 |
| student | Subscription | none | 1 (D1, as before) |

`primary-count` 0 everywhere. Not driven in the browser, checked by reading the code instead: the question import after a clean check (F11, needs an .xlsx upload), the Free-student home (F12, no Free account in the seed), and the validation queue with bulk approve (shown only when items exist, so never together with the filtered-empty «مسح الفلاتر»; the filter Apply is now outline, so bulk approve is the one mint).

Static check for the rest of `src`: every `Button`/`SubmitButton` that renders mint (explicit `variant="primary"`, or no variant) was listed and each one's screen read. Mutually exclusive branches (quiz check/next, exam start/continue, exam-result retake, onboarding form/skip, ask-teacher claim/reply, assistant not-found/chat) never show two. Dialog submits sit in their own modal. The remaining screens have one each.

Regression tests: `mintButtons()` (`web/src/test/mintButtons.ts`) counts `[data-slot=button].bg-action`. The filtered-empty test of Audit, Payments, Questions, Avatar conversations and the validation queue asserts exactly one mint. The Users filtered-empty test asserts none on Students, and a new Users test asserts the Teachers filtered-empty tab's only mint is «Invite teacher». Mutation-checked: putting the Users and Audit Apply back to primary fails all three affected tests.
