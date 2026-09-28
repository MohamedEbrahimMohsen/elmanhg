# Metrics — [E4.S1] Arabic answer normalisation

Branch: `feature/70-arabic-answer-normalisation` (base `main`)

| # | Stage | Agent | Model | Started | Finished | Duration | Tokens | Tool uses | Outcome |
|---|-------|-------|-------|---------|----------|----------|--------|-----------|---------|
| 1 | Plan | feature-planner | Opus 5.5 | 08:45 | 08:56 | 11m 4s | 174,256 | 44 | plan written, 6 new + 26 modified; 8 per-question toggles, data migration, #151 normaliser items |
| 2 | Implement | feature-implementer | Opus 5.5 | 08:56 | 09:13 | 16m 45s | 195,732 | 86 | 7 new + 34 modified; api 1142/1142, web 397/397 |
| 3 | Review r1 | feature-reviewer | Opus 5.5 | 09:13 | 09:19 | 5m 53s | 107,593 | 38 | CHANGES_REQUESTED (2: U+FFFE NFC throw, literal constants not escapes) |
| 4 | Rework r2 | feature-implementer | Opus 5.5 | 09:19 | 09:23 | 4m 24s | 40,819 | 30 | U+FFFE dropped pre-NFC + 2 tests; escapes; api 1144/1144, web 397/397 |
| 5 | Review r2 | feature-reviewer | Opus 5.5 | 09:24 | 09:28 | 4m 41s | 65,414 | 31 | APPROVED |
| 6 | CodeRabbit | orchestrator | n/a | n/a | n/a | n/a | 1 actionable (Major: strip invisible controls before NFC) |
| 7 | Triage | feature-reviewer | Opus 5.5 | 09:38 | 09:40 | 2m 44s | 46,073 | 26 | 1 implement (RC1 confirmed by repro, wider than reported) |
| 8 | CR fix | feature-implementer | Opus 5.5 | 09:40 | 09:45 | 4m 46s | 36,819 | 23 | invisible controls dropped pre-NFC + 4 tests + doc order; api 1156/1156 |
| 9 | CR verify | feature-reviewer | Opus 5.5 | 09:45 | 09:49 | 4m 7s | 60,847 | 33 | CHANGES_REQUESTED (raw invisible chars in new tests, not escapes) |
| 10 | CR fix r2 | feature-implementer | Opus 5.5 | 09:50 | 09:51 | 1m 47s | 30,629 | 14 | new test strings escaped; api 1156/1156 |
| 11 | CR verify r2 | feature-reviewer | Opus 5.5 | n/a | n/a | n/a | APPROVED |
