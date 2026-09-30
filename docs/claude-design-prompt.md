# Elmanhg — High-fidelity, fully functional build for Claude Design

You are building the production-quality front end of **Elmanhg (المنهج)**, an Arabic-first exam-prep platform for Egyptian Thanaweya Amma students. This package contains a working wireframe prototype and a finished design system. Your job is to rebuild the wireframe as a polished, fully interactive product using the design system exactly. Do not invent new flows, screens or business rules. Do not change the wording of any Arabic copy unless it is obviously a typo.

## 0. What is in this package

| Path | What it is | How to use it |
|---|---|---|
| `prototype/index.html`, `prototype/app.js`, `prototype/data.js`, `prototype/styles.css` | The working wireframe. Vanilla JS, hash router, localStorage state, seed data, real graders, all three roles. | This is the **functional spec**. Every route, screen, button, rule and state transition in it must exist in your build and behave the same. Read `app.js` fully before designing. |
| `prototype/README.md` | Click-through walkthrough (source: `docs/prototype.md` in the repo) | Use it as your acceptance test script. |
| `docs/design-system.md` | The "Glass" design system: colours, type, spacing, radius, shadows, components, RTL, motion. | This is the **visual spec**. Apply it verbatim. Section 2 to 5 are copied below for convenience; the file is canonical. |
| `docs/PRD.md` | Product requirements | Reference only, for understanding intent. The prototype already implements the v1 scope. |

Open the prototype first: serve the `prototype/` folder with any static server and open `index.html`. Switch role and user from the top bar. Reset data with the button.

## 1. Deliverable

A single self-contained web app that runs from a static folder with no build step and no backend:

- `index.html`, one CSS file, one or more JS files, seed data. No frameworks that require a bundler. Plain HTML/CSS/JS, or React via a single UMD script tag if you prefer, but the output must open by double-clicking `index.html` or serving the folder.
- Fonts from Google Fonts via `<link>`: **Readex Pro** (500, 600, 700) and **Noto Sans Arabic** (400, 500, 600).
- All state in `localStorage` under one key, with a working "إعادة ضبط البيانات" (reset) button, exactly like the prototype.
- Hash router. Every route listed in section 4 exists. Browser back and forward work.
- Every button, link, tab, option, input and form in the prototype works in your build. Nothing is decorative. If the prototype does something on click, yours does the same thing.
- `lang="ar"` and `dir="rtl"` on `<html>`. Logical CSS properties only. No left/right margins or paddings.
- Responsive: designed first at **375 px** width, verified at 390, 768 and 1280. No horizontal page scroll at any width. Tables scroll inside their card on mobile.
- No console errors. No emoji in UI. No dark mode.

## 2. The design system (canonical file: `docs/design-system.md`)

### 2.1 Colours

Define these as CSS variables on `:root` and use nothing else.

```css
--bg: #F5F5F7;            /* page ground. Never pure white for the page */
--surface: #FFFFFF;       /* cards, sheets, inputs, app bar */
--soft: #EDEDF0;          /* selected option fill, muted chips, progress track */
--text: #1D1D1F;          /* primary text, primary buttons */
--text-2: #6E6E73;        /* secondary text, labels, captions */
--border: rgba(0,0,0,.06);        /* hairline on cards */
--border-strong: rgba(0,0,0,.14); /* inputs, option outlines */
--accent: #0071E3;  --accent-soft: #E8F1FC;   /* links, focus ring, progress fill, Ask a Teacher, active tab */
--ok: #34A853;      --ok-soft: #E9F6EC;       /* correct, mastered, approved */
--bad: #E5484D;     --bad-soft: #FDECEC;      /* wrong, rejected, overdue, destructive */
--warn: #B7791F;    --warn-soft: #FFF6E5;     /* partial credit, pending review */
--v2: #6A3FB5;                                /* "v2" badge only */
--aurora: linear-gradient(135deg, #7C5CFF 0%, #C86DD7 50%, #FFB07A 100%); /* landing hero and subscribe header ONLY */
```

Colour rules:
- Blue = action or link. Green = correct. Red = wrong. Nothing else is coloured.
- Green and red are used as **text on their soft background** for body sizes. As **fills with white text** only for badges 12 px bold or larger.
- The gradient appears in exactly two places: the landing page hero and the subscribe screen header. Never on lesson, quiz, exam, teacher or admin screens.
- Lesson content areas are pure white with no tint.

### 2.2 Typography

| Role | Font | Size mobile / desktop | Line height |
|---|---|---|---|
| Display (headline counter, landing hero) | Readex Pro 700, letter-spacing -0.02em | 36 / 44 px | 1.05 |
| H1 page title | Readex Pro 700 | 26 / 30 px | 1.2 |
| H2 section or card title | Readex Pro 700 | 20 / 22 px | 1.3 |
| H3 | Readex Pro 600 | 16 px | 1.4 |
| Body (lesson text, question stem) | Noto Sans Arabic 400 (stem 600) | 16 px | 1.7 (lesson explanation 1.8) |
| UI (buttons, inputs, options) | Noto Sans Arabic 500/600 | 15 px | 1.5 |
| Caption | Noto Sans Arabic 400 | 13 px | 1.5 |
| Micro (badges, tab labels) | Noto Sans Arabic 600 | 12 px | 1.4 |

Arabic body text never below 15 px. Arabic-Indic digits (٠١٢٣) in student-facing UI; ASCII digits in admin tables and exports; never mixed in one string. LaTeX, code, URLs and phone numbers are wrapped in `direction: ltr; unicode-bidi: isolate`.

### 2.3 Spacing, radius, elevation

- Base unit 4 px. Scale: 4, 8, 12, 16, 20, 24, 32, 40, 48.
- Page gutter 16 px mobile, 24 px desktop. Content max width 1040 px, centred.
- Card padding 16 px mobile, 20 px desktop. Gap between stacked cards 12 px. Gap between cards in a grid 12 px.
- Section spacing: 24 px between an H2 and the previous block, 12 px between H2 and its content.
- Radius: `--r-sm` 10 px inputs and chips; `--r-md` 14 px options and list items; `--r-lg` 18 px cards, sheets, dialogs; `--r-pill` 999 px buttons and badges.
- Shadows: `--shadow-1: 0 1px 2px rgba(0,0,0,.04), 0 8px 24px rgba(0,0,0,.06)` on cards; `--shadow-2: 0 4px 12px rgba(0,0,0,.08), 0 24px 48px rgba(0,0,0,.10)` on sheets, dialogs and the floating assistant button.
- Cards: white, hairline border, shadow-1. No coloured left or right borders ever.

### 2.4 Components and their states

**Buttons.** Pill shape, 44 px min height (36 px for the `sm` size in dense admin tables), 15 px weight 600, horizontal padding 18 px (12 px for sm). Disabled 45 percent opacity. Focus ring 2 px `--accent` offset 2 px. Hover: background shifts to `--soft` for secondary, to `#000` for primary. Transition 150 ms.

| Variant | Fill | Text | Border | Where |
|---|---|---|---|---|
| Primary | `--text` | white | none | One per screen: start quiz, check answer, submit exam, save |
| Accent | `--accent` | white | none | Subscribe, Ask a Teacher, pay |
| Secondary | `--surface` | `--text` | `--border-strong` + 1px shadow | Everything else |
| Danger | `--surface` | `--bad` | `--bad` | Reject, delete, cancel subscription, fail payment |
| Ghost | none | `--accent` | none | Inline actions in tables |

**Quiz option.** Full-width `<label>` containing the input, 48 px min height, `--r-md`, 15 px, 12 px vertical and 14 px horizontal padding, 10 px gap between control and text, 8 px gap between options. States: default white with `--border-strong`; hover `--soft`; selected `--soft` fill with `--text` border; after check, correct `--ok-soft` with `--ok` border and wrong `--bad-soft` with `--bad` border. Radio and checkbox controls 18 px with `accent-color: var(--text)`.

**Feedback panel** (under the question after checking): `--r-md`, 12 px by 14 px padding, soft background and strong border in the verdict colour, a 26 px filled circle icon (check or x) at the start, bold verdict, explanation in `--text-2`.

**Badges.** Pill, 12 px 600, 2 px by 10 px padding. `ok` green fill white text; `bad` red fill; `pending` `--soft` fill `--text-2` text; `role` `--text` fill white text; `v2` transparent with `--v2` outline and text.

**Progress.** Track `--soft`, fill `--accent` for mastery and `--ok` for a passed exam. 6 px tall, fully rounded. The headline counter is a white card, number in Display size, meta line in caption.

**Navigation.** Mobile: bottom tab bar, white, hairline top border, at most 4 items, Lucide stroke icons 22 px at 1.8 px stroke, label 12 px (micro), active `--text` 600, inactive `--text-2`. A role with more than 4 destinations shows its 3 primary destinations plus a fourth item "المزيد" that opens a list of the rest. Desktop (≥ 900 px): top bar with every destination as text tabs, active has a 2 px `--accent` underline. Sub-tabs are pills: 7 px by 14 px, active white with `--border-strong`.

**Inputs.** White, `--r-sm`, `--border-strong`, 44 px height, 9 px by 12 px padding, 15 px. Labels 13 px `--text-2` above the field with 6 px gap. Selects in filter rows may be 36 px.

**Tables.** Inside a card, no vertical rules, hairline row separators, header 12.5 px 600 `--text-2`, cells 13.5 px with 9 px by 10 px padding, right aligned. Sticky header on desktop. Horizontal scroll inside the card on mobile.

**Cards in a grid.** Subject cards, plan cards, dashboard KPI cards: grid with `minmax(280px, 1fr)` on 700 px and up, single column below.

**Assistant panel** (AI Avatar). Floating pill button bottom-start, `--text` fill, white text, shadow-2, 48 px. Opens a slide-in sheet from the start edge, max 380 px, full height, white, shadow-2, rounded outer corners `--r-lg`. Student bubbles `--soft` with no border; assistant bubbles white with hairline border; a 14 px sparkle icon in `--accent` marks the assistant. 220 ms slide.

**Dialogs.** Centred, max 420 px, `--r-lg`, 20 px padding, shadow-2, overlay `rgba(29,29,31,.35)`.

**Sticky exam header.** White card, `--r-md`, shadow-1, sticks under the app bar, holds the countdown in Readex Pro 700 18 px, turning `--bad` in the last two minutes.

### 2.5 Motion and icons

- 150 ms for hover and focus, 220 ms for panels and dialogs, 400 ms for progress bar fill. Easing `cubic-bezier(.2,.8,.2,1)`. Honour `prefers-reduced-motion`.
- Correct answer: feedback panel fades in, option border transitions. No confetti, no bounces.
- Icons: Lucide, stroke 1.8, round caps. 22 px in navigation, 16 px inline. Inline SVG. No emoji anywhere.

## 3. Roles and users (from `data.js`)

Keep the top bar role switcher and user switcher exactly as in the prototype: role select (طالب / معلّم / مدير), user select per role, reset button. Users: students أحمد (Base + Ask a Teacher) and سارة (Free); teachers أ. محمد (physics only) and أ. هدى (math only); admin المدير.

## 4. Screens and routes to rebuild

Every route below exists in `prototype/app.js`. Match its behaviour one to one. Improve only the visual execution.

**Student**
- `#/student` Home: greeting, headline counter card ("متبقّي لك X سؤال من Y", seen count, streak), plan line (plan name; for Free the daily counter «أسئلة التدريب اليوم: X / 10» and «اشترك»), next recommended lesson (for Free, only open lessons are suggested) with "درّب الآن", subject cards with mastery percent and progress bar; chosen subjects under «موادك», others under «مواد أخرى», link «تعديل موادي».
- `/onboarding` after sign-up: «اختر موادك», subject checkboxes, «متابعة» (needs one), «تخطّي الآن»; reopened from Home «تعديل موادي» with «العودة إلى الرئيسية»; loading, empty, error-with-retry.
- `#/student/subject/:id` breadcrumb (الرئيسية › subject), subject mastery line and bar, units in order with mastery bar, published-lesson count, best unit-exam score («—» when none) and «امتحان الوحدة»; «امتحان متعدد الوحدات» opens the builder for this subject. `#/student/unit/:id` breadcrumb (الرئيسية › subject › unit), unit mastery, published lessons in order with mastery and question count (locked lessons for Free users show «مقفل - للمشتركين» and «اشترك لفتح الدرس»; a locked lesson page shows only the subscribers-only notice with «اشترك», no tabs), and a unit-exam card with the best score and a link to the exam start. Loading, empty, error-with-retry and RTL states on both.
- `#/student/lesson/:id` breadcrumb (الرئيسية › subject › unit › lesson), the lesson title with «إتقانك X٪ · أسئلة متاحة: N · شاهدت: M» and a mastery bar, pill tabs الشرح / الأهداف / الملخص / التدريب (each a route; التدريب is `/practice`: pick 5 / 10 / 20 and start), empty messages per tab, and previous/next lesson links that continue into the next unit («العودة إلى {unit}» at the end). Opening the page records the lesson as opened (used by the optional unit-exam gate). "اسأل المساعد عن الدرس" opens the assistant with the lesson; "اسأل معلّم" opens the new-question page with the lesson attached.
- `#/student/quiz/:sessionId` One question at a time, question counter, type and difficulty chips, option list, "تحقّق", immediate feedback with explanation and "اسأل المساعد" and "اسأل معلّم", "التالي", "إنهاء التدريب"; an essay is written in a plain-text RTL editor with a live word count against the limit, characters left near 20 000 and a draft autosaved on the device («استعدنا مسودتك المحفوظة.», «حُفظت المسودة على هذا الجهاز»); «أرسل الإجابة» submits it, and the essay is then shown read-only with «جارٍ تصحيح إجابتك…» until the AI grade arrives, «قيد المراجعة» while a teacher reviews it, then the verdict, score, criterion marks and justification. `#/student/quiz-result/:id` score, time, per-question review (a written essay with its grade status, and the note «بعض الإجابات المقالية ما زالت قيد التصحيح، وستتحدّث الدرجة عند اكتمالها.» while one is pending).
- `#/student/exam-start/:unitId` blueprint summary, time limit, best score, attempts list. `#/student/exam/:sessionId` sticky timer, all questions (an essay gets the same plain-text editor with its word count, auto-saved to the server), auto-save, no feedback until submit, resume on refresh. `#/student/exam-result/:id` score, pass or fail, per-lesson breakdown, weakest objectives, a written essay with its grade status and the provisional-score note while it is graded, attempts list with the best score highlighted, retake.
- `#/student/multi-exam` unit multi-select within a subject, size 20 / 40 / 60 (PRD §7.5), live merged preview with shortfall, start.
- `#/student/progress` per subject mastery with a unit table (mastery, best unit-exam score), weak lessons and weak objectives with "درّب الآن", session history filterable by all / quizzes / exams, paged.
- `#/student/ask` threads list with the monthly allowance «الرصيد الشهري: X / N» and an upsell without the add-on; `#/student/ask-new` (lesson picker) and `#/student/ask-new?lessonId|questionId|attemptId` compose with auto-attached context and an optional photo; `#/student/thread/:id` thread view; an answered thread shows the cards «سؤال متابعة (مرة واحدة فقط)» with «إرسال المتابعة», and «قيّم الإجابة وأغلق السؤال» (1 to 5 stars; «قيّم الإجابة» after the teacher's final reply closes the thread); a saved rating shows «التقييم:» with stars; a live «وصل رد من المعلّم على سؤالك.» toast appears when a teacher replies; a thread with an unread teacher reply shows «رد جديد» in the list until the student opens it. The photo is private: the page loads it with the signed-in session, never as a public link. A teacher's voice reply shows as an audio player with its length, then «نص التفريغ الصوتي:» and the text; the audio is private like the photo.
- `#/student/subscription` Aurora gradient header, current plan, plan cards, payment log, subscribe buttons per Base period and for Ask a Teacher (disabled without Base); checkout redirects to Paymob, or with the fake gateway to `#/student/fake-checkout/:paymentId` (simulated Paymob page with success, failure and cancel); `#/student/checkout-result/:paymentId` shows confirming, success, failure or still-confirming; Renew buttons per period from 7 days before the period ends; each active plan line has a Danger 'إلغاء' opening a confirm dialog (access continues to the period end); an Active plan past its end shows 'انتهت المدة، متاحة حتى …'.
- Assistant panel on every student screen. A floating «المساعد» button opens it in the general context; «اسأل المساعد عن الدرس» on the lesson page, «اسأل المساعد» after answering a quiz question and on quiz-result and exam-result review items open it with that lesson or question; «اسأل المساعد» on the exam screen opens it too. The panel has the header «المساعد الذكي» with a close button, «السياق: {title}» («عام» for the general context), a Free-only counter «رسائل اليوم: X / N», the conversation (a greeting for the entry point, soft student bubbles, white assistant bubbles, and under a reply the «المصادر» chips, each linking to the lesson tab it came from), a «المساعد يكتب…» line while a reply is pending, and a composer with «سؤالك» and «إرسال». States: while an exam is in progress it shows the refusal and disables the composer; at the daily limit it shows the limit notice, with «اشترك» for Free students; if the assistant is unavailable a notice appears in the conversation; if the panel cannot load it shows an error with «إعادة المحاولة». The general context has no lesson picker: the greeting asks the student to open the lesson they need.
- Free plan: 10 quiz questions per day, with the counter on Home, the practice tab («الباقة المجانية: X / 10 سؤال اليوم») and the quiz screen («اليوم: X / 10»); the paywall dialog («اشترك» opens `#/student/subscription`, «لاحقًا» closes) on quiz start, answer, new practice, unit exam start and multi-unit exam start; 5 assistant messages per day; first lesson per unit only; exams need Base.

**Teacher**
- `#/teacher` validation queue scoped to assigned subjects, oldest first, filters (unit, lesson, type, difficulty, age: waiting at least 1 / 3 / 7 days), stats strip. Items opened in this review session carry an "opened" badge and a checkbox; the teacher can select them and bulk-approve after a confirm dialog (unopened items cannot be selected). `#/teacher/q/:id` records the opening for the session and shows the question as the student sees it with the answer key ticked, the explanation, the grading spec (JSON), the version, and the revision and decision (rejection) history; an essay also shows its rubric (criteria with points and level descriptions, total points) and its model answers; Approve, Reject with required reason, change difficulty then approve. No edit of content.
- `#/teacher/inbox` «أسئلة الطلاب»: a «تذكيرات» card above the tabs lists the questions with a 12 h or 20 h reminder («التذكير الأول / الثاني», who claimed it, the SLA badge; a warning icon, no red), and a live «تذكير: سؤال طالب بانتظار ردك.» toast appears when a reminder fires; then Ask a Teacher threads for assigned subjects, awaiting ones first by reply due time, with pill tabs الكل / غير مُستلمة / الخاصة بي; each item shows the question, subject / lesson, the student's display name, the date, who claimed it (غير مُستلم / مستلم بواسطتك / المعلّم: …) and the SLA badge; loading, empty, error-with-retry, paged. `#/teacher/thread/:id` the context card with «الطالب · المعلّم», the messages, «استلام السؤال» (a lost race shows «استلم معلم آخر هذا السؤال.»), then the reply card «الرد» with a «نص / صوت» choice: text shows the text form; voice shows «تسجيل» and the maximum length, «إيقاف» with a timer while recording (no red), a local preview with «إعادة التسجيل», «جارٍ تفريغ التسجيل…», then the editable «نص التفريغ الصوتي (يمكنك تصحيحه قبل الإرسال)» and «إرسال الرد»; a failed transcription asks the teacher to type the text; a thread claimed by another teacher shows «هذه المحادثة مستلمة بواسطة معلّم آخر.»; the student's rating shows as «التقييم:» with stars
- `#/teacher/stats` personal stats card.
- A physics teacher must never see math content, even by direct URL.

**Admin**
- `#/admin` dashboard: KPI cards (students, DAU, MAU, subscribers by plan, MRR, content counts by status and type, servable total, solve rate, success rate, validation backlog and median decision time, Ask a Teacher open and breached, payments and revenue) and the charts, filterable by subject and 7 / 14 / 30 days.
- `#/admin/content` subject, unit, lesson tree (each lesson shows its question count) with reorder, publish, move to draft, archive and delete, each confirmed inline and offered only when the lesson's state allows it; `#/admin/lesson/:id` editor: name, rich-text explanation and summary (image upload, inline and block LaTeX), ordered objectives list, optional video URL, and a live student-view preview, links to a new question, to the lesson's questions and to the question import, plus the same publish, move to draft, archive and delete actions next to the state badge.
- `#/admin/questions` list with status, type, subject, teacher, minimum-version and rejection-reason filters (and a lesson filter reached from the lesson editor, whose bar links to a new question and to the question import for that lesson); each row shows the question, lesson, type, status, version, teacher and rejection reason, with "تعديل" or, for a rejected question, "تعديل وإعادة إرسال". `#/admin/question/:id` and `#/admin/question/new/:lessonId` editor per type with live student preview and "جرّب الإجابة" running the real grader and showing the verdict, the score and the grader's feedback line when there is one; fill-in and text short answers show an «تطبيع الإجابة» group of eight rule checkboxes (all on by default); essay (v2) has an optional word limit, a rubric editor (criteria with title, optional description, points and at least two levels from 0 to full points; add/remove criteria and levels; a total-points line) and one to three model answers, and its preview shows the essay box with a word count and «جرّب الإجابة» sends the essay to the AI grader, showing the verdict, the score, each criterion's points with its reason, the Arabic justification and the grader's confidence; a rejected question shows its reason and saves with "حفظ وإعادة الإرسال للمراجعة"; editing content of an approved question returns it to pending and bumps the version. `#/admin/question/import/:lessonId` bulk import from an Excel file: a breadcrumb (Questions › lesson › Import questions) with the title "استيراد أسئلة" and an intro line, then (1) a template card explaining one sheet per question type plus an Instructions sheet, with "تنزيل القالب"; (2) a check card with a labelled `.xlsx` file input, the hint that nothing is saved until you confirm, and "افحص الملف", with file-level errors inline under the input; (3) the check result: rows found, ready and problems, and ready counts per type; with problems, a table of sheet, row, column and problem plus a warning notice to fix and re-check and no import button; when clean, a pending-review note and "استيراد N سؤال"; (4) after import, a success panel (`role=status`, success tint) "تم استيراد N سؤال قيد المراجعة" with a link to the lesson's questions and "استيراد ملف آخر". Choosing another file clears the result; loading uses a skeleton; a lesson that fails to load shows the error state with retry.
- `#/admin/blueprints` exam blueprints per subject (a subject select, kept in the URL): the subject's default blueprint card, then one card per unit. A blueprint card has a table of question type / required count / available servable questions, with short rows highlighted and a total row; the time limit in minutes (optional); the pass mark; an optional difficulty mix (easy/medium/hard %, summing to 100); a live «عجز حالي» notice listing each short type as «مطلوب، متاح، ينقص»; and «حفظ», which is refused while any type is short. A unit without its own blueprint shows «تستخدم النموذج الافتراضي» (or that no exam is available when the subject has no default), a warning when the default is short for that unit, and «إنشاء نموذج للوحدة», which opens the editor prefilled from the default. A unit blueprint offers «استخدام النموذج الافتراضي», confirmed inline, to remove it. Loading skeleton, error with retry, and an empty state linking to content when there are no subjects.
- `#/admin/users` students (progress, suspend, grant plan), teachers (assign subjects), admins. `#/admin/student/:id` progress view.
- `#/admin/payments` payment log (nav «المدفوعات»; not simulated by the prototype): H1 «المدفوعات»; pill sub-tabs «كل المعاملات» and «بحاجة لمراجعة» with a count badge of open reviews; a filter form (status, plan, reference = payment id or Paymob transaction id, from and to dates; «تطبيق» and «مسح الفلاتر»), all kept in the URL; a table with date, student (name as a button that filters the log to that student, with the email or phone under it; the form then notes «تعرض مدفوعات طالب واحد.»), plan and period, amount, status badge (ناجحة / فاشلة / قيد الانتظار / مستردة), a «بحاجة لمراجعة» badge and the review reason for an open review, the refund date and reason for a refunded payment, the Paymob reference, and actions. «استرداد» (Danger, only for a refundable successful payment) opens a dialog «استرداد {المبلغ}؟» that says the amount goes back to the student through Paymob and the paid time is removed at once, with a required «سبب الاسترداد» text area, server errors inline (a Paymob decline shows as an alert inside the dialog), «تراجع» and «تأكيد الاسترداد»; success closes it with a toast «تم استرداد الدفع.» and the row reads «مستردة». «إبقاء الدفع» (only on an open review) opens a confirm dialog «إبقاء هذا الدفع؟» and closes the review with a toast. States: loading skeleton; error with retry; three empty states («لا توجد مدفوعات بعد.», «لا توجد مدفوعات بحاجة لمراجعة.», and «لا توجد مدفوعات تطابق الفلاتر.» with «مسح الفلاتر»); pagination.
- `#/admin/avatar-conversations` assistant conversations (nav «محادثات المساعد»; not simulated by the prototype): H1 «محادثات المساعد» and an intro line (the students' conversations with the AI assistant, most recent first, each reply with its model, prompt version, tokens, cost and the context sent); a filter form («بحث» with the hint «نص في المحادثة أو اسم الطالب», «نقطة الدخول» with «الكل» / «الدرس» / «سؤال تدريب» / «مراجعة امتحان» / «عام», «من», «إلى»; «تطبيق» and «مسح الفلاتر»), all kept in the URL; a table with «آخر رسالة», «الطالب» (display name only), «السياق» (the entry point, then «المادة › الدرس» under it), «أول سؤال» (clamped to two lines), «الرسائل» and «عرض». States: loading skeleton; error with retry; «لا توجد محادثات بعد.»; «لا توجد محادثات تطابق الفلاتر.» with «مسح الفلاتر»; pagination.
- `#/admin/avatar-conversation/:id` one conversation: a back link «محادثات المساعد», H1 «محادثة المساعد», a white summary card («الطالب», «السياق» with the entry point and «المادة › الوحدة › الدرس», «بدأت», «آخر رسالة», «الرسائل», «الرموز: دخل X · خرج Y», and «التكلفة» in US dollars or «غير محسوبة»), then the messages in order: student messages in soft bubbles labelled «الطالب» with the time; replies in white bubbles with a hairline border and an accent sparkle, labelled «المساعد» with the time, then a meta line («النموذج», «نسخة التعليمات», «الرموز» as «دخل / خرج», «التكلفة», «سبب التوقف», «رسائل سابقة مرسلة», technical values in monospace left to right), the «المصادر» chips (not links), and a collapsible «السياق المرسل» with the context fields (المادة، الوحدة، الدرس، الأهداف، الشرح، الملخص، السؤال، إجابة الطالب، الإجابة الصحيحة، شرح السؤال، المواد) and «نتائج البحث المرسلة» (each result's title and text, or «لا شيء»). States: loading skeleton; error with retry (an unknown id reads «المحادثة غير موجودة.»).
- `#/admin/audit` audit log. `#/admin/export` three JSONL downloads.

**Landing** (new, the only screen not in the prototype)
- `#/` a marketing landing page: Aurora gradient hero with the live servable count ("100,000 سؤال" style headline using the real number from data), three value props (تدريب لا نهائي، امتحانات وحدات، اسأل معلّم خلال 24 ساعة), plan cards, and a "ابدأ مجانًا" button that goes to sign-up (the prototype jumps to `#/student` as سارة). A top-bar «تسجيل الدخول» opens sign-in; the plan cards come from the live catalogue; a signed-in visitor is sent to their home. Light, generous whitespace, Readex Pro display. This is the one place the gradient is allowed besides the subscribe header.

## 5. Business rules that must survive the redesign

These are implemented in `prototype/app.js` (`window.ElmanhgTest` exposes the grader, servable check, quiz selector and blueprint merge for you to reuse or verify against). Keep them identical:

1. Servable question = approved AND lesson published AND not retired. Derived, never stored.
2. Content edit on an approved question → pending, version + 1, revision kept.
3. Only a teacher assigned to the subject can approve or reject. Reject needs a reason. Admin has no approve button.
4. Quiz selection order: unseen → last wrong → correct once → rest by least recent. Random within bucket. No repeats in a session.
5. Mastery = normalised score ≥ 0.8 on the two most recent attempts. Every attempt is stored.
6. Headline counter = platform servable total − student's mastered count.
7. Exams come from blueprints; shortfall shows a clear message; multi-unit merges blueprints proportionally.
8. Exam retakes unlimited; best score shown; all attempts listed.
9. Assistant never reveals answers during an in-progress exam.
10. Ask a Teacher: 24 h SLA with red "متأخر" badge on breach, one follow-up, voice always has a transcript, rating on close.
11. Plan changes only via the simulated Paymob webhook modal or an admin refund.
12. Every content change, validation decision, publish, grant and export is written to the audit log.
13. Arabic answer normalisation: strip tashkeel and tatweel, unify أإآ→ا, ة→ه, ى→ي, Arabic-Indic digits → ASCII, trim and collapse whitespace; each rule can be switched off per question.

## 6. Layout guidance per screen type

- **List screens** (units, lessons, threads, queue): page title H1, optional caption, then a stack of white list items `--r-md` with 12 px by 14 px padding and 8 px gap. Each item: title 15 px 600, meta caption on the second line, trailing progress or badge aligned to the end.
- **Detail and editor screens**: cards stacked with 12 px gap. Two-column grid on 900 px and up for the question editor (editor 1.2fr, preview 1fr).
- **Quiz screen**: one white card, 18 px padding, stem 17 px 600 with 1.6 line height, options below with 8 px gap, primary "تحقّق" full width 48 px, then the feedback panel, then a row with "التالي" primary and "اسأل المساعد" secondary.
- **Exam screen**: sticky timer card, then one card per question numbered, 12 px gap, a final "تسليم" primary button in a sticky bottom bar on mobile.
- **Dashboard**: KPI cards in the responsive grid, each with caption label, Readex Pro 26 px value, and a caption sub-line; charts in white cards with 16 px padding.
- **Forms**: labels above fields, 12 px gap between fields, actions right-aligned (start in RTL) in a row with 8 px gap, primary last in reading order.

## 7. Acceptance checklist (run all of it before you finish)

1. Open as أحمد. Home shows the counter. Open الفيزياء → a lesson → التدريب → 5 → answer all → result. Feedback colours and option states match section 2.4.
2. Start a unit exam, answer two questions, reload the page, the exam resumes with answers intact and the timer still running. Submit. Result shows breakdown. Retake works.
3. Build a multi-unit exam with two units, size 20.
4. Switch to سارة. Answer 10 quiz questions, the 11th shows the paywall. «اشترك» in the paywall opens the subscription screen; subscribe, then on the fake Paymob page press "نجاح الدفع"; the plan activates and the quiz continues.
5. Open the assistant during an exam. It refuses. Open it after submitting. It answers.
6. Ask a Teacher as أحمد, switch to أ. محمد, claim, reply by voice, switch back, follow up, rate.
7. As أ. محمد, approve one question, reject another without a reason (blocked), then with a reason. Try to open a math question by URL. Refused.
8. As المدير, edit the stem of an approved question. It becomes pending v2. Change only its difficulty on another. It stays approved. Publish the draft math lesson. The servable total on the dashboard rises.
9. Save a blueprint that needs more essays than exist. Blocked with the shortfall shown.
10. Download the three JSONL exports.
11. Resize to 375 px. No horizontal scroll on any screen. Tables scroll inside their cards.
12. Console has zero errors across all of the above.

## 8. What not to do

- Do not add features, screens or copy beyond the prototype and the landing page.
- Do not use any colour outside section 2.1. Do not add a dark theme.
- Do not use Inter, Roboto, Arial, Cairo or Tajawal. Only the two fonts named.
- Do not use gradients, glass blur or decorative illustrations anywhere except the landing hero and subscribe header.
- Do not leave any control non-functional. If you cannot implement something, keep the prototype's behaviour for it and say so in your summary.

When you finish, list every route you built, every checklist item you ran with its result, and anything you deviated from with the reason.
