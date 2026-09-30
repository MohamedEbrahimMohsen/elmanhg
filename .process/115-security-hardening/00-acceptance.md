# Acceptance — [E13.S4] Security hardening

- Date: 2026-09-30 21:47
- Story: #115 (E13.S4)
- Mode: autopilot (parallel lane, worktree)
- Dev instruction (2026-09-27): "this session will be ran once to implement EVERYTHING in this github project https://github.com/users/MohamedEbrahimMohsen/projects/1 ... You will do the same cycle story after story ... till you finish without any interruption. don't stop untill you finalized the project, whatever you stuck in it, ignore it and generate an ouput report at the end + put them as open issues in GitHub." Followed by: "GO". Continued in a cloud session (2026-09-28): "this should be a very long session, with no stop till you finalize everything"; the dev approved full autopilot (per-story branch, PR, squash-merge on green CI) for that session.
- Dev (2026-09-29): run up to 4 independent stories in parallel.
- Auto-accepted under autopilot.

## Plan gate (autopilot)
APPROVED 2026-09-30 by orchestrator, with conditions: (1) do not defer refresh-token reuse detection (#137) or the pre-suspension token check (#240). A schema change or migration is fine, and both are security fixes that need no dev decision. (2) CSP must be checked against the built web app, including KaTeX fonts, SignalR websockets, the media and R2/S3 image origins, and any inline style or script the build emits. Document every allowed origin in docs/security.md and make the media origin config-driven.
