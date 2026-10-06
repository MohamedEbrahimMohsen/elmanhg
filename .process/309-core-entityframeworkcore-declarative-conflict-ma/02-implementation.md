# Implementation: Core.EntityFrameworkCore declarative conflict map and row-version convention (E21.S6)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.DDD/Entities/IVersioned.cs` | 6 | Marker interface, `uint Version { get; }` |
| `api/core-libraries/Core.EntityFrameworkCore/Conflicts/UniqueViolation.cs` | 3 | `sealed record UniqueViolation(string? ConstraintName, string? TableName)` |
| `api/core-libraries/Core.EntityFrameworkCore/Conflicts/ConflictMap.cs` | 53 | Fluent map: `MapConcurrency<T>`, `MapUniqueConstraint`, `MapUniqueTable`, `TryTranslate`. Concurrency rules are checked first, then the detector is called once, then the unique rules in order. Names are compared ordinally. |
| `api/core-libraries/Core.EntityFrameworkCore/Context/RowVersionConvention.cs` | 23 | `ApplyRowVersionConvention()`: root, non-owned, non-shared `IVersioned` types get `Version.IsRowVersion()` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.Conflicts.cs` | 54 | `public static ConflictMap Conflicts` with the 24 rules in plan order and the 2 WHY comments copied verbatim. Postgres `DetectUniqueViolation` lives here. |
| `api/Elmanhg.Tests/Core/Persistence/ConcurrencyFailures.cs` | 23 | Builds `DbUpdateConcurrencyException` from substituted `IUpdateEntry`s |
| `api/Elmanhg.Tests/Core/Persistence/CorePersistenceProbes.cs` | 54 | Probe types plus an Npgsql probe `DbContext` that never connects |
| `api/Elmanhg.Tests/Core/Persistence/ConflictMapTests.cs` | 166 | Tests #1–#11 |
| `api/Elmanhg.Tests/Core/Persistence/RowVersionConventionTests.cs` | 54 | Tests #12–#15 |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextConflictMapTests.cs` | 113 | Tests #16–#18 (11 + 14 + 2 rows) |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | The 21 catch blocks are now a single `catch (DbUpdateException exception) when (Conflicts.TryTranslate(exception, out var conflict)) { throw conflict; }`. The 7 `.IsRowVersion()` lines are deleted. `modelBuilder.ApplyRowVersionConvention();` is inserted after `ConfigureSlaCalendars`. 4 unused usings are removed (`Core.Errors`, `Elmanhg.Application.Exceptions`, `Npgsql`, `DomainErrorCodes`). The soft-delete filter method and `ConfigureTeacherVoiceDrafts` are untouched. |
| `AppDbContext.MathStepGrading.cs`, `.RuntimeSettings.cs`, `.SlaCalendars.cs`, `.TrainingExports.cs` | One `.IsRowVersion()` line deleted from each |
| 11 domain entities (Session, QuestionMastery, Subscription, Payment, TeacherThread, AvatarConversation, TrainingExport, EssayGrade, MathStepGrade, RuntimeSettingOverride, ExamPeriod) | `, IVersioned` added to the base list. `Version` declarations unchanged. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Test #19 `Model_VersionedEntities_MapVersionToXminRowVersion` added, plus its usings. Existing methods unchanged. |
| `docs/constitution.md` | Line 3 stack list: replaced as specified |
| `.claude/skills/dotnet-feature/SKILL.md` | Delta 1, the §6.3 row and the §6.8 bullets replaced as specified |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Test #19: "the set of entity CLR types whose `Version` property `IsConcurrencyToken`" | If a derived type of a versioned root were ever added, `FindProperty` on that type would return the inherited property and the root would be listed twice. | I used `FindDeclaredProperty(nameof(IVersioned.Version))` and took `DeclaringType.ClrType`. Today the result is identical: the same 11 types. |
| Probe context "with `DbSet<…> Alphas`, …" | The plan does not specify the property form. | I used `{ get; set; }` auto-properties, matching `AppDbContext`. |

Compile-time confirmations the plan asked for:
- The `PostgresException` named-argument form (`tableName:`, `constraintName:`) compiles, so the 18-positional-argument fallback was not needed.
- `Substitute.For<IUpdateEntry>()` with `ToEntityEntry()` returning `context.Entry(entity)` compiles under TreatWarningsAsErrors with no EF1001 warning. `DbUpdateException.Entries` returns the substituted entries' entities: tests #1–#5 and #16 pass.

## Build & test
- `dotnet build api/Elmanhg.slnx -c Release` → **Build succeeded.** The only warnings are pre-existing CS8618/CS8602 in `Core.Notifications`, `Core.OTP` and `Core.Validation`, none in touched files. The app projects have zero warnings.
- Targeted run (`dotnet test --project api/Elmanhg.Tests -c Release --no-build -- --filter-class …` for ConflictMapTests, RowVersionConventionTests, AppDbContextConflictMapTests, AppDbContextTests) → `total: 48, failed: 0, succeeded: 48`.
- `dotnet test api/ -c Release --no-build` (the CI command, Docker/Testcontainers) → **Passed! total: 5342, failed: 0, succeeded: 5342, skipped: 0**, duration 1m 06s. This includes `Model_Current_MatchesLatestMigrationSnapshot`, `GradeConcurrencyTests` and all the persistence regression guards.
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build` → `No changes have been made to the model since the last migration.`
- Checks: `grep IsRowVersion api/Elmanhg.Infrastructure/Data/Context` returns nothing. `grep -ri npgsql api/core-libraries` returns nothing. `CoreDbContext.cs` is unchanged. There are no `#<number>` references in new code. No migration, package, project or slnx change.

## Deferred
None.

## Notes for review
- `ConflictMapTests.cs` is 166 lines, over the ~100-line guide. It holds the 11 plan-mandated tests in one class, as the plan names it, and each test repeats the 4-line assertion block the plan requires.
- `FirstOrDefault` on the value-tuple lists returns `default` (`ErrorCode == null`) when nothing matches, which is how the finders produce `null`.
- The sed/python edits converted the touched files' line endings to LF in the working copy. They were restored to CRLF to match the `core.autocrlf=true` checkout, and `git diff --stat` shows only the intended lines.
