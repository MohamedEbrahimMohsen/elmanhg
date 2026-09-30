# Elmanhg — Product Requirements Document

| | |
|---|---|
| Version | 1.0 (draft for agreement) |
| Date | 2026-09-25 |
| Product | Thanaweya Amma (الثانوية العامة) exam-prep platform |
| Status | Awaiting owner sign-off. No implementation started. |

---

## 1. One-paragraph summary

Elmanhg is an Arabic-first web platform for Thanaweya Amma students. Content is a strict tree: Subject → Unit → Lesson. Every lesson has an explanation, learning objectives, a summary, and a bank of teacher-validated questions. Students drill unlimited adaptive quizzes per lesson, sit blueprint-driven exams per unit or across units, and track mastery against a marketed total of 100,000 questions. A subject-scoped Teacher validates every question before it can ever be served. An Admin authors content and owns the business dashboards. Two add-ons: an AI Avatar that explains answers, and a paid "Ask a Teacher" channel with a 24-hour reply SLA whose text transcript is captured as training data.

---

## 2. Goals and success metrics

### 2.1 Product goals

1. A student can go from landing page to first graded quiz in under 3 minutes.
2. No question is ever shown to a student without a real teacher's approval.
3. Every quiz feels purposeful: the system prefers unseen and previously-wrong questions.
4. The "100,000 questions" claim is backed by a live, honest counter.
5. Every student ↔ teacher interaction and every AI-graded answer becomes clean text training data.

### 2.2 Success metrics (v1)

| Metric | Target |
|---|---|
| Free → paid conversion | ≥ 5% within 30 days |
| Weekly active / monthly active | ≥ 40% |
| Median questions answered per active day | ≥ 25 |
| Ask a Teacher SLA compliance | ≥ 95% replied within 24h |
| Question approval backlog | < 7 days median from creation to decision |

---

## 3. Personas

### 3.1 Student
- 16–18 years old, Egypt, Arabic-speaking, mostly on a phone.
- Wants: know what to study next, drill fast, see progress, understand mistakes.
- Pays: base subscription; optionally Ask a Teacher.

### 3.2 Teacher
- Subject specialist. Assigned to exactly one subject (or more, but each assignment is explicit).
- Role: content gatekeeper and paid Q&A responder. Not a classroom instructor inside the app.
- Can only see and act on content in assigned subjects.

### 3.3 Admin
- Platform owner / content operations.
- Authors all content, manages users and teachers, sees all dashboards and finances.

---

## 4. Scope and phasing

"All question types" is in scope for the product. Delivery is phased so v1 ships a complete platform on the deterministic types, then v2 adds the AI-graded and interactive types.

### 4.1 Version 1 (target ~3–4 months)

- All three roles, full auth and permission model.
- Full content tree with publish and validation workflows.
- Question types: **MCQ, Multi-select, True/False, Fill-in-the-blank, Short numeric/text answer**.
- Adaptive quiz engine, unit exams, multi-unit exams, exam blueprints.
- Scoring, mastery, progress, "questions remaining" counter.
- AI Avatar (explain lesson / explain my mistake).
- Ask a Teacher (text + voice reply, transcription to text, SLA timers).
- Paymob subscriptions (Base, Ask a Teacher).
- Admin dashboards (basic set, §11.3).
- Training-data capture.

### 4.2 Version 2 (target ~2–3 months after v1)

- **Essay** questions with AI grading against rubric + model answer, teacher spot-check.
- **Math with steps**: step-by-step input, AI step grading, CAS check of final answer.
- **Science drag-and-drop**: admin diagram authoring, student canvas, deterministic grading.
- Teacher grading-review queue for AI-graded answers.
- Native mobile wrapper (optional).

### 4.3 Explicitly out of scope (all versions, for now)

- KYC / identity verification.
- Live classes, video streaming, chat between students.
- Multiple grades or curricula beyond Thanaweya Amma.
- Parent accounts.

---

## 5. Content model

### 5.1 Hierarchy

```
Subject (e.g. Physics)
 └─ Unit (ordered)
     └─ Lesson (ordered)
         ├─ Explanation      (rich text, images, optional video embed)
         ├─ Objectives        (ordered list of learning objectives)
         ├─ Summary           (rich text)
         └─ Questions         (bank; each question belongs to exactly one lesson)
```

### 5.2 Lesson states

| State | Meaning |
|---|---|
| Draft | Admin editing. Invisible to students. |
| Published | Visible to students. Approved questions become servable. |
| Archived | Hidden from students; history retained. |

Only Admin publishes. Teachers do not publish lessons; they validate questions.

Transitions: **Publish** Draft or Archived → Published (sets `published_at`). **Unpublish** Published or Archived → Draft. **Archive** Published → Archived. A draft is deleted, not archived. A published lesson cannot be deleted; move it to draft or archive it first. A lesson that has questions cannot be deleted. Entering or leaving Published raises `LessonPublished`, `LessonUnpublished` or `LessonArchived`; servable and mastery recalculation subscribe to these. Every lesson read that a Student or Teacher can reach returns Published lessons only.

### 5.3 Question states

| Field | Values |
|---|---|
| Validation status | Pending · Approved · Rejected |
| Servable (derived, not stored) | `Approved AND lesson.state == Published AND question.not_retired` AND type != DragDrop (until the student canvas ships, E16.S2) |

Rules:
- A question is created as Pending. Only a Teacher assigned to the question's subject can Approve or Reject.
- Rejection requires a reason. Admin sees the reason and may edit and resubmit: resubmitting applies the edit (a content change still bumps the version), returns the question to Pending and clears the rejection reason.
- **Any edit to an Approved question's content resets it to Pending.** Content is the stem, body (options, blanks), grading spec, explanation and max score. Edits to difficulty, objective link or tags alone change neither the status nor the version. Every content edit, in any status, increments `version` and writes a `QuestionRevision` snapshot of the new version; version 1 is snapshotted at creation. The type of a question never changes.
- Approved questions on an unpublished lesson wait silently; the moment the lesson is Published they become servable with no further action.
- Retiring a question (Admin only) removes it from future quizzes but preserves all historical attempts. Retirement is final: a retired question cannot be edited, resubmitted, approved or rejected.

### 5.4 Question metadata (all types)

| Field | Notes |
|---|---|
| id, lesson_id, subject_id (denormalised) | |
| type | See §6 |
| stem | Rich text, Arabic, supports images and LaTeX |
| difficulty | Easy · Medium · Hard. Set by Admin, may be changed by Teacher at validation. |
| objective_id (optional) | Links to one of the lesson's objectives |
| tags (optional) | Free-form |
| explanation | Shown after answering; also fed to the Avatar |
| body (JSON) | Type-specific: options, blanks, canvas spec, etc. |
| grading_spec (JSON) | Type-specific: correct answer, rubric, tolerance, etc. |
| max_score | Points; default 1 |
| validation_status, validated_by, validated_at, rejection_reason | |
| version | Incremented on content edit |

---

## 6. Question types and grading

Every answer produces a **score in [0, max_score]** and a **normalised score in [0, 1]**. Deterministic types return 0 or 1 (or a partial for multi-select). AI-graded types return a partial score plus a written justification. Every grade may also carry a short feedback line in the student's language (for example, how many correct and wrong options a multi-select answer chose); the rules are in docs/question-schemas.md.

| Type | Version | Student input | Grading | Partial credit |
|---|---|---|---|---|
| MCQ (single) | v1 | One option | Exact match | No |
| Multi-select | v1 | Set of options | Set match; optional partial max(0, (correct chosen − wrong chosen) / number of correct options) | Optional |
| True/False | v1 | Boolean | Exact match | No |
| Fill-in-the-blank | v1 | One string per blank | Normalised match (Arabic diacritics stripped, whitespace collapsed, alef/hamza/taa-marbuta variants unified); accepted-answers list per blank | Per blank |
| Short answer (numeric/text) | v1 | String or number | Numeric: tolerance ±x or %; Text: accepted list with normalisation | No |
| Essay | v2 | Plain text (Arabic, multi-paragraph) | LLM grader with rubric (criteria + weights) and model answer. Returns score per criterion + justification. | Yes |
| Math with steps | v2 | Ordered list of steps (LaTeX/text) + final answer | Final answer: CAS equivalence check (SymPy). Steps: LLM against model solution, per-step credit, weighted per question (steps weight). | Yes |
| Science drag-and-drop | v2 | Map of item → drop zone (per-zone order when the zone is ordered) | Deterministic: each item's zone vs correct zone; optional order constraints | Per item |

Math with steps: each question sets a required form and an optional numeric tolerance for the final answer, and optionally a model solution and a steps weight w (0–100 %) (docs/question-schemas.md, docs/math-cas.md). Step grading (E15.S3, #123) awards 0, 1 or 2 points per model-solution step. The score is ((100 − w) × F + w × S) ÷ 100 of the max score, where F is 1 for an equivalent final answer (else 0) and S is the step points ÷ (2 × model steps); w = 0 (the default) grades the final answer only. A blank final answer is Unanswered even with steps; with w > 0 and no written steps the steps earn 0 without an AI call. When step grading is needed, or the CAS cannot check the final answer (for example, the AI service is down), the answer is graded in the background: the student sees «جارٍ تصحيح إجابتك…», then the verdict with per-step marks, or «قيد المراجعة» when the grade goes to teacher review (§8.3); the correct answer stays hidden until the grade is applied, and the quiz or exam score is marked provisional. A check that fails is retried; after the retries the answer goes to review. See docs/math-step-grading.md.

Per-type `body` and `grading_spec` JSON shapes: `docs/question-schemas.md`.
Answer shapes and the exact grading rules (normalisation, numeric parsing, rounding) are in the same document.

### 6.1 AI grading rules (v2)

- The grader receives: question stem, rubric, model answer, student answer, subject, lesson objectives. Never the student's identity.
- Output is structured JSON: per-criterion score, total, one-paragraph Arabic justification, confidence.
- Low-confidence grades (below a configurable threshold) are queued for teacher review before the score is final. The student sees "قيد المراجعة" in the meantime.
- A teacher may override any AI grade. Overrides are training data (§13).
- Decided (#118): the threshold is `EssayGrading:ReviewConfidenceThreshold` (default 0.7); a grade the AI cannot produce after `EssayGrading:MaxAttempts` also goes to teacher review; grading is a background job with retries; the admin test grader grades essays synchronously ([essay-grading.md](essay-grading.md)).
- Decided (#123): math step grading returns 0–2 points per model-solution step with a justification each, a one-paragraph justification and a confidence; the threshold is `MathStepGrading:ReviewConfidenceThreshold` (default 0.7); grading and failed final-answer checks are retried in the background (`MathStepGrading:MaxAttempts`, 4) before going to teacher review; the admin test grader grades steps synchronously ([math-step-grading.md](math-step-grading.md)).
- Decided (#128): teachers accept or override the AI grades that land in review (§8.3); a grade the AI applied directly is final and is not re-opened, because attempts are append-only. Accept and override both write the attempt with `graded_by = Teacher` ([grade-review.md](grade-review.md)).

### 6.2 Answer normalisation (Arabic)

Applied to fill-in and short-text answers before comparison.

Each rule can be switched off per question; all are on by default: strip tashkeel; strip tatweel; unify أ إ آ ٱ → ا; ة → ه; ى → ي; convert Arabic-Indic digits to ASCII; collapse whitespace; case-fold Latin characters.

Always applied: removal of invisible bidi and zero-width marks, Unicode NFC, ، → `,`, ی → ي, and trimming.

Numeric answers ignore the per-question rules and must be a plain decimal (`docs/question-schemas.md`).

---

## 7. Student experience

### 7.1 Navigation

1. Landing (live servable counter, value props, plans) → sign up (phone + one-time code, or email + password) → choose subjects of interest (skippable; editable later from Home). Students with an email account can also sign in with a one-time code sent to that email; teachers and admins always sign in with email and password. An invited teacher or admin sets their first password at `/accept-invite` after proving the email with a one-time code.
2. Home: subjects with per-subject mastery and "next recommended lesson".
3. Subject → Units (ordered, with mastery %) → Lessons (ordered, with mastery %).
4. Lesson page tabs: Explanation · Objectives · Summary · Practice.

Home lists the chosen subjects first under «موادك», the rest under «مواد أخرى».

Free tier: can browse the tree and read the first lesson of each unit; quizzes limited to a small daily count. Paid tier: unlimited.

**Sign-in code delivery.** Phone codes go by WhatsApp (Meta WhatsApp Cloud API, approved authentication template). SMS through a local telecom's HTTP gateway is built but off; when WhatsApp is disabled and SMS is enabled, phone codes go by SMS. Email codes go through Resend. Each channel is switched on and pointed at its provider by configuration only. With no channel enabled for the recipient the request fails with 503 `OTP_CHANNEL_UNAVAILABLE`; a provider failure returns 503 `OTP_DELIVERY_FAILED`. The code screen names the channel used. Configuration and go-live steps: `docs/otp-delivery.md`.

### 7.2 Quiz engine (per lesson)

A quiz is a session of N questions (default 10, student may choose 5/10/20) drawn from the lesson's servable questions.

**Selection priority** (fill from bucket 1, then 2, …, until N; random within a bucket):

1. Never attempted by this student.
2. Last attempt wrong (normalised score < mastery threshold), oldest first.
3. Correct exactly once.
4. All others, weighted toward the least-recently seen.

If the lesson has fewer than N servable questions, the quiz is shorter. The student is never shown the same question twice in one session.

Per question the student sees immediate feedback: correct/incorrect, correct answer, explanation, and an "اسأل المساعد" button (Avatar, §9).

Session result: score out of 100, time, per-question review.

### 7.3 Mastery and progress

| Term | Definition |
|---|---|
| Attempt | One student answer to one question inside any quiz or exam. Stored forever. |
| Mastery threshold | Normalised score ≥ 0.8 on an attempt counts as "correct" for mastery. |
| Question mastered | Correct on the **two most recent** attempts. Loses mastery on a wrong attempt. |
| Lesson mastery % | mastered / servable questions in lesson |
| Unit / Subject mastery % | Weighted by question count |
| Questions remaining (headline) | Total servable questions on the platform − questions mastered by this student |
| Seen (secondary) | Attempted at least once |

The headline counter is shown on Home as "متبقّي لك X سؤال من 100,000". Both the platform total and the student's mastered count are live.

### 7.4 Unit exam

- Available once the student has opened every lesson in the unit (configurable; default: no gate).
- Generated from the unit's **exam blueprint** (§10.2): fixed counts per question type and optionally per difficulty.
- Selection prefers questions **not** already mastered, then random. Questions are drawn across all lessons in the unit.
- Optional time limit set in the blueprint.
- Submitted as a whole; no per-question feedback until submission (unlike quizzes).
- A student has at most one exam in progress.
- Answers are auto-saved as drafts; with a time limit, saves close at the deadline (plus a short grace) and the exam is submitted automatically.
- Unanswered questions score 0.
- Submitted exam answers are attempts and count toward mastery (§7.3).
- Result: score /100, per-lesson breakdown, weakest objectives.
- Retakes: unlimited. **Best score** is the displayed unit-exam score; all attempts are kept and visible in history.

### 7.5 Multi-unit exam

- Student selects 2+ units within one subject.
- The system merges the selected units' blueprints proportionally to a target size chosen by the student (20/40/60 questions), or uses the subject's default blueprint if units have none.
- Sizes are exactly 20, 40 or 60.
- Each unit's share is proportional to its blueprint's question count; if a unit is short of a type, the rest comes from the other selected units.
- Time limit and pass mark are the question-weighted combination of the units' blueprints; if any contributing blueprint is untimed, the exam is untimed.
- Result adds a per-unit breakdown.
- Same rules as unit exam otherwise.

### 7.6 Progress page

- Summary: headline counter and day streak (student-wide, shown once: consecutive days with ≥ 1 non-test quiz attempt, `docs/mastery.md`).
- Per subject: mastery %, unit exam best scores.
- Weak spots: lowest-mastery lessons and objectives, with a "درّب الآن" shortcut.
- History: all quiz and exam sessions, filterable.

---

## 8. Teacher experience

### 8.1 Validation queue

- Lists Pending questions for the teacher's assigned subjects only. Filters: unit, lesson, type, difficulty, age.
- Question view: full stem, body, grading spec, explanation, admin's chosen difficulty, question version, and prior rejection history.
- Actions: **Approve**, **Reject (reason required)**, **Change difficulty then Approve**.
- A teacher cannot edit stem/options/answers. If it is wrong, reject with a reason. (Keeps authorship with Admin and the audit trail clean.)
- Bulk approve is allowed only for questions opened in the current review session. The server records each opening against the question's current version, and a session lasts until reload, sign-out or `ReviewSessionLifetimeMinutes`. An edit after opening voids it.
- Approve and reject name the version the teacher reviewed; a newer version is refused (`QUESTION_VERSION_CHANGED`). Age = time since the current version entered review.

### 8.2 Ask a Teacher inbox (§12)

### 8.3 AI grade review queue (v2)

- The queue lists, per assigned subject, essay grades and math step grades in review: low confidence, grading failed, or final answer unchecked after retries. Test-mode sessions are excluded, and grades are shown oldest first.
- The teacher sees the question (served version), the grading key, the answer, and the AI score, confidence and reasons. No student identity is shown.
- Accept is possible only when the AI produced a score. Override takes a score from 0 to full marks (2 decimals) and a required note.
- The decision is final. It writes the attempt (`graded_by = Teacher`), mastery and the session score, and the student sees the note. Until then the student sees «قيد المراجعة» instead of a verdict.
- Legacy `mathUnchecked` attempts from before #123 are not reviewed (no live deploy).
- Details: `docs/grade-review.md`.

### 8.4 Teacher visibility limits

- Cannot see students' identities beyond a display name in Ask a Teacher threads.
- Cannot see other subjects, finances, or platform dashboards.
- Sees a personal stats card on the teacher home and «إحصائياتي»: approved/rejected counts, median decision time, and reply SLA compliance (with reply count and median reply time) over the last 30 Cairo days (`docs/dashboard.md`).

---

## 9. AI Avatar

An in-app assistant for students, scoped to the platform's content.

### 9.1 Entry points

| Where | Context passed |
|---|---|
| Lesson page | Subject, unit, lesson explanation/objectives/summary |
| After answering a quiz question | Above + question, student's answer, correct answer, explanation |
| Exam review | Above, per question |
| Global | Subject list; asks the student to pick a lesson |

### 9.2 Behaviour

- Answers in Egyptian-friendly Modern Standard Arabic; short, step-based.
- Grounded on lesson content via retrieval (embeddings over explanation/summary/explanation-of-questions). Cites the lesson section it drew from (each reply lists its sources, which link to the lesson tab).
- While the student has an exam in progress (open, and not past its deadline plus the grace period), the avatar refuses every message without calling the model. It explains freely after submission and in quizzes.
- Refuses off-curriculum requests politely and redirects.
- Every conversation is stored (§13) with the context bundle.

### 9.3 Limits

- Rate-limited per student per day: Free 5, Base 50 (`Subscriptions` configuration). A message counts once the assistant has replied; the day follows `Subscriptions:DailyQuotaTimeZone`.
- Model and prompt versions, tokens and cost are recorded on every reply for later evaluation, with the context bundle and search results that were sent (`docs/avatar.md`, Conversation log).

---

## 10. Admin experience

### 10.1 Content management

- CRUD for Subjects, Units, Lessons with ordering (drag to reorder).
- Lesson editor: three rich-text areas (Explanation, Objectives as list, Summary), image upload, LaTeX support, video embed URL.
- Question editor per type, with live preview of the student view and a "test answer" box that runs the real grader.
- Bulk import of questions via spreadsheet template (v1: deterministic types only). Imported questions enter as Pending; drag-and-drop diagrams are authored in the editor only.
- Question list with validation status, version, teacher, rejection reasons; one-click "edit and resubmit".
- Publish / unpublish / archive lessons.

### 10.2 Exam blueprints

Per unit (and a default per subject):

| Field | Example |
|---|---|
| Question type → count | MCQ: 10, Fill-in: 5, True/False: 5, Essay: 1 |
| Difficulty mix (optional) | Easy 30% · Medium 50% · Hard 20% |
| Time limit (optional) | 45 min |
| Pass mark | 50 |

Validation: the blueprint cannot be saved if the unit lacks enough servable questions of any required type; the editor shows the shortfall.

The subject default is checked against the subject's whole servable pool; a unit blueprint against its unit's pool. The difficulty mix is a target (whole percentages summing to 100) and is not part of the save check. Pass mark is 1–100; time limit, when set, is at least 1 minute. A unit blueprint can be removed, returning the unit to the subject default; the subject default cannot be removed. Details: `docs/exam-blueprints.md`.

### 10.3 Dashboards (v1 basic set)

| Card / chart | Definition |
|---|---|
| Students | Total, new this week, active today (DAU), active this month (MAU) |
| Subscribers | Active by plan, churned this month, MRR |
| Content | Subjects, units, lessons (published/draft), questions by status and type; **servable total** (the marketed number) |
| Solve rate | Attempts per active student per day |
| Success rate | Correct attempts / attempts, overall and per subject/unit/lesson |
| Validation | Pending backlog, median time to decision, per-teacher throughput |
| Ask a Teacher | Open threads, SLA breaches, median reply time |
| Payments | Successful / failed transactions, revenue by day, refunds |
| Sign-up funnel | Distinct visitors per step: landing viewed → sign-up started → account created → onboarding done → first quiz answer, plus median landing-to-first-answer time (`docs/analytics.md`) |

Every chart except Content is filterable by date range (Cairo days); Content is a current snapshot with no date range. Content, Solve rate, Success rate, Validation and Ask a Teacher are also filterable by subject; Students, Subscribers, Payments and the Sign-up funnel have no subject dimension. Definitions, caching and endpoints: `docs/dashboard.md`.

### 10.4 User management

- One page, `/admin/users`, with Students, Teachers and Admins tabs. Each tab has server-side search and paging. Search matches the name (any part), the full mobile number or the full email; a partial number or email matches nothing.
- Contact data is always masked on these screens (`010*****678`, `m***@example.test`).
- Students: search, view profile, progress and session history, suspend and reactivate, grant a complimentary subscription.
- Teachers: invite, assign/unassign subjects, deactivate and reactivate.
- Admins: invite, deactivate and reactivate. At least one active admin must always remain, and an admin can never deactivate their own account.
- Suspending or deactivating signs the user out everywhere: sign-in and token refresh stop at once, and an access token already issued is refused on its next request.
- Invitations: the invite creates the account without a password and emails the `/accept-invite` link when email delivery is configured; the admin can also copy the link. The link carries no email or secret. The invitee proves the email with a one-time code and chooses a password. An invitation does not expire; an admin can deactivate a pending invitee.
- Complimentary grant: Base (any period sold) or Ask a Teacher (monthly, and only with an active Base), free of charge, starting now. A plan the student already holds cannot be granted. The grant is audited and shows as «مجاني» in the student's subscriptions.
- Rules, endpoints and error codes: `docs/user-administration.md`.

### 10.5 Assistant conversations

Admins see the students' Avatar conversations, most recent first, with search by message text or student name and filters by entry point and date. Each conversation shows every message and, per reply, the model, prompt version, tokens, cost, citations and the context sent. See `docs/avatar.md`.

---

## 11. Subscriptions and payments

### 11.1 Plans

| Plan | Includes |
|---|---|
| Free | Browse tree; first lesson of each unit; 10 quiz questions/day; Avatar 5 messages/day |
| Base (monthly / termly / yearly) | Unlimited quizzes and exams; full progress; Avatar with higher limit |
| Ask a Teacher (add-on, monthly) | Requires Base. N questions/month (configurable, e.g. 20) with 24-hour reply SLA |

Prices, billing periods and quotas are configuration (`Subscriptions` section, see `docs/subscriptions.md`), not code. Base is sold monthly (1 month), termly (4 months) and yearly (12 months); Ask a Teacher monthly only. Shipped defaults: Free 10 quiz questions/day, 5 Avatar messages/day, first lesson of each unit; Base 50 Avatar messages/day; Ask a Teacher 20 questions/month with a 24-hour SLA. Prices have no default and must be configured; money is stored in minor units (piastres) with an ISO 4217 currency.

### 11.2 Paymob integration

- Card and mobile wallet via Paymob checkout. Checkout creates a pending payment and redirects to Paymob's unified checkout; the return page only reads the payment status and never activates a plan. A plan already held cannot be bought again until its renewal window opens, and Ask a Teacher needs an active Base. Configuration and go-live: `docs/paymob.md`.
- Subscription state is driven **only by Paymob-verified events**: HMAC-verified webhooks, or Paymob's response to an admin refund; or by an admin's complimentary grant (§10.4). The client never sets entitlement.
- States: Active · PastDue · Cancelled · Expired (Trialing is not used in v1). Access is derived from state and dates, never stored: Active and PastDue grant access until the end of the paid period plus the grace period; Cancelled grants access until the end of the paid period; Expired grants none. Ask a Teacher grants access only while Base does.
- Grace period on failed renewal: 3 days, then downgrade to Free.
- v1 has no automatic charge: the student renews by paying again from 7 days before the period end (configurable), and the new period continues from the old end.
- A verified payment for a plan the student already holds extends it; an Ask a Teacher payment without Base is kept and flagged for admin review.
- The student may cancel: access continues until the paid period ends.
- A status sweep marks lapsed plans PastDue, then Expired.
- Full transaction log with a needs-review queue for the admin; refunds are initiated by an admin (full amount, reason required), executed in Paymob, recorded locally, and remove the time the payment bought (the plan ends at once if nothing paid remains). Signed Paymob refund callbacks have the same effect; a partial refund made in Paymob is flagged for review. Details: `docs/subscriptions.md` → Refunds.

---

## 12. Ask a Teacher

### 12.1 Flow

1. Student (with add-on) opens "اسأل معلّم" from a lesson or question, or the quiz attempt being asked about. Context (subject/unit/lesson/question) is attached automatically; student writes text and may attach an image (e.g. a photo of their work).
   Each new question counts against the monthly quota, which resets on the 1st of each calendar month in `DailyQuotaTimeZone` (Africa/Cairo); a follow-up does not count. The student may attach one photo (PNG, JPG or WEBP). Photos are private: only the owning student, a teacher assigned to the thread's subject, or an admin can open one (`docs/ask-teacher.md`).
2. The thread is routed to the queue of teachers assigned to that subject. First teacher to claim it owns it.
3. SLA clock starts at submission. Reminders to the teacher at 12h and 20h; admin alert on breach. A follow-up starts a new 24-hour window; the clock never pauses.
4. Teacher replies with **text or voice**. Voice is recorded in-browser, stored, and transcribed to Arabic text automatically. The teacher sees the transcript and can correct it before sending.
5. Student receives the reply (audio player + text). Student may send **one** follow-up on the same thread; the teacher replies once more; the thread then closes. Anything further is a new question against the monthly quota.
6. Student rates the answer (1–5). Ratings are visible to admin.

### 12.2 Storage

- Audio files are kept for playback.
- **The training record is text only** (§13): question text, attached context ids, final corrected transcript, rating.

---

## 13. Training-data capture

Everything below is written to append-only tables, keyed by anonymised student id, with subject/unit/lesson/question references and timestamps.

| Source | Record |
|---|---|
| Ask a Teacher | Student question (text), context bundle, teacher reply (final text), rating |
| Avatar | Full conversation, context bundle, model + prompt version |
| AI grading (v2) | Student answer, rubric, AI score + justification, teacher override (if any) |
| Attempts | Every answer, score, time taken — for difficulty calibration |

The anonymised id is an HMAC-SHA256 of the student id under a server secret. Admin test-mode sessions are not recorded. AI grades are recorded when the grader completes (`EssayGradeTrainingRecords`); a teacher review adds a `TeacherReviewed` row (#128). Tables, triggers and the privacy checklist: `docs/training-data.md`.

Exports (admin only): JSONL per source, date-ranged, optionally per subject, with PII stripped (contact data masked in texts, raw source ids never exported). An export is prepared in the background, downloaded only by an admin through the audited API, and its file is deleted after a configurable retention (7 days by default).

---

## 14. Non-functional requirements

| Area | Requirement |
|---|---|
| Language | Arabic UI, RTL throughout. English only for admin technical fields if needed. |
| Devices | Mobile-first responsive web. All interactions touch-friendly. v2 canvas and math input must work on phones. |
| Performance | Lesson page < 2s on 3G-class connections; quiz question transition < 300ms (prefetch next). Budgets and how they are measured: docs/performance.md. |
| Availability | 99.5% monthly, measured and alerted as in docs/observability.md. Exam sessions auto-save every answer; a refresh resumes the session. |
| Security | Role-based authorisation on every endpoint; teacher subject scoping enforced server-side; Paymob webhooks HMAC-verified; rate limits on auth and Avatar. |
| Privacy | Students identified to teachers by display name only. Training exports strip PII. |
| Auditability | All content changes and validation decisions logged with actor and timestamp. |
| Accessibility | Readable font sizes, sufficient contrast, keyboard-navigable quizzes. |

---

## 15. Data model (entities and key fields)

```
User(id, role[Student|Teacher|Admin], phone, email, display_name, status, onboarded_at?, subject_interest_ids[])
TeacherSubject(teacher_id, subject_id)

Subject(id, name, order)
Unit(id, subject_id, name, order)
Lesson(id, unit_id, name, order, state[Draft|Published|Archived],
       explanation, summary, video_url?, published_at)
LessonObjective(id, lesson_id, text, order)

Question(id, lesson_id, subject_id, type, stem, difficulty, objective_id?, tags[],
         body_json, grading_spec_json, explanation, max_score, version,
         validation_status, validated_by?, validated_at?, rejection_reason?,
         retired_at?)
QuestionRevision(question_id, version, snapshot_json, edited_by, edited_at)

ExamBlueprint(id, subject_id, unit_id?, type_counts_json, difficulty_mix_json?, question_count, time_limit_min?, pass_mark)  -- unit_id null = the subject default; one default per subject, one per unit (docs/exam-blueprints.md)

Session(id, student_id, kind[Quiz|UnitExam|MultiUnitExam], scope_json, scope_key, is_test_mode,
        started_at, last_activity_at, submitted_at?, score_pct?, time_limit_min?, pass_mark?, deadline?)
SessionItem(id, session_id, position, question_id, question_version, max_score,
            saved_answer_json?, answer_saved_at?)  -- served questions fixed at start; exam drafts until submission
Attempt(id, session_id, student_id, question_id, question_version,
        answer_json, score, normalised_score, graded_by[Auto|AI|Teacher],
        grade_json?, time_taken_ms, created_at)  -- append-only
QuestionMastery(student_id, question_id, mastered bool, latest_attempt_id, latest_normalised_score, latest_attempted_at, previous_attempt_id?, previous_normalised_score?, previous_attempted_at?, updated_at)  -- materialised from the two most recent attempts (docs/mastery.md)

Subscription(id, student_id, plan[Base|AskTeacher], period[Monthly|Termly|Yearly], status[Active|PastDue|Cancelled|Expired], current_period_start, current_period_end, cancelled_at?, expired_at?, paymob_ref?)
Payment(id, student_id, subscription_id?, plan, period, period_months, amount_minor, currency, status[Pending|Succeeded|Failed|Refunded], paymob_txn_id?, provider_order_id?, raw_webhook_json?, completed_at?, review_reason?, review_resolved_at?, review_resolved_by?, refunded_at?, refunded_by?, refund_reason?, refund_transaction_id?, refund_idempotency_key?, created_at)  -- docs/subscriptions.md

TeacherThread(id, student_id, teacher_id?, subject_id, context_json, status[Open|Answered|Closed], submitted_at, sla_due_at, claimed_at?, closed_at?, rating?)  -- docs/ask-teacher.md
TeacherMessage(id, thread_id, sender_id, kind[Text|Voice], text, image_url?, audio_url?, audio_duration_seconds?, transcript_final bool, student_read_at?, created_at)
TeacherVoiceDraft(id, thread_id, teacher_id, audio_key, audio_url, audio_duration_seconds, status[Pending|Ready|Failed|Sent], transcript?, transcription_model?, attempts, next_attempt_at?, recorded_at, transcribed_at?, sent_message_id?)  -- transcription job; docs/ask-teacher.md
TeacherThreadSlaEvent(id, thread_id, kind[FirstReminder|SecondReminder|Breach], sla_due_at, teacher_id?, occurred_at)  -- one per SLA window and stage; reminders and breach record (docs/ask-teacher.md)

AvatarConversation(id, student_id, entry_point, subject_id?, unit_id?, lesson_id?, session_id?, question_id?, started_at, last_message_at, message_count)  -- docs/avatar.md
AvatarMessage(id, conversation_id, position, role[Student|Assistant], text, created_at, model?, prompt_version?, input_tokens?, output_tokens?, cost_usd?, stop_reason?, history_message_count?, context_json?, citations_json?)  -- append-only; replies carry the context bundle, model and prompt version
AvatarMessageUsage(id, student_id, entry_point, created_at)  -- daily Avatar quota counter (docs/avatar.md)
AttemptTrainingRecord(id, student_hash, attempt_id, question_id, question_version, subject_id, unit_id, lesson_id, session_kind, answer_json, score, normalised_score, graded_by, grade_json?, time_taken_ms, occurred_at, recorded_at)  -- append-only; docs/training-data.md
AvatarTrainingRecord(id, student_hash, conversation_id, student_message_id, assistant_message_id, student_message_position, entry_point, subject_id?, unit_id?, lesson_id?, question_id?, student_text, assistant_text, model, prompt_version, context_json, asked_at, occurred_at, recorded_at)  -- append-only
TeacherThreadTrainingRecord(id, student_hash, thread_id, trigger[Closed|RatedAfterClose], subject_id, unit_id, lesson_id, question_id?, question_version?, attempt_id?, context_json, messages_json, rating?, submitted_at, occurred_at, recorded_at)  -- append-only; one per trigger
EssayGradeTrainingRecord(id, student_hash, essay_grade_id, question_id, question_version, subject_id, unit_id, lesson_id, session_kind, answer_json, max_score, score, normalised_score, criteria_json, justification, confidence, outcome[Graded|InReview], model, prompt_version, trigger[Completed|TeacherReviewed], review_decision?, reviewed_score?, reviewed_normalised_score?, review_comment?, reviewed_at?, occurred_at, recorded_at)  -- append-only; one per AI grade and trigger
TrainingExport(id, source[Attempts|Avatar|TeacherThreads|EssayGrades], from, to, subject_id?, status[Pending|Completed|Failed|Expired], attempts, next_attempt_at?, last_error_code?, requested_at, completed_at?, expires_at?, file_key?, row_count?, file_size_bytes?, sha256?, created_by)  -- JSONL export job; docs/training-data.md

LessonContentChunk(id, lesson_id, section[Explanation|Objectives|Summary|QuestionExplanation], section_title?, position, question_id?, question_version?, content, embedding vector(1536), embedding_model, created_at)  -- derived; docs/content-retrieval.md
LessonContentIndex(id, lesson_id, source_updated_at, questions_updated_at?, chunk_count, embedding_model?, indexed_at)

FunnelEvent(id, anonymous_id, user_id?, type[LandingViewed|SignUpStarted|SignUpCompleted|OnboardingCompleted|FirstQuizAnswered], occurred_at)  -- docs/analytics.md
UserActivityDay(id, user_id, day, first_seen_at)  -- one row per user per Cairo day; DAU/MAU (docs/dashboard.md)

AuditLog(id, actor_id, actor_name, actor_role, action, entity, entity_id, outcome, error_code, diff_json, trace_id, created_at)  -- append-only
```

---

## 16. Permission matrix

| Capability | Student | Teacher | Admin |
|---|:-:|:-:|:-:|
| Browse published tree | ✓ | ✓ (assigned subjects) | ✓ |
| Take quizzes / exams | ✓ | – | ✓ (test mode) |
| Create / edit content | – | – | ✓ |
| Publish lessons | – | – | ✓ |
| Approve / reject questions | – | ✓ (assigned subjects) | – |
| Change question difficulty | – | ✓ at validation | ✓ |
| Manage blueprints | – | – | ✓ |
| Reply to Ask a Teacher | – | ✓ (assigned subjects) | ✓ |
| Ask a Teacher (submit) | ✓ | – | – |
| Use the AI Avatar | ✓ | – | – |
| Override AI grade (v2) | – | ✓ (assigned subjects) | ✓ |
| View own progress | ✓ | – | – |
| View any student's progress | – | – | ✓ |
| Manage own subscription | ✓ | – | – |
| Dashboards / finance | – | own stats only | ✓ |
| Payment log and refunds | – | – | ✓ |
| Manage users / teachers | – | – | ✓ |
| View audit log | – | – | ✓ |
| View Avatar conversations | – | – | ✓ |
| Export training data | – | – | ✓ |

Admins deliberately cannot approve questions. This keeps the "validated by a real teacher" claim true.

---

## 17. Key business rules (single list, for implementation reference)

1. Servable = Approved ∧ Lesson Published ∧ not retired. Derived, never stored; drag-and-drop questions are not servable until the student canvas ships.
2. Content edit on an Approved question → Pending, version + 1. Historical attempts keep the old version.
3. Only a Teacher assigned to the subject may validate. Admins cannot.
4. Rejection requires a reason.
5. Quiz selection order: unseen → last wrong → correct once → rest (least-recent first).
6. Mastery = normalised score ≥ 0.8 on the two most recent attempts.
7. Headline counter = platform servable total − student mastered.
8. Exams are generated from blueprints; a blueprint cannot be saved with a shortfall.
9. Exam retakes unlimited; best score displayed; all kept.
10. Avatar never reveals answers during an in-progress exam.
11. Ask a Teacher: 24h SLA from submission (a follow-up opens a new 24h window); one follow-up per thread; voice always transcribed; training record is text only.
12. Subscription entitlement changes only through Paymob-verified events: HMAC-verified webhooks, or Paymob's response to an admin refund; or through an admin's complimentary grant (§10.4). The client never sets entitlement.
13. Every content change and validation decision is audit-logged.

---

## 18. Recommended technical shape (for the implementation workflow to adopt or adjust)

- **Backend**: .NET 10, DDD/CQRS, PostgreSQL (JSONB for question bodies; pgvector for Avatar retrieval).
- **AI/grading service**: Python FastAPI — LLM grading (v2), SymPy CAS checks (v2), Avatar embeddings + generation (v1). Retrieval search itself runs in the .NET API over pgvector (see `docs/content-retrieval.md`), speech-to-text through OpenAI Whisper (v1; the API's background worker schedules and retries it).
- **LLM**: Claude API. Confirm current model IDs and pricing at build time.
- **Embeddings**: OpenAI text-embedding-3-small (1536) through the AI service; Anthropic has no embeddings API. Fake by default.
- **Frontend**: React + TypeScript, shadcn/ui, i18next RTL. v2 adds a math input with LaTeX preview, a drag-and-drop canvas, and a rich Arabic editor.
- **Media**: S3-compatible object storage for images and audio; private media (question photos, voice replies) is served only through the API after an access check.
- **Jobs / realtime**: background jobs for grading, transcription, SLA reminders; the web polls for essay grade results (#118); SignalR for teacher replies.
- **Payments**: Paymob, webhook-driven.
- **Hosting**: Docker Compose on one VPS per environment (staging, production); Caddy (TLS, SPA, /api proxy), images built by CI and pushed to GHCR; PostgreSQL + pgvector. Object storage (from #96) is a managed S3-compatible service (Cloudflare R2 or AWS S3), set by config; local dev and CI use the local-disk store, and there is no object-store container in compose. Runbook: docs/deployment.md.
- **Observability**: OpenTelemetry traces and metrics (api, ai), JSON logs from every container, self-hosted collector + Prometheus + Loki + Tempo + Grafana + Alertmanager behind the `observability` compose profile; runbook docs/observability.md.

---

## 19. Open questions (do not block v1 start, but need answers before the related feature is built)

1. Exact plan pricing and the Ask a Teacher monthly quota. Defaults are configuration (docs/subscriptions.md); final prices are still open.
2. Whether teachers are paid per reply / per validation (affects the teacher stats card).
3. Multi-select partial-credit formula: on or off by default?
4. Should unit-exam access be gated on opening all lessons? (Default: no gate.)
5. Avatar daily limits for Free vs Base. Configured defaults: Free 5/day, Base 50/day.
6. Transcription provider for Arabic voice (evaluate quality on Egyptian dialect before committing). The evaluation harness is in `docs/ai-service.md`; the run waits for recorded Egyptian-dialect clips.
7. v2: essay rubric format. Decided (#117): a free criteria list per question; each criterion has points (its weight) and a level scale from 0 to full points; one to three model answers.

---

## 20. Glossary

| Term | Meaning |
|---|---|
| Servable | A question that may be shown to students right now |
| Attempt | One answer to one question |
| Mastered | Correct on the last two attempts |
| Blueprint | Per-unit recipe of question counts per type for an exam |
| Context bundle | The subject/unit/lesson/question ids attached to an Avatar or teacher thread; a teacher thread also keeps the attempt id when asked from a quiz answer |
