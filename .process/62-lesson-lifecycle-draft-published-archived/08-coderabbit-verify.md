VERDICT: APPROVED

# CodeRabbit verify — [E2.S3] Lesson lifecycle (#62)

## Item 1 (RC1) — resolved
- `postman/elmanhg.postman_collection.json:791-814` is now "Archive lesson" (POST `/api/lessons/{{lessonId}}/archive`), and `:815-838` is "Unpublish lesson" (POST `/unpublish`). The Lessons folder now runs Publish, Archive, Unpublish, Reorder, Delete.
- I checked the sequence against the domain code:
  - Publish: Draft to Published.
  - Archive: Published to Archived. `Lesson.Lifecycle.cs:41-49` only refuses Archived or Draft lessons.
  - Unpublish: Archived to Draft. `Lesson.Lifecycle.cs:24` only refuses Draft lessons.
  - Reorder: `Lesson.MoveTo` has no state guard.
  - Delete: the lesson is Draft, so it passes the `LessonIsPublished` guard in `Lesson.cs`.
  
  Every "status is 200" test in the folder can now pass when run in order.

## JSON validity
- `python json.load` on the collection prints `valid`.

## Nothing else changed
- `git status`: the only tracked file modified is `postman/elmanhg.postman_collection.json`. The only untracked files are the `.process/` notes.
- `git diff` is 6 insertions and 6 deletions, all inside the two swapped blocks: the names, `url.raw` and `url.path`. The event scripts, method, headers and response are the same in both blocks, so the diff is exactly a swap of their positions. No other request, variable or folder text changed.
- The CRLF warning comes from git autocrlf. It does not change the content.

## Blocking
None.
