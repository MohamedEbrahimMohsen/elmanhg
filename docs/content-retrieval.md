# Lesson content retrieval

## Role

The AI avatar answers "grounded on lesson content via retrieval (embeddings over explanation/summary/explanation-of-questions)" (PRD §9.2). This feature keeps a vector index of every Published lesson and returns the chunks of one lesson that best match a text, each with a section reference the avatar can cite.

- The .NET API owns the chunks, the vectors (PostgreSQL `pgvector`) and the search. The AI service only turns text into vectors (`POST /v1/embeddings`, [ai-service.md](ai-service.md)).
- The consumer is the avatar chat (#91), which sends `SearchLessonContentQuery` in-process after it has checked the student's access. The query is the student's message (for the question entry points, the question stem, a new line, then the message), with `includeQuestionExplanations: true` and the default `top`. The avatar then drops the chunks of every question whose explanation the student cannot see yet (an unanswered item of an open quiz, or an item of an unsubmitted exam), so a question's explanation never reaches the student before the quiz reveals it. The remaining matches go to the AI service as citable sources, and the lesson explanation and summary are sent in full (as plain text) only when there are no matches, for example when a lesson has no index yet. See [avatar.md](avatar.md).
- Admins can run the same search over HTTP to check grounding, and can force a full re-index.

## What is indexed

Only **Published** lessons, and only **servable** questions of those lessons (`ServableQuestionSpecification`: Approved, not retired, lesson Published). Each lesson becomes a list of chunks, in this order:

| Section | Source | Chunking |
|---|---|---|
| `Explanation` | `Lesson.Explanation` (sanitised HTML) | An `h2` or `h3` heading closes the current chunk and becomes the `sectionTitle` of the chunks after it. |
| `Objectives` | the lesson objectives in `Order` | One numbered list (`1. …`, `2. …`), split only if it is too long. |
| `Summary` | `Lesson.Summary` | Same as the explanation. |
| `QuestionExplanation` | the stem and then the explanation of each servable question, oldest first | One chunk group per question, tagged with `questionId` and the `questionVersion` that was read. Headings are kept as text. |

Rules:

- HTML is turned into plain blocks first: headings, paragraphs, one block per list item (`- item` or `3. item`, honouring `<ol start>`), `$latex$` for inline math and `$$latex$$` for block math, image `alt` text, and `<br>` as a space. Whitespace is collapsed and empty blocks are dropped.
- Blocks are joined with a newline into a chunk until the next block would pass `ContentRetrieval:ChunkMaxCharacters` (1500). A single block longer than that is split at the last space before the limit, or cut at the limit when it has no space.
- A section title longer than 200 characters is truncated. The text sent to the embedder is `sectionTitle + "\n" + content` when there is a title; the stored `content` stays clean for citation.
- `position` counts from 1 within each section, and within each question.

## Section references

`reference` is how the avatar cites a chunk:

| Section | Reference |
|---|---|
| `Explanation` | `explanation-<position>` |
| `Objectives` | `objectives-<position>` |
| `Summary` | `summary-<position>` |
| `QuestionExplanation` | `question-<questionId>-<position>` |

## Freshness

There are no event handlers. Each lesson has one `LessonContentIndex` row that stores the two stamps the indexer read:

- `sourceUpdatedAt`: the lesson's `UpdationDate`.
- `questionsUpdatedAt`: the newest `UpdationDate` among the lesson's servable questions (null when it has none).

A lesson is **stale** when:

- it is Published and has no index row, or either stamp differs from the current database value; or
- it has an index row but is no longer Published (unpublished, archived or deleted). Its re-index removes the chunks and the row.

Publish, content edits, reorder, question approval and question edits stamp `UpdationDate`, so each of them makes the lesson stale. A retirement, or an edit that sends an Approved question back to Pending, takes the question out of the servable set; that changes `questionsUpdatedAt` only when it was the newest servable question, so otherwise its chunks stay in the table until the lesson's next re-index. They are never returned in the meantime: the search checks servability at query time (see Retrieval). Because the stored stamp is the value that was read, a change that commits while the indexer is reading is caught by the next sweep; no clock comparison is involved.

`LessonContentIndexWorker` sweeps every `IndexSweepIntervalSeconds` (30 s). It lists up to `IndexSweepBatchSize` (20) stale lessons, oldest change first, and re-indexes each one in its own scope. A lesson whose re-index fails is logged as a warning and left out of later batches until a sweep reaches the end of the backlog, so one failing lesson cannot block the rest. Existing Published lessons are indexed by the first sweeps after deployment.

Re-indexing a lesson embeds all its chunks in batches of `EmbeddingBatchSize` (32), then deletes the old chunks and inserts the new ones in one save. Nothing is written when an embeddings call fails. When the lesson is no longer Published (unpublished, archived or deleted), the re-index deletes its chunks and index row instead.

Re-indexing writes derived data, so it is not audited.

## Retrieval

`SearchLessonContentQuery(LessonId, Query, Top?, IncludeQuestionExplanations = true)` returns `LessonContentSearchResult { lessonId, indexedAt, matches[] }`:

1. The lesson must exist and be Published, otherwise `404 LESSON_NOT_FOUND`.
2. A lesson with no index row, or with zero chunks, returns `indexedAt` (null when there is no row) and no matches. The AI service is not called.
3. The trimmed query is embedded as `query`. A vector that is not 1536 wide gives `503 AI_SERVICE_UNAVAILABLE`.
4. The search runs in SQL on that one lesson. A chunk is returned only when its `embeddingModel` equals the query's model, and, for a question chunk, when the question is still servable **at the chunk's `questionVersion`**. So a retirement, an edit back to Pending, or an unpublish hides content at once, before the next sweep. `includeQuestionExplanations: false` drops question chunks.
5. Chunks are ordered by cosine distance (then id) and the first `top` are returned (`DefaultTopK` 5, at most `MaxTopK` 20). `score = 1 − distance`.

Each match has `chunkId`, `section`, `sectionTitle`, `position`, `questionId`, `reference`, `content` and `score`.

## HTTP (admin only)

Both endpoints need `Content.Manage` (Admin). Question chunks contain answers, so a student-callable search would leak exam answers (PRD §17 rule 10); students reach retrieval only through the avatar.

`POST /api/lessons/{lessonId}/content-chunks/search`

```json
{ "query": "قانون أوم", "top": 5, "includeQuestionExplanations": true }
```

```json
{
  "lessonId": "2b3c4d5e-6f7a-4b2c-8d3e-4f5a6b7c8d9e",
  "indexedAt": "2026-09-29T10:00:00+00:00",
  "matches": [
    {
      "chunkId": "8a0f…",
      "section": "Explanation",
      "sectionTitle": "قانون أوم",
      "position": 1,
      "questionId": null,
      "reference": "explanation-1",
      "content": "شدة التيار تتناسب طرديا مع فرق الجهد.",
      "score": 0.82
    }
  ]
}
```

| Code | Status | When |
|---|---|---|
| `LESSON_ID_REQUIRED` | 422 | empty lesson id |
| `CONTENT_SEARCH_QUERY_REQUIRED` | 422 | blank `query` |
| `CONTENT_SEARCH_QUERY_TOO_LONG` | 422 | `query` longer than `QueryMaxLength` (2000) |
| `CONTENT_SEARCH_TOP_INVALID` | 422 | `top` outside 1 to `MaxTopK` |
| `LESSON_NOT_FOUND` | 404 | unknown, Draft or Archived lesson |
| `AI_SERVICE_UNAVAILABLE` | 503 | the embeddings call failed or returned a bad vector |

`CONTENT_EMBEDDING_DIMENSIONS_INVALID` is a domain guard on chunk creation; it can only surface in the sweep, which logs it and retries the lesson later.

## Rebuild

`POST /api/content-index/rebuild` deletes every `LessonContentIndex` row, so the sweep treats every Published lesson as stale and re-embeds it. Use it after changing the embedding provider or model (for example going live with OpenAI). Until a lesson is re-embedded, its search returns no matches, because old-model chunks are never compared with a new-model query. The old chunks are replaced lesson by lesson.

The rebuild is audited as `ContentIndex.Rebuild` with no resource id and no diff ([audit-log.md](audit-log.md)).

## Configuration

`ContentRetrieval` section (environment form `ContentRetrieval__*`), validated at startup:

| Key | Default | Range | Notes |
|---|---|---|---|
| `IndexSweepEnabled` | `true` | | On by default in code; the test host turns it off. |
| `IndexSweepIntervalSeconds` | 30 | 5 to 86400 | |
| `IndexSweepBatchSize` | 20 | 1 to 500 | stale lessons per sweep |
| `ChunkMaxCharacters` | 1500 | 200 to 6000 | 6000 plus a 200-character title stays under the AI service's 8000-character text limit |
| `EmbeddingBatchSize` | 32 | 1 to 64 | texts per embeddings call; at most `ELMANHG_AI_EMBEDDING_MAX_TEXTS` |
| `DefaultTopK` | 5 | 1 to 50 | must not exceed `MaxTopK` |
| `MaxTopK` | 20 | 1 to 50 | |
| `QueryMaxLength` | 2000 | 1 to 4000 | |

The vector width, 1536, is a schema invariant (`vector(1536)`), not a setting. Changing it needs a migration and a rebuild.

## Fakes

With the defaults (`AiService:Provider=Fake`), the API embeds in-process with lexical feature hashing over the `AnswerNormalizer` output (model `fake`); the Python service's fake uses the same idea with model `fake-embedding`. Texts that share words score higher, so search works offline and in tests. See [ai-service.md](ai-service.md#fakes).

## Limits

- No approximate-nearest-neighbour (HNSW) index: every search scans one lesson's chunks (tens to about a hundred rows), filtered by a B-tree index on `LessonId`.
- Retrieval is scoped to one lesson; there is no subject-wide or cross-lesson search.
- Between sweeps a lesson's chunks can lag behind an edit by up to one interval. Visibility is never stale: the query-time filters above always apply.
- The Arabic retrieval quality of the real model has not been measured yet (tracked as a follow-up).
