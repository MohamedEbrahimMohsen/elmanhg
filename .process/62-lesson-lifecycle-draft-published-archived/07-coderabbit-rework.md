# CodeRabbit rework — [E2.S3] Lesson lifecycle (#62)

| # | What I changed | File:line |
|---|---|---|
| 1 (RC1) | Moved the "Archive lesson" item unchanged so it sits directly before "Unpublish lesson". Lessons folder order is now Publish → Archive → Unpublish → Reorder → Delete. | `postman/elmanhg.postman_collection.json:791-838` (Archive 791-814, Unpublish 815-838) |

## Files modified
| Path | Change |
|---|---|
| `postman/elmanhg.postman_collection.json` | Swapped the two request blocks. No other edits. |

## Deviations
None.

## Build & test
- `python json.load` on the collection printed `valid`.
- Checked item order with grep: Publish 768, Archive 792, Unpublish 816, Reorder 840, Delete 869.
- I did not run `dotnet build` or `dotnet test` because no code changed. I did not run the Postman collection against a live API.

## Notes for review
The block contents are byte-identical to the originals, with the original line endings kept. Only their position changed.
