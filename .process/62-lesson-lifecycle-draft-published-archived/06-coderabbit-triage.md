TRIAGE: 1 to implement, 0 rejected, 0 dev-decisions

# CodeRabbit triage — PR #145, [E2.S3] Lesson lifecycle (#62)

## IMPLEMENT

### 1. RC1: "Archive lesson" fails when the Lessons folder runs in order (Minor, IMPLEMENT)
**Where:** `postman/elmanhg.postman_collection.json:791-838`. "Unpublish lesson" is at 791-814 and "Archive lesson" is at 815-838.
**Verified:** The claim is correct. The folder runs Publish (767), Unpublish (791), Archive (815), Reorder (839), Delete (868). After Publish the lesson is Published. Unpublish moves it to Draft. Archive then hits `Lesson.Archive` at `api/Elmanhg.Domain/Lessons/Lesson.Lifecycle.cs:46-49`, which throws `LESSON_NOT_PUBLISHED` (400) for a Draft lesson, as plan D2 says it should. So the "status is 200" test at line 823 fails when the folder runs in order. The collection is the repo's runnable API contract, so a request that fails by construction is a real defect.
**Fix:** Move the whole "Archive lesson" item (lines 815-838) so it comes directly before "Unpublish lesson" (line 791). The order becomes Publish → Archive → Unpublish → Reorder → Delete. Change nothing else.
**Why the fix holds:**
- Publish: Draft → Published.
- Archive: Published → Archived (allowed, D2).
- Unpublish: Archived → Draft (allowed, D2; `Lesson.Lifecycle.cs:22-37` only refuses Draft).
- Reorder: `Lesson.MoveTo` (`api/Elmanhg.Domain/Lessons/Lesson.cs:75`) has no state guard.
- Delete: runs on a Draft lesson, so it passes the `LESSON_IS_PUBLISHED` guard at `Lesson.cs:94-97`.
No code, test or doc change is needed. The folder description text from plan row `postman/…` is unaffected.

## REJECTED

None.

## DEV-DECISION

None.

## Not actionable

- PC1 (review body): a summary of RC1 plus run metadata. It has no separate claim, so it needs no action beyond item 1.
