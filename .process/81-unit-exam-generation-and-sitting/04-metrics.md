# Metrics — [E6.S2] Unit exam generation and sitting

Branch: `feature/81-unit-exam-generation-and-sitting` (base `main`)

| # | Stage | Agent | Model | Started | Finished | Duration | Tokens | Tool uses | Outcome |
|---|-------|-------|-------|---------|----------|----------|--------|-----------|---------|
| 1 | Plan | feature-planner | opus-5.5 medium | 21:06 | 21:30 | 24m 2s | 356,114 | 77 | plan written |
| 2 | Implement | feature-implementer | opus-5.5 medium | 21:30 | 22:15 | 44m 28s | 554,369 | 279 | done, api 1781/1781, web 606/606 |
| 3 | Review r1 | feature-reviewer | opus-5.5 medium | 22:15 | 22:24 | 9m 10s | 257,324 | 107 | APPROVED (3 notes promoted to fixes) |
| 4 | Hardening r2 | feature-implementer | opus-5.5 medium | 22:24 | 22:38 | 13m 37s | 95,389 | 79 | 3 fixes, api 1786, web 609 |
| 5 | Review r2 | feature-reviewer | opus-5.5 medium | 22:38 | 22:42 | 4m 24s | 56,093 | 22 | CHANGES_REQUESTED (test gap) |
| 6 | Test fix r3 | feature-implementer | opus-5.5 medium | 22:43 | 22:45 | 2m 8s | 31,742 | 20 | starvation test pinned |
