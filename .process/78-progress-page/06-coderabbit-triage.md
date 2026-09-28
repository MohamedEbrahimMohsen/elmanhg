# CodeRabbit triage — PR #174

- RC1 (out-of-range `page` hides pagination): **fix**. Verified: `SessionHistorySection.tsx` renders the empty state whenever items are empty, regardless of `totalPages`. Fix: reset to page 1 in an effect, with a test.
