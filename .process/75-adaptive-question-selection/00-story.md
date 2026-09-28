# [E5.S2] Adaptive question selection

Issue: #75

As a student each quiz prefers unseen and previously wrong questions so that practice feels purposeful. PRD §7.2.

Epic: #73

### Sub-tasks
- [ ] Selector service implementing bucket order: unseen, last wrong, correct once, rest by least recent
- [ ] Random within bucket, no repeats within a session, shorter quiz when pool is small
- [ ] Query for a student's per-question attempt summary used by the selector
- [ ] Unit tests for bucket ordering with seeded randomness
