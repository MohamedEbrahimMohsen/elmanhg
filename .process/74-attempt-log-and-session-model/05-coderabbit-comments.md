# CodeRabbit comments — PR #165

Collected 2026-09-28 12:27. Review 5338574370 ("Actionable comments posted: 3"). Essentials verbatim.

## RC1 — `api/Elmanhg.Domain/Sessions/Session.Submission.cs:17` (🟠 Major, Data Integrity, Heavy lift)
**Serialize finishing with answer submission.** If an answer and finish both load an open session, `Submit` can calculate the score before the answer is saved. The answer can then save against the finished session. The resulting attempt is permanent, but `ScorePercent` omits it. The per-question unique index does not prevent this race, and the `Session` mapping has no concurrency token. Add a session-level concurrency check or lock, then reload and resolve the losing operation before saving the final score.

## RC2 — `api/Elmanhg.Domain/Sessions/Session.Answering.cs:16` (🟠 Major, Data Integrity, Quick win)
**Require the item to belong to this session.** `RecordAttempt` accepts any `SessionItem`. If a caller passes an item from another session, `Attempt.Create` stores that item's question and version under this session's ID. Check the item's `SessionId` and membership in `Items` before the replay lookup.

## RC3 — `api/Elmanhg.Application/Shared/Options/SessionsOptions.cs:9-16` (🟡 Minor, Functional Correctness)
**Validate `MinQuizSize <= DefaultQuizSize <= MaxQuizSize` at startup.** Per-property `[Range(1,100)]` only; `Min=15, Max=10` passes `ValidateOnStart`. Proposed: `.Validate(x => x.MinQuizSize <= x.DefaultQuizSize && x.DefaultQuizSize <= x.MaxQuizSize, "...")` on the options registration.
