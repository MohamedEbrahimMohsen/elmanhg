# Acceptance — [E12.S2] JSONL export

- Date: 2026-09-30 10:04
- Story: #110 (E12.S2)
- Mode: autopilot (parallel lane, worktree)
- Dev instruction (2026-09-27): "this session will be ran once to implement EVERYTHING in this github project https://github.com/users/MohamedEbrahimMohsen/projects/1 ... You will do the same cycle story after story ... till you finish without any interruption. don't stop untill you finalized the project, whatever you stuck in it, ignore it and generate an ouput report at the end + put them as open issues in GitHub." Followed by: "GO". Continued in a cloud session (2026-09-28): "this should be a very long session, with no stop till you finalize everything"; the dev approved full autopilot (per-story branch, PR, squash-merge on green CI) for that session.
- Dev (2026-09-29): run up to 4 independent stories in parallel.
- Auto-accepted under autopilot.

## Plan gate (autopilot)
APPROVED 2026-09-30 by orchestrator, with conditions: (1) no anonymous download. The download endpoint is admin-policied and audited, and streams the stored file through the API (the web fetches it with the admin JWT and saves the blob), so no bearer link can leak. (2) The separate EssayGradeTrainingRecords table is accepted. (3) Exported files carry a short retention (config, default 7 days) and a sweep deletes expired ones.
