# Acceptance — [E15.S2] CAS final answer check

- Date: 2026-09-30 09:17
- Story: #122 (E15.S2)
- Mode: autopilot (parallel lane, worktree)
- Dev instruction (2026-09-27): "this session will be ran once to implement EVERYTHING in this github project https://github.com/users/MohamedEbrahimMohsen/projects/1 ... You will do the same cycle story after story ... till you finish without any interruption. don't stop untill you finalized the project, whatever you stuck in it, ignore it and generate an ouput report at the end + put them as open issues in GitHub." Followed by: "GO". Continued in a cloud session (2026-09-28): "this should be a very long session, with no stop till you finalize everything"; the dev approved full autopilot (per-story branch, PR, squash-merge on green CI) for that session.
- Dev (2026-09-29): run up to 4 independent stories in parallel.
- Auto-accepted under autopilot.

## Plan gate (autopilot)
APPROVED 2026-09-30 by orchestrator, with conditions: (1) only MathSteps questions may call the ai service; every other question type grades locally exactly as today, with no new ai dependency. (2) An ai outage must never lose a student answer or block an exam submit: store the MathSteps answer and mark its CAS result unchecked/pending (retried by a sweep or shown as under review), instead of returning 503 and saving nothing. (3) Merge origin/main (which now has #118 and #104) before starting.
