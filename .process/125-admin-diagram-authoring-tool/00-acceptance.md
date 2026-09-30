# Acceptance — [E16.S1] Admin diagram authoring tool

- Date: 2026-09-30 17:35
- Story: #125 (E16.S1)
- Mode: autopilot
- Dev instruction (2026-09-27): "this session will be ran once to implement EVERYTHING in this github project https://github.com/users/MohamedEbrahimMohsen/projects/1 ... You will do the same cycle story after story ... till you finish without any interruption. don't stop untill you finalized the project, whatever you stuck in it, ignore it and generate an ouput report at the end + put them as open issues in GitHub." Followed by: "GO". Continued in a cloud session (2026-09-28): "this should be a very long session, with no stop till you finalize everything"; the dev approved full autopilot (per-story branch, PR, squash-merge on green CI) for that session.
- Auto-accepted under autopilot.

## Plan gate (autopilot)
APPROVED 2026-09-30 by orchestrator, with condition: store the diagram image as a storage key (not a URL) validated against the diagram storage prefix. The public URL is resolved at read time by the layer that knows PublicBaseUrl (the same pattern as other media), so no external host can ever be referenced. That rules out tracking pixels, which would leak student IPs.
