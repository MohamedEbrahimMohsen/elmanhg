# Elmanhg (المنهج) — clickable wireframe prototype

A throwaway, wireframe-grade prototype of the platform in `docs/PRD.md`, for the product owner to click through. It is **not** the production system.

## How to open

Double-click `index.html` (or open `file:///D:/Personal/elmanhg/prototype/index.html` in Chrome, Edge or Firefox). There is no build step, no server, no internet needed.

Files:
- `index.html` — shell (Arabic, RTL)
- `styles.css` — grey/white wireframe styling, mobile-first
- `data.js` — seed data (`window.SEED`)
- `app.js` — all logic and rendering (vanilla JS)

## Switching persona (no login)

The top bar has two dropdowns:
- **الدور** (role): طالب / معلّم / مدير
- **المستخدم** (current user for that role):
  - Students: **أحمد** (Base + Ask a Teacher, has practice history) and **سارة** (Free)
  - Teachers: **أ. محمد** (Physics only) and **أ. هدى** (Math only)
  - Admin: **المدير**

## Resetting data

All state is saved in `localStorage` under the key `elmanhg.v1`. Click **إعادة ضبط البيانات** in the top bar to restore the seed. (Clearing site data in the browser also works.)

## Suggested walkthrough

1. **Student home (أحمد)**: headline "متبقّي لك X سؤال من Y", "شاهدت N", streak, next suggested lesson, subject cards with mastery %. The product also opens on a landing page and asks a new student to choose subjects of interest after sign-up; the prototype does not simulate them.
2. **الفيزياء → الكهربية التيارية → التيار الكهربي وقانون أوم**: tabs الشرح / الأهداف / الملخص / التدريب. In التدريب pick 5/10/20. The product also has breadcrumbs on every level, previous/next lesson links across units and the mastery line under the lesson title; the prototype does not simulate them.
3. **Quiz**: answer, press تحقّق for instant feedback (correct answer + explanation), then **اسأل المساعد** to open the avatar panel with that question's context. (Product: the reply lists its sources, which link to the lesson tab. The general assistant does not list lessons; it asks the student to open one. The product also has a past-chats view «محادثاتي السابقة» in the panel, where the student reopens or deletes a chat; the prototype does not simulate it.)
4. **Unit exam** (from the unit page): see the blueprint summary, start, note the countdown and "محفوظ تلقائيًا". Reload the page: the exam resumes. Open the avatar during the exam and it refuses to reveal answers. Submit to see score, per-lesson breakdown, weakest objectives and all attempts (best score is highlighted).
5. **Unit "التكامل" exam**: shows a shortfall message, because lesson "التكامل المحدد" is still a draft.
6. **امتحان متعدد الوحدات**: choose 2+ units and a size (the prototype offers 10/20; the product uses 20/40/60, PRD §7.5). The blueprints are merged proportionally, and any shortfall is shown.
7. **اسأل معلّم**: one thread is past its SLA (red **متأخر** badge). The other has a simulated voice reply with its transcript. You can send one follow-up, then rate 1–5 to close. The product attaches the lesson, or the quiz attempt, automatically, lets the student pick a lesson when there is no context, counts questions per calendar month, and accepts one photo (PNG, JPG or WEBP); the prototype simulates the photo with a checkbox. The product marks a new reply «رد جديد» in the list until the student opens the thread. The product plays the voice reply from private storage with its transcript beneath. The product closes the thread after the teacher's reply to the follow-up, reminds teachers in-app at 12 h and 20 h («تذكيرات» on the inbox and a live toast), and pushes new replies to the student live.
8. **Switch to سارة (Free)**: only the first lesson in each unit is open. After 10 quiz questions today the paywall appears. Exams are paywalled too. Click **اشترك** to open the fake Paymob checkout, then **نجاح الدفع**. This simulates the webhook, which activates the plan. (Product: the paywall's **اشترك** links to `/student/subscription`, where the student picks a plan and checks out. The prototype jumps straight to checkout.)
9. **Teacher (أ. محمد)**: validation queue shows Physics only, with filters. Open a question to approve it (optionally change difficulty first) or reject it (a reason is required). Opening a Math question or thread by URL shows "غير مسموح". The inbox lets you claim a thread and reply with text or a simulated voice note plus an editable transcript. The product has the same three inbox tabs, shows the student's display name only, lets only the claiming teacher reply, and answers a lost claim race with «استلم معلم آخر هذا السؤال.» A personal stats card is at the top. The product also has bulk approve for questions opened in the current session and an age filter; the prototype does not simulate them. The product records the voice note in the browser, uploads it, transcribes it automatically in the background, and lets the teacher correct the transcript before sending. The product also has the AI grade review queue «مراجعة التصحيح» (#128): teachers accept or change AI essay and math step grades that wait in review, and the student sees the teacher's note; the teacher tab bar is three tabs plus «المزيد» (stats). The prototype does not simulate the AI grade review queue.
10. **Admin**:
    - Dashboard cards are computed live and can be filtered by subject and period.
    - Content tree supports CRUD, reordering with ▲▼, and publish/unpublish/archive. Publish "التكامل المحدد" and watch the servable total rise.
    - Question editor has a live preview and a **جرّب الإجابة** box that runs the real grader. Editing the stem, options or answer of an approved question sends it back to *pending* with version +1. Changing only the difficulty keeps it approved. Rejected questions get **تعديل وإعادة إرسال**. Fill-in and text short answers have per-rule answer-normalisation checkboxes, all on by default.
    - Blueprint editor refuses to save when there is a shortfall.
    - Users page: suspend students, grant plans, assign subjects to teachers. The last active admin cannot be deactivated. The product also has search, invitations (link plus email code), reactivation, and no self-deactivation; the prototype does not simulate them.
    - The product also has a payments page (log, needs-review queue, refunds); the prototype does not simulate it.
    - The product also has an assistant conversations page (list with search, and each conversation with the model, prompt version, tokens, cost and context of every reply); the prototype does not simulate it.
    - Audit log.
    - JSONL training-data export, with student ids hashed. The product requests an export per source with a date range and optional subject, prepares it in the background, lets the admin download it through the API with their session, and deletes the file after a retention period; the prototype downloads three files at once.
    - The product also has a Configuration page (runtime settings, feature flags, including the reply calendar and exam periods, read-only infrastructure and secret status); the prototype does not simulate it.

## Business rules implemented (PRD §17)

`servable()` is derived. Other rules implemented:
- Edits reset a question to pending.
- Only teachers of the subject can validate, and rejecting needs a reason.
- Quiz selection order: unseen → wrong → correct once → least-recent.
- Mastery means the last two attempts are ≥ 0.8.
- Headline counter.
- Exams are built from blueprints, with shortfall checks.
- Unlimited retakes, and the best score is shown.
- The avatar refuses to answer during an exam.
- Ask-a-Teacher has a 24h SLA and one follow-up.
- Entitlement changes only through the (simulated) webhook.
- Audit log.

Essay and math-with-steps questions are marked **v2**:
- Essay uses a keyword-overlap heuristic with a fake AI justification.
- Math-with-steps checks only the final answer ("تصحيح الخطوات في الإصدار 2").
- The built app grades the math final answer with a SymPy CAS check (equivalent forms, tolerance, form rules; #122) and, when a question has a steps weight, grades the steps with the LLM (OpenAI-compatible, docs/ai-service.md) against a model solution (#123), showing «جارٍ تصحيح إجابتك…» and then the per-step marks or «قيد المراجعة». The prototype does not simulate this.

The built app replaces the prototype's essay keyword list with a rubric (criteria, points, levels) and model answers (#117); essays are served since #119, and students write them in an RTL plain-text editor. The built app grades essays with the LLM (OpenAI-compatible, docs/ai-service.md) against the rubric (#118) instead of the keyword heuristic.

The prototype has no drag-and-drop type. The built app authors diagram questions (image, drop zones, items, correct zones, zone order) with a student preview (#125); they are served since #126: students place items on a canvas and are graded per item.

For console testing, `window.ElmanhgTest` exposes `grade`, `normAr`, `servable`, `selectQuiz`, `mergeBlueprints` and `state()`.
