# CodeRabbit rework r2 — #70 Arabic answer normalisation (PR #159)

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Replaced every non-ASCII character on the new test lines with a `\uXXXX` escape (uppercase hex, like the existing `\uD800` / `￾`). Arabic letters and marks are escaped too, so every row shows each code point. I did this with a Python script that writes the literal backslash escapes, not through the tool's JSON layer. No logic changes. | `api/Elmanhg.Tests/Domain/Questions/Grading/AnswerNormalizerTests.cs:167-170, :179, :183-188`; `api/Elmanhg.Tests/Domain/Questions/Grading/TextGraderTests.cs:150, :152` |

Line numbers: in TextGraderTests.cs the finding cited `:149` / `:151`. The affected lines are actually `:150` (`["أ"]`) and `:152` (`"ا‌ٔ"`). They are the same statements, one line lower.

Before and after, these lines have the same code points. Before the edit I dumped them with Python `ascii()`, and each one matches its escaped form exactly, e.g. `:170` = `"و﻿ٔ", "ؤ"` and `:188` = `"  Aـَ٢‏ "`. The new tests therefore still fail on the committed `AnswerNormalizer.cs`, as the 08 verify showed (10 failures).

## Deviations
None.

## Build & test
- `grep -nP '[\x{200B}-\x{200F}\x{202A}-\x{202E}\x{2066}-\x{2069}\x{FEFF}\x{061C}\x{0654}\x{0655}\x{0653}]' …` in the default locale gives `grep: character code point value in \x{} or \o{} is too large`. The shell locale is not UTF-8. I re-ran it under `env LC_ALL=C.UTF-8` and added `\x{2060}`. Hits: AnswerNormalizerTests.cs lines 108-110, 117-128 and 163 only. None of these is a line from this finding, and TextGraderTests.cs has no hits. None of the rewritten lines contains any non-ASCII character.
- `dotnet test api/ -c Release`: `Test run summary: Passed! total: 1156 failed: 0 succeeded: 1156 skipped: 0`.

## Notes for review
- The remaining raw characters come from earlier rounds and were outside this finding's scope, so I did not change them: `Normalize_DecomposedHamzaOrMadda_ComposesWithUnifyAlefOff` (108-110, decomposed hamza/madda), `Normalize_InvisibleControl_IsRemoved` (117-128, raw invisible `char` literals) and `Normalize_AllRulesOff_AppliesOnlyAlwaysOnSteps` (163, raw U+200F). They have the same readability risk. Escaping them is a mechanical follow-up if wanted.
- Nothing committed.
