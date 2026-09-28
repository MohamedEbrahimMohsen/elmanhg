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
| Servable (derived, not stored) | `Approved AND lesson.state == Published AND question.not_retired` |

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
| Essay | v2 | Rich text | LLM grader with rubric (criteria + weights) and model answer. Returns score per criterion + justification. | Yes |
| Math with steps | v2 | Ordered list of steps (LaTeX/text) + final answer | Final answer: CAS equivalence check (SymPy). Steps: LLM against model solution, per-step credit. | Yes |
| Science drag-and-drop | v2 | Map of item → drop zone | Deterministic: each item's zone vs correct zone; optional order constraints | Per item |

Per-type `body` and `grading_spec` JSON shapes: `docs/question-schemas.md`.
Answer shapes and the exact grading rules (normalisation, numeric parsing, rounding) are in the same document.

### 6.1 AI grading rules (v2)

- The grader receives: question stem, rubric, model answer, student answer, subject, lesson objectives. Never the student's identity.
- Output is structured JSON: per-criterion score, total, one-paragraph Arabic justification, confidence.
- Low-confidence grades (below a configurable threshold) are queued for teacher review before the score is final. The student sees "قيد المراجعة" in the meantime.
- A teacher may override any AI grade. Overrides are training data (§13).

### 6.2 Answer normalisation (Arabic)

Applied to fill-in and short-text answers before comparison.

Each rule can be switched off per question; all are on by default: strip tashkeel; strip tatweel; unify أ إ آ ٱ → ا; ة → ه; ى → ي; convert Arabic-Indic digits to ASCII; collapse whitespace; case-fold Latin characters.

Always applied: removal of invisible bidi and zero-width marks, Unicode NFC, ، → `,`, ی → ي, and trimming.

Numeric answers ignore the per-question rules and must be a plain decimal (`docs/question-schemas.md`).

---

## 7. Student experience

### 7.1 Navigation

1. Landing → sign up (phone + OTP, or email) → choose subjects of interest.
2. Home: subjects with per-subject mastery and "next recommended lesson".
3. Subject → Units (ordered, with mastery %) → Lessons (ordered, with mastery %).
4. Lesson page tabs: Explanation · Objectives · Summary · Practice.

Free tier: can browse the tree and read the first lesson of each unit; quizzes limited to a small daily count. Paid tier: unlimited.

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
- Result: score /100, per-lesson breakdown, weakest objectives.
- Retakes: unlimited. **Best score** is the displayed unit-exam score; all attempts are kept and visible in history.

### 7.5 Multi-unit exam

- Student selects 2+ units within one subject.
- The system merges the selected units' blueprints proportionally to a target size chosen by the student (20/40/60 questions), or uses the subject's default blueprint if units have none.
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

- Low-confidence AI grades for the teacher's subjects.
- Teacher sees student answer, AI score and justification; can accept or override with a score and comment.

### 8.4 Teacher visibility limits

- Cannot see students' identities beyond a display name in Ask a Teacher threads.
- Cannot see other subjects, finances, or platform dashboards.
- Sees a personal stats card: approved/rejected counts, median decision time, SLA compliance.

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
- Grounded on lesson content via retrieval (embeddings over explanation/summary/explanation-of-questions). Cites the lesson section it drew from.
- Will not reveal correct answers for an **in-progress exam**. Will explain freely after submission or in quizzes.
- Refuses off-curriculum requests politely and redirects.
- Every conversation is stored (§13) with the context bundle.

### 9.3 Limits

- Rate-limited per student per day (configurable; higher for paid).
- Model and prompt versions are recorded on every message for later evaluation.

---

## 10. Admin experience

### 10.1 Content management

- CRUD for Subjects, Units, Lessons with ordering (drag to reorder).
- Lesson editor: three rich-text areas (Explanation, Objectives as list, Summary), image upload, LaTeX support, video embed URL.
- Question editor per type, with live preview of the student view and a "test answer" box that runs the real grader.
- Bulk import of questions via spreadsheet template (v1: deterministic types only). Imported questions enter as Pending.
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

All charts filterable by date range and subject.

### 10.4 User management

- Students: search, view profile and progress, suspend, grant complimentary subscription.
- Teachers: invite, assign/unassign subjects, deactivate.
- Admins: invite, deactivate. At least one active admin must always remain.

---

## 11. Subscriptions and payments

### 11.1 Plans

| Plan | Includes |
|---|---|
| Free | Browse tree; first lesson of each unit; 10 quiz questions/day; Avatar 5 messages/day |
| Base (monthly / termly / yearly) | Unlimited quizzes and exams; full progress; Avatar with higher limit |
| Ask a Teacher (add-on, monthly) | Requires Base. N questions/month (configurable, e.g. 20) with 24-hour reply SLA |

### 11.2 Paymob integration

- Card and mobile wallet via Paymob checkout.
- Subscription state is driven **only by Paymob webhooks** (HMAC-verified). The client never sets entitlement.
- States: Trialing (if used) · Active · PastDue · Cancelled · Expired.
- Grace period on failed renewal: 3 days, then downgrade to Free.
- Full transaction log for the admin dashboard; refunds initiated by admin, recorded locally, executed in Paymob.

---

## 12. Ask a Teacher

### 12.1 Flow

1. Student (with add-on) opens "اسأل معلّم" from a lesson or question. Context (subject/unit/lesson/question) is attached automatically; student writes text and may attach an image (e.g. a photo of their work).
2. The thread is routed to the queue of teachers assigned to that subject. First teacher to claim it owns it.
3. SLA clock starts at submission. Reminders to the teacher at 12h and 20h; admin alert on breach.
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

Exports (admin only): JSONL per source, date-ranged, with PII stripped.

---

## 14. Non-functional requirements

| Area | Requirement |
|---|---|
| Language | Arabic UI, RTL throughout. English only for admin technical fields if needed. |
| Devices | Mobile-first responsive web. All interactions touch-friendly. v2 canvas and math input must work on phones. |
| Performance | Lesson page < 2s on 3G-class connections; quiz question transition < 300ms (prefetch next). |
| Availability | 99.5% monthly. Exam sessions auto-save every answer; a refresh resumes the session. |
| Security | Role-based authorisation on every endpoint; teacher subject scoping enforced server-side; Paymob webhooks HMAC-verified; rate limits on auth and Avatar. |
| Privacy | Students identified to teachers by display name only. Training exports strip PII. |
| Auditability | All content changes and validation decisions logged with actor and timestamp. |
| Accessibility | Readable font sizes, sufficient contrast, keyboard-navigable quizzes. |

---

## 15. Data model (entities and key fields)

```
User(id, role[Student|Teacher|Admin], phone, email, display_name, status)
TeacherSubject(teacher_id, subject_id)

Subject(id, name, order, default_blueprint_id?)
Unit(id, subject_id, name, order, blueprint_id?)
Lesson(id, unit_id, name, order, state[Draft|Published|Archived],
       explanation, summary, video_url?, published_at)
LessonObjective(id, lesson_id, text, order)

Question(id, lesson_id, subject_id, type, stem, difficulty, objective_id?, tags[],
         body_json, grading_spec_json, explanation, max_score, version,
         validation_status, validated_by?, validated_at?, rejection_reason?,
         retired_at?)
QuestionRevision(question_id, version, snapshot_json, edited_by, edited_at)

ExamBlueprint(id, name, type_counts_json, difficulty_mix_json?, time_limit_min?, pass_mark)

Session(id, student_id, kind[Quiz|UnitExam|MultiUnitExam], scope_json, scope_key, is_test_mode,
        started_at, last_activity_at, submitted_at?, score_pct?, time_limit_min?)
SessionItem(id, session_id, position, question_id, question_version, max_score)  -- the questions served, fixed at start
Attempt(id, session_id, student_id, question_id, question_version,
        answer_json, score, normalised_score, graded_by[Auto|AI|Teacher],
        grade_json?, time_taken_ms, created_at)  -- append-only
QuestionMastery(student_id, question_id, mastered bool, latest_attempt_id, latest_normalised_score, latest_attempted_at, previous_attempt_id?, previous_normalised_score?, previous_attempted_at?, updated_at)  -- materialised from the two most recent attempts (docs/mastery.md)

Subscription(id, student_id, plan, status, current_period_end, paymob_ref)
Payment(id, subscription_id, amount, currency, status, paymob_txn_id, raw_webhook_json, created_at)

TeacherThread(id, student_id, teacher_id?, subject_id, context_json, status,
              submitted_at, sla_due_at, closed_at, rating?)
TeacherMessage(id, thread_id, sender_id, kind[Text|Voice], text, audio_url?, transcript_final bool, created_at)

AvatarConversation(id, student_id, context_json, model, prompt_version, started_at)
AvatarMessage(id, conversation_id, role, text, created_at)

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
| Override AI grade (v2) | – | ✓ (assigned subjects) | ✓ |
| View own progress | ✓ | – | – |
| View any student's progress | – | – | ✓ |
| Manage own subscription | ✓ | – | – |
| Dashboards / finance | – | own stats only | ✓ |
| Manage users / teachers | – | – | ✓ |
| View audit log | – | – | ✓ |
| Export training data | – | – | ✓ |

Admins deliberately cannot approve questions. This keeps the "validated by a real teacher" claim true.

---

## 17. Key business rules (single list, for implementation reference)

1. Servable = Approved ∧ Lesson Published ∧ not retired. Derived, never stored.
2. Content edit on an Approved question → Pending, version + 1. Historical attempts keep the old version.
3. Only a Teacher assigned to the subject may validate. Admins cannot.
4. Rejection requires a reason.
5. Quiz selection order: unseen → last wrong → correct once → rest (least-recent first).
6. Mastery = normalised score ≥ 0.8 on the two most recent attempts.
7. Headline counter = platform servable total − student mastered.
8. Exams are generated from blueprints; a blueprint cannot be saved with a shortfall.
9. Exam retakes unlimited; best score displayed; all kept.
10. Avatar never reveals answers during an in-progress exam.
11. Ask a Teacher: 24h SLA from submission; one follow-up per thread; voice always transcribed; training record is text only.
12. Subscription entitlement changes only via verified Paymob webhooks.
13. Every content change and validation decision is audit-logged.

---

## 18. Recommended technical shape (for the implementation workflow to adopt or adjust)

- **Backend**: .NET 10, DDD/CQRS, PostgreSQL (JSONB for question bodies; pgvector for Avatar retrieval).
- **AI/grading service**: Python FastAPI — LLM grading (v2), SymPy CAS checks (v2), Avatar retrieval + generation (v1), transcription orchestration (v1).
- **LLM**: Claude API. Confirm current model IDs and pricing at build time.
- **Frontend**: React + TypeScript, shadcn/ui, i18next RTL. v2 adds a math input with LaTeX preview, a drag-and-drop canvas, and a rich Arabic editor.
- **Media**: S3-compatible object storage for images and audio.
- **Jobs / realtime**: background jobs for grading, transcription, SLA reminders; SignalR for grade results and teacher replies.
- **Payments**: Paymob, webhook-driven.

---

## 19. Open questions (do not block v1 start, but need answers before the related feature is built)

1. Exact plan pricing and the Ask a Teacher monthly quota.
2. Whether teachers are paid per reply / per validation (affects the teacher stats card).
3. Multi-select partial-credit formula: on or off by default?
4. Should unit-exam access be gated on opening all lessons? (Default: no gate.)
5. Avatar daily limits for Free vs Base.
6. Transcription provider for Arabic voice (evaluate quality on Egyptian dialect before committing).
7. v2: essay rubric format — free criteria list, or a fixed platform-wide template?

---

## 20. Glossary

| Term | Meaning |
|---|---|
| Servable | A question that may be shown to students right now |
| Attempt | One answer to one question |
| Mastered | Correct on the last two attempts |
| Blueprint | Per-unit recipe of question counts per type for an exam |
| Context bundle | The subject/unit/lesson/question ids attached to an Avatar or teacher thread |
