# CodeRabbit comments — PR #154

Collected 2026-09-28 07:37. Review 5335295920 ("Actionable comments posted: 2"). Verbatim essentials.

## RC1 — `api/Elmanhg.Application/Questions/GetServableQuestionCount/GetServableQuestionCountHandler.cs:20` (🟡 Minor, Data Integrity & Integration)

**Coordinate in-flight fills with invalidation.**

`ServableQuestionCountInvalidationHandler` can remove the key while `GetServableQuestionCountHandler` is awaiting the repository. The fill can then write the old count back to the shared cache. The endpoint can return that count until expiration, which defaults to 60 seconds.

Add a shared singleton coordinator or generation-and-lock helper. Invalidation must advance the generation and remove the key under the same coordination mechanism. A fill must write only when its generation is still current. Keep the documented pre-commit consistency behavior unchanged.

This is localized to the two handlers, the cache helper, dependency injection, and their unit tests.

## RC2 — `PROGRESS.md:4` (🟡 Minor, Maintainability)

**Update the resume instruction to story `#67`.** The checkpoint now says `#66` has merged, and the remaining-story table starts with `#67`. This instruction still tells the next cloud session to resume at `#65`.
