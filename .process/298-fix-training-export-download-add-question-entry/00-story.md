# [E19.S7] Fix training export download; add question entry on Questions page

Issue: #298

As an admin I can download training exports, and I can find where to add questions from the Questions page.

The dev reported two problems on 2026-10-05:

**1. Bug: the training export download always fails** (`/admin/export`).
- **Symptom:** clicking «تنزيل» shows the toast «تعذّر تنزيل ملف التصدير. حدث خطأ غير متوقع. حاول مرة أخرى.»
- **Server side:** the API returns `200 application/x-ndjson` (logged at 03:56:48 on the demo).
- **Root cause:** in `web/src/shared/lib/http.ts`, `parse()` treats any `Content-Type` that contains `json` as JSON. `application/x-ndjson` contains "json", so:
  - a non-empty JSONL body goes through `JSON.parse` and throws (it holds many values);
  - an empty body (0 rows) returns `undefined`, and `downloadBlob(undefined)` throws.
- **Fix:** only `application/json` and `*/*+json` are parsed as JSON. Every other content type, including NDJSON, is returned as a Blob. A 0-row export still downloads an empty `.jsonl` file. Optionally, the list can also show «0 صف» clearly; it already shows 0 rows.
- **Tests:**
  - `http.ts` unit tests for `application/json`, `application/problem+json`, `application/x-ndjson` (non-empty and empty), and binary content;
  - the export download hook gets a Blob in both cases.

**2. Usability: no visible way to add questions from `/admin/questions`.**
- **Today:** «سؤال جديد» and «استيراد أسئلة» show only when the list is filtered by a lesson (`?lessonId=`), or on Content → lesson editor.
- **Change:** an always-visible «إضافة سؤال» button in the Questions page header. If the list is already filtered by a lesson, it goes straight to `/admin/question/new/$lessonId`. Otherwise it opens a small dialog that picks subject → unit → lesson (reuse an existing picker if there is one), with «سؤال جديد» and «استيراد من ملف» actions.
- **Design rule:** keep exactly one primary (mint) on the page; this button is the page's primary.
- **Docs:** the admin questions section of `docs/claude-design-prompt.md` and `docs/prototype.md`, and the PRD if it describes the entry points.

### Sub-tasks
- [ ] Fix the `http.ts` content-type handling, with tests
- [ ] «إضافة سؤال» button with the lesson-picker dialog, with tests
- [ ] Docs

