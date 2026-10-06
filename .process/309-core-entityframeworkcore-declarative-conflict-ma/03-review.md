VERDICT: APPROVED

# Review: Core.EntityFrameworkCore declarative conflict map and row-version convention (E21.S6, story 309), round 1

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Core/Persistence/ConflictMapTests.cs:1-166`: the file is 166 lines, over the ~100-line guide in skill section 1. The implementer disclosed this. It holds the 11 tests the plan requires, and each one repeats a 4-line assertion block.
- `api/Elmanhg.Tests/Core/Persistence/ConflictMapTests.cs`: no test covers the case where a concurrency rule matches and the detector would also report a mapped unique violation. That test would pin "concurrency wins, and the detector is not called" (plan Decision 4). The current code is correct (`ConflictMap.cs:30`, `??` short-circuits), but nothing locks that in. Worth adding later.
- `api/Elmanhg.Tests/Core/Persistence/ConflictMapTests.cs:51,79,145`: the fluent `ConflictMap` chains are on one line, but skill section 1 says "new line per operator". Inline builder chains already exist in other tests (for example `Core/Otp/OtpTests.cs`), so this is not a divergence from house practice.
- `api/Elmanhg.Tests/Core/Persistence/ConcurrencyFailures.cs:9` uses `params object[] entities`, and `AppDbContextConflictMapTests.cs:95` returns `object`. Skill section 1 bans `object` parameters, but this test helper mirrors `DbContext.Entry(object)` and the plan specified it. Acceptable.

## Verified
- **21 catch blocks, mapped 1:1.** I compared `git show origin/main:api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` (`SaveChangesAsync`, 9 concurrency catches and 12 unique catches) with `AppDbContext.Conflicts.cs:21-45`.
  - Concurrency: Session and QuestionMastery map to SESSION_MODIFIED_CONCURRENTLY, Payment, Subscription, TeacherThread, AvatarConversation, TrainingExport, then EssayGrade and MathStepGrade map to GRADE_MODIFIED_CONCURRENTLY, then RuntimeSettingOverride and ExamPeriod. The codes and order are identical. Each "or" pattern is split into adjacent rules with the same code, so first-match precedence is unchanged (for example, entries QuestionMastery plus Payment still give SESSION_MODIFIED_CONCURRENTLY).
  - Unique: RuntimeSettingKeyIndex, Paymob and RefundTransaction, the QuestionImportBatches table rule, InProgressSession, AttemptPerQuestion (DomainErrorCodes), QuestionMasteryPerStudent, OneOpenExam, LessonOpeningPerStudent, SubjectDefault and Unit blueprint, AvatarMessagePosition, TeacherThreadTrainingTrigger, EssayGradeTrainingTrigger. The codes and order are identical, and the 2 WHY comments are copied verbatim.
  - `ErrorCodes` resolves to `Elmanhg.Application.Exceptions.ErrorCodes`, as before.
- **Semantics preserved.**
  - The concurrency rules apply only to `DbUpdateConcurrencyException` (`ConflictMap.cs:38`). An unmatched concurrency exception falls through to the unique rules, as the old catches did, because it is a `DbUpdateException`.
  - The detector checks that `InnerException` is a PostgresException with SqlState UniqueViolation (`AppDbContext.Conflicts.cs:47-50`). Names are compared ordinally, the same as constant pattern matching.
  - An untranslated exception is not caught. The filter returns false, so the exception propagates unchanged with its original stack (`AppDbContext.cs:105`). `ConflictCoreException(code, innerException: exception)` is the same constructor call as before.
- **Static initialisation is safe.** Every index name the map uses is a `const` (`AppDbContext.RuntimeSettings.cs:8`, `AppDbContext.TrainingData.cs:12-13`, `AppDbContext.cs`), so the order of partial-file static initialisers does not matter.
- **Row versions.**
  - On origin/main, `IsRowVersion` appears on exactly 11 lines (7 in `AppDbContext.cs` and 1 in each of 4 partials). All 11 are deleted, and the only `IsRowVersion` left is in `RowVersionConvention.cs:20`.
  - All 11 entities implement `IVersioned`, with their `Version` declarations unchanged.
  - `ApplyRowVersionConvention()` is called after `ConfigureSlaCalendars` and before the soft-delete filter method (`AppDbContext.cs:143`).
  - I ran `dotnet ef migrations has-pending-model-changes` myself: "No changes have been made to the model since the last migration."
  - `Model_VersionedEntities_MapVersionToXminRowVersion` pins the set of exactly 11 types to `xmin`.
- **Core is app-agnostic.** `grep -ri npgsql api/core-libraries` finds nothing. There are no Elmanhg names in `Conflicts/`, `RowVersionConvention.cs` or `IVersioned.cs`. `CoreDbContext.cs` has no diff against origin/main. `IVersioned` is in `Core.DDD.Entities` (Decision 7).
- **No issue numbers in comments.** No hash-number reference appears in any new or changed code file.
- **Plan contract.** All 10 files to create exist with the planned signatures, and nothing extra was created. There are no csproj, slnx, package or migration changes. Both deviations in 02 are disclosed and harmless:
  - Test 19 uses `FindDeclaredProperty`, which is stricter.
  - The probe DbSets use auto-properties.
- **Style.** Namespaces are file-scoped. `ConflictMap` and the `UniqueViolation` record are `sealed`. LINQ chains put one operator per line (`RowVersionConvention.cs:10-16`). There is no try/catch in any handler, and the single catch is the one the plan specified.
- **Build and tests.**
  - `dotnet build api/Elmanhg.slnx -c Release` succeeded, with no warnings in the touched projects.
  - The targeted classes (ConflictMapTests, RowVersionConventionTests, AppDbContextConflictMapTests, AppDbContextTests, GradeConcurrencyTests) passed 50/50.
  - The full `dotnet test api/ -c Release` passed 5342/5342 with 0 skipped, which matches the claim in 02.
- **Existing tests untouched.** `AppDbContextTests.cs` only gains the one new method and its usings.
- **Docs sync.**
  - `docs/constitution.md:3` names `ConflictMap`, `IVersioned` and `ApplyRowVersionConvention()`.
  - `.claude/skills/dotnet-feature/SKILL.md` was updated in delta 1, the section 6.3 row and section 6.8. This also removed the stale `byte[] RowVersion` / `CoreExceptionMiddleware` guidance.
  - The feature docs that say a race is "mapped in `AppDbContext.SaveChangesAsync`" (`docs/ask-teacher.md:106`, `docs/exam-blueprints.md:66`, `docs/training-data.md:42`) are still true.
  - There is no divergence.
- **Postman.** The HTTP surface did not change, so no Postman update is needed.

## Test quality
- **ConflictMapTests** constrains the code. It catches:
  - a reversed precedence (tests 3 and 10);
  - a missing derived-type match (test 2);
  - a skipped fall-through to the unique rules (test 5);
  - case-insensitive matching (test 11);
  - a wrong inner exception (`BeSameAs`).
- **RowVersionConventionTests** constrains the code. It catches:
  - an owned type promoted to an entity, or given a row version (test 14);
  - a version applied to unversioned types (test 13);
  - a derived type being re-configured (test 15);
  - wrong `xmin`/`xid` mapping (test 12).
- **AppDbContextConflictMapTests** constrains the code.
  - Each of the 11 concurrency rows and 14 unique rows asserts the exact legacy code against the real static map, so a wrong code or a missing rule fails one row.
  - Test 18 pins the SqlState guard (a ForeignKeyViolation on a mapped index gives false) and the unmapped-index path.
  - Precedence between different codes for multi-entity failures is covered only at core level (test 3). That is acceptable, because the app map is a straight list.
- **AppDbContextTests.Model_VersionedEntities_MapVersionToXminRowVersion** constrains the code. It fails if any of the 11 entities loses its row version or an unexpected type gains one.
- The wiring of the single `catch` in `SaveChangesAsync` is still exercised end to end by the existing real-race tests (for example GradeConcurrencyTests), which pass.
- No vacuous tests were found.
