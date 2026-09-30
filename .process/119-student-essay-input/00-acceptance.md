# Acceptance — [E14.S3] Student essay input

- Date: 2026-09-30 09:47
- Story: #119 (E14.S3)
- Mode: autopilot
- Dev instruction (2026-09-27): "this session will be ran once to implement EVERYTHING in this github project https://github.com/users/MohamedEbrahimMohsen/projects/1 ... You will do the same cycle story after story ... till you finish without any interruption. don't stop untill you finalized the project, whatever you stuck in it, ignore it and generate an ouput report at the end + put them as open issues in GitHub." Followed by: "GO". Continued in a cloud session (2026-09-28): "this should be a very long session, with no stop till you finalize everything"; the dev approved full autopilot (per-story branch, PR, squash-merge on green CI) for that session.
- Auto-accepted under autopilot.

## Plan gate (autopilot)
APPROVED 2026-09-30 by orchestrator, with conditions: (1) the raw answer cap stays 4000 characters for every non-essay type; only Essay answers get the larger cap, enforced per type, not by raising the global limit. (2) On a concurrency retry the worker reuses the grade already stored and never calls the AI again. (3) Lane #122 changes the same serving/exam-submit paths in parallel; keep changes additive and localised to ease the later merge.
