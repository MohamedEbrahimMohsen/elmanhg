# Acceptance — [E18.S2] Configurable SLA calendar with exam periods

- Date: 2026-10-02 18:33
- Story: #254 (E18.S2)
- Mode: autopilot (parallel lane, worktree)
- Dev instruction (2026-09-27): "this session will be ran once to implement EVERYTHING in this github project https://github.com/users/MohamedEbrahimMohsen/projects/1 ... You will do the same cycle story after story ... till you finish without any interruption. don't stop untill you finalized the project, whatever you stuck in it, ignore it and generate an ouput report at the end + put them as open issues in GitHub." Followed by: "GO". Continued in a cloud session (2026-09-28): "this should be a very long session, with no stop till you finalize everything"; the dev approved full autopilot (per-story branch, PR, squash-merge on green CI) for that session.
- Dev (2026-09-29): run up to 4 independent stories in parallel.
- Auto-accepted under autopilot.
- Plan auto-approved under autopilot (2026-10-02 19:07). Accepted trade-off: the sweep writes TeacherThreads only after a calendar/settings change (rare 409 for a racing teacher action); docs/ask-teacher.md must be updated to match.
