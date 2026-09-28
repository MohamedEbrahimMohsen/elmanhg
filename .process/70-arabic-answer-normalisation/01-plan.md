# Plan — Arabic answer normalisation (#70, E4.S1)

## Goal
An admin can switch each Arabic normalisation rule on or off per fill-in and text short-answer question: tashkeel, tatweel, alef forms, taa marbuta, alef maqsura, digits, whitespace and Latin case. The grader applies exactly the chosen rules, so Egyptian spelling variants (القاهره/القاهرة, مصطفي/مصطفى, احمد/أحمد, ٢٠٢٤/2024) stop producing false negatives. Input artefacts are also fixed (#151): decomposed hamza or madda, pasted bidi and zero-width marks, the Arabic comma and thousands separator, Persian ی, and exponent numbers. Every existing question keeps grading exactly as it does today.

## Scope
**In:**
- Extend the #65 normaliser. Do not create a parallel one.
- `AnswerNormalization`: an 8-rule record stored in the fill and text-short grading spec. It replaces `unifyLetterVariants`.
- A data migration for `Questions.GradingSpec` and `QuestionRevisions.Snapshot`.
- The always-on #151 fixes.
- Plain-decimal numeric parsing.
- Import: the `unify_letter_variants` column now maps onto the three letter rules.
- Web editor: an "Answer normalisation" group of 8 checkboxes.
- An Egyptian spelling corpus test.
- Docs.

**Out (the #151 items not about the normaliser stay open on #151):**
- The stale grade result in `QuestionPreviewPanel`.
- Tests for the `QuestionGrader` defensive throws.
- `GetTeachersHandlerTests` TH1.
- `"1,000"` still grades as 1 (plan D6 of #65, documented).
- Hamza-seat unification (ئ/ؤ/ء). The PRD does not list it; it is pinned as distinct by the corpus test.
- Feedback text (#71, #72).

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Where do the toggles live, and in what shape? | Replace `unifyLetterVariants` with a nested `normalization` object in the Fill and Short-text grading spec: `{"stripTashkeel","stripTatweel","unifyAlef","unifyTaaMarbuta","unifyAlefMaqsura","convertDigits","collapseWhitespace","foldCase"}`, all booleans. | The sub-task says "a toggle for each rule" and names alef, taa and maqsura separately. One object keeps the spec readable and has a single place to default. |
| D2 | Defaults | Every rule defaults to `true`. A missing object means all on; a partial object means the missing rules are on. Canonical storage always writes all 8. | PRD §6.2 says "default on". This mirrors the existing `partialCredit` and `unifyLetterVariants` canonical defaults. |
| D3 | Existing data | New EF migration `AddAnswerNormalizationRules`. It rewrites every `Questions.GradingSpec` and `QuestionRevisions.Snapshot->gradingSpec` that holds `unifyLetterVariants`: the three letter rules take the old flag (null means true), the other five are true, and the old key is removed. Numeric short specs have no such key and are untouched. `AuditLogs` diffs keep the old key (append-only trigger; history). There are no attempts yet, so nothing needs regrading. | Existing questions grade exactly as today. `docs/question-schemas.md` § "Changing a schema" requires rewriting old-shape rows. Without the rewrite, a stored `false` would silently read as all-on. Revisions are rewritten too, so snapshots stay readable by the same records. |
| D4 | Expand/contract? | A single step, pre-launch: one deploy of api and web together, jsonb content only. | There are no external clients. The only reader of the key is this API. |
| D5 | Legacy `unifyLetterVariants` in a request | Not read. It is dropped like any unknown property (existing `QuestionJson` behaviour), so all rules are on. | The web client changes in the same PR. There are no other clients. |
| D6 | Steps that always run, with no toggle | 1. Drop unpaired UTF-16 surrogates. 2. Unicode NFC (`NormalizationForm.FormC`). 3. Remove invisible controls: U+061C, U+200B–U+200F, U+202A–U+202E, U+2060, U+2066–U+2069, U+FEFF. 4. Map ، (U+060C) → `,`. 5. Map ی (U+06CC) → ي (U+064A). 6. Trim leading and trailing whitespace. | These are keyboard and paste artefacts, not spelling choices (#151). Step 1 exists because `string.Normalize` throws `ArgumentException` on a lone surrogate. U+202A–E and U+2060 are the same class as #151's list. ی → ي is character canonicalisation, independent of the maqsura toggle. Trimming is always on because accepted answers are trimmed at save, so an untrimmed answer could never match. |
| D7 | Meaning of `collapseWhitespace` off | Inner whitespace is kept verbatim (tabs and runs are not replaced). The ends are still trimmed (D6.6). | The PRD rule is "trim and collapse"; only collapsing is configurable. |
| D8 | Numeric parsing | This stays a fixed profile and ignores per-question rules. It uses `AnswerNormalization` with the three letter rules off and everything else on, exactly as today. After normalising: remove ٬ (U+066C), then map ٫ (U+066B) and `,` to `.` and `−` (U+2212) to `-`. Parse with `NumberStyles.AllowLeadingSign \| NumberStyles.AllowDecimalPoint`: no exponent, no whitespace, no thousands. | This aligns the code with the doc's "a plain decimal number" (#151). Exponents are rejected. ، reaches the parser as `,`, so it means a decimal point, consistent with plan D6 of #65. |
| D9 | Import template (#66) | Keep the single `unify_letter_variants` column: `true` or empty turns all three letter rules on, `false` turns all three off. The other five rules are always on for imported questions. No new columns. Update the help text and `docs/question-import.md`. | Existing templates keep working. Per-rule fine-tuning is rare and belongs in the editor, where the admin can see the effect in "جرّب الإجابة". |
| D10 | Editor UI | A new `NormalizationRulesField`: a `<fieldset>` with legend "Answer normalisation" / "تطبيع الإجابة", a hint line, and 8 `CheckboxField`s in a 2-column grid from `md`. It replaces the single unify checkbox in `FillBlanksField` and in the text branch of `ShortAnswerFields`. The `editor.blanks.unify` i18n key is deleted. | The prototype has no toggle UI. This reuses the existing `CheckboxField` and the legend styling from `FillBlanksField`. |
| D11 | Validation of `normalization` | No new error code. A non-object, or a non-boolean rule, fails deserialisation, which returns the existing `422 QUESTION_GRADING_SPEC_INVALID`. | `QuestionSchemaReader.TryRead` already maps `JsonException` to that code. |
| D12 | Code-point constants | Move all of them into a new `internal static class ArabicCharacters`, written as `'\uXXXX'` escapes (existing literals converted too). | Invisible controls cannot be written as literals. This keeps `AnswerNormalizer` under about 100 lines. It closes the #151 "not escapes" nit. |
| D13 | Where does the record live? | `Elmanhg.Domain.Questions.Schemas.AnswerNormalization`. | It is part of the grading spec schema, next to `FillGradingSpec` and `ShortGradingSpec`. |
| D14 | Teacher validation view | No change. It already shows the raw `gradingSpec` JSON, which now includes `normalization`. | — |
| D15 | Down migration | Reverse SQL: `unifyLetterVariants = normalization.unifyAlef` (default true); drop `normalization`. Not covered by a test. | No existing migration's `Down` is tested. Running it against the shared Testcontainers database would rewrite rows that other test classes are reading in parallel. |
| D16 | OpenAPI, Orval, Postman | No change. `gradingSpec` is `JsonElement` and no endpoint changes. The implementer confirms there is no drift with `npm --prefix web run gen:api`. | — |
| D17 | Morabh reuse | Nothing to reuse. `/home/user/apis` has no normaliser (searched for `NormalizationForm`, `tashkeel`, `Tatweel` and `Arabic`). Every piece is new, with no Morabh equivalent. | — |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs` | Rewrite (see Files to create A2). Remove the `bool` overload. |
| `api/Elmanhg.Domain/Questions/Grading/TextGrader.cs` | `Matches(string?, List<string>, AnswerNormalization)`; `GradeFill` passes `spec.Normalization ?? AnswerNormalization.Default`; `GradeShort` text branch passes `spec.Normalization ?? AnswerNormalization.Default`. Add `private const char ArabicThousandsSeparator = '٬';`, `private const NumberStyles PlainDecimal = NumberStyles.AllowLeadingSign \| NumberStyles.AllowDecimalPoint;` and `private static readonly AnswerNormalization NumberRules = new(UnifyAlef: false, UnifyTaaMarbuta: false, UnifyAlefMaqsura: false);`. Convert the existing `ArabicDecimalSeparator` and `MinusSign` to `'٫'` and `'−'`. `TryParseNumber` becomes `AnswerNormalizer.Normalize(text, NumberRules).Replace(ArabicThousandsSeparator.ToString(), string.Empty).Replace(ArabicDecimalSeparator, '.').Replace(',', '.').Replace(MinusSign, '-')` followed by `decimal.TryParse(candidate, PlainDecimal, CultureInfo.InvariantCulture, out value)`. Keep the existing WHY comment and extend it: `// Students type these separators on Arabic keyboards; ٬ groups thousands, the others mean the ASCII characters.` |
| `api/Elmanhg.Domain/Questions/Schemas/FillSchemas.cs` | `public sealed record FillGradingSpec(List<FillBlankAnswers>? Blanks, AnswerNormalization? Normalization = null);` |
| `api/Elmanhg.Domain/Questions/Schemas/ShortSchemas.cs` | `public sealed record ShortGradingSpec(decimal? Value, decimal? Tolerance, ToleranceMode? ToleranceMode, List<string>? AcceptedAnswers, AnswerNormalization? Normalization);` |
| `api/Elmanhg.Application/Questions/Shared/FillQuestionRules.cs` | `Normalize`: `new FillGradingSpec(answers, spec.Normalization ?? AnswerNormalization.Default)`. |
| `api/Elmanhg.Application/Questions/Shared/ShortQuestionRules.cs` | `Normalize` text branch: `new ShortGradingSpec(null, null, null, QuestionSchemaReader.TrimAnswers(spec.AcceptedAnswers), spec.Normalization ?? AnswerNormalization.Default)`. The numeric branch is unchanged (last argument `null`). |
| `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportBodies.cs` | Fill: `new FillGradingSpec(…, LetterVariants(cells.Boolean(UnifyLetterVariants) ?? true))`. Short text: `new ShortGradingSpec(null, null, null, cells.List(AcceptedAnswers), LetterVariants(cells.Boolean(UnifyLetterVariants) ?? true))`. Add `private static AnswerNormalization LetterVariants(bool unify) => new(UnifyAlef: unify, UnifyTaaMarbuta: unify, UnifyAlefMaqsura: unify);`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx` | `QUESTION_IMPORT_HELP_UNIFY_LETTER_VARIANTS` → `true or empty to treat أ إ آ ٱ as ا, ة as ه and ى as ي; false to keep them different. The other normalisation rules stay on; change them in the question editor.` |
| `api/Elmanhg.Api/Resources/Messages.ar.resx` | The same key → `true او فارغ لاعتبار أ إ آ ٱ مثل ا، و ة مثل ه، و ى مثل ي؛ و false للتفريق بينها. تبقى قواعد التطبيع الأخرى مفعّلة، ويمكن تغييرها من محرر السؤال.` |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | `Migrate_FreshDatabase_LeavesNoPendingMigrations`: append `thirteenth => thirteenth.Should().EndWith("_AddAnswerNormalizationRules")` (accepted pattern, PROGRESS.md). |
| `web/src/features/questions/api/questionOptions.ts` | Add `export const normalizationRules = ['stripTashkeel', 'stripTatweel', 'unifyAlef', 'unifyTaaMarbuta', 'unifyAlefMaqsura', 'convertDigits', 'collapseWhitespace', 'foldCase'] as const;` and `export type NormalizationRule = (typeof normalizationRules)[number];` |
| `web/src/features/questions/schemas/questionContentSchemas.ts` | Add `export const normalizationSchema = z.object({ stripTashkeel: z.boolean().optional(), stripTatweel: …, unifyAlef: …, unifyTaaMarbuta: …, unifyAlefMaqsura: …, convertDigits: …, collapseWhitespace: …, foldCase: z.boolean().optional() });` In `fillSpecSchema` and `shortTextSpecSchema`, replace `unifyLetterVariants: z.boolean().optional()` with `normalization: normalizationSchema.optional()`. |
| `web/src/features/questions/schemas/questionEditorSchema.ts` | Replace `unifyLetterVariants: z.boolean()` with `normalization: z.object({ stripTashkeel: z.boolean(), stripTatweel: z.boolean(), unifyAlef: z.boolean(), unifyTaaMarbuta: z.boolean(), unifyAlefMaqsura: z.boolean(), convertDigits: z.boolean(), collapseWhitespace: z.boolean(), foldCase: z.boolean() })`. |
| `web/src/features/questions/api/questionValues.ts` | Add `function readNormalization(stored: Partial<Record<NormalizationRule, boolean>> \| undefined): QuestionValues['normalization']`. It returns an explicit object literal with each of the 8 keys set to `stored?.<rule> ?? true`. `emptyQuestionValues`: `normalization: readNormalization(undefined)` (replacing `unifyLetterVariants: true`). `readFill`: `normalization: readNormalization(parsedSpec.data?.normalization)`. `readShort` text: `normalization: readNormalization(text.data.normalization)`. `toContent`: Fill and Short-text `gradingSpec` send `normalization: values.normalization` instead of `unifyLetterVariants`. |
| `web/src/features/questions/components/FillBlanksField.tsx` | Replace `<CheckboxField … name="unifyLetterVariants" …/>` with `<NormalizationRulesField />`. Drop the `CheckboxField` import. |
| `web/src/features/questions/components/ShortAnswerFields.tsx` | Same replacement inside the text branch. Drop the `CheckboxField` import. |
| `web/src/features/questions/i18n/en.json` | Delete `editor.blanks.unify`. Add `editor.normalization`: `legend` "Answer normalisation", `hint` "Applied to the student's answer and to every accepted answer before they are compared.", `stripTashkeel` "Ignore diacritics (tashkeel)", `stripTatweel` "Ignore tatweel (ـ)", `unifyAlef` "Treat أ إ آ ٱ as ا", `unifyTaaMarbuta` "Treat ة as ه", `unifyAlefMaqsura` "Treat ى as ي", `convertDigits` "Treat Arabic digits (٠–٩) as 0–9", `collapseWhitespace` "Ignore extra spaces", `foldCase` "Ignore Latin letter case". |
| `web/src/features/questions/i18n/ar.json` | Delete `editor.blanks.unify`. Add `editor.normalization`: `legend` "تطبيع الإجابة", `hint` "تُطبَّق على إجابة الطالب وعلى كل إجابة مقبولة قبل المقارنة.", `stripTashkeel` "تجاهل التشكيل", `stripTatweel` "تجاهل التطويل (ـ)", `unifyAlef` "اعتبار أ إ آ ٱ مثل ا", `unifyTaaMarbuta` "اعتبار ة مثل ه", `unifyAlefMaqsura` "اعتبار ى مثل ي", `convertDigits` "اعتبار الأرقام العربية (٠–٩) مثل 0–9", `collapseWhitespace` "تجاهل المسافات الزائدة", `foldCase` "تجاهل حالة الأحرف اللاتينية". |
| `docs/PRD.md` §6.2 | Replace the bullets with: "Each rule can be switched off per question; all are on by default: strip tashkeel; strip tatweel; unify أ إ آ ٱ → ا; ة → ه; ى → ي; convert Arabic-Indic digits to ASCII; collapse whitespace; case-fold Latin characters." Then: "Always applied: Unicode NFC, removal of invisible bidi and zero-width marks, ، → `,`, ی → ي, and trimming." Then: "Numeric answers ignore the per-question rules and must be a plain decimal (`docs/question-schemas.md`)." |
| `docs/question-schemas.md` | **Fill** and **Short text** spec examples: `"normalization":{"stripTashkeel":true,"stripTatweel":true,"unifyAlef":true,"unifyTaaMarbuta":true,"unifyAlefMaqsura":true,"convertDigits":true,"collapseWhitespace":true,"foldCase":true}` replaces `"unifyLetterVariants":true`. Replace the `unifyLetterVariants` paragraph with the 8-rule table (key · effect · default `true`), plus the partial-object and missing-object rule (D2). Canonical storage: "defaults are written out (`partialCredit`, all eight `normalization` rules)"; "text short specs keep only `acceptedAnswers` and `normalization`". Rules table: add a row `Fill, Short text \| normalization, when present, is an object of booleans \| QUESTION_GRADING_SPEC_INVALID`. Grading → **Normalisation**: rewrite as the D6 always-on steps in order, then the 8 toggleable rules with their code points (existing ranges), plus D7. **Numeric parsing**: D8 text, including "exponents such as `9.8e0` do not parse". Add a "Migration" bullet under Canonical storage covering D3, including that audit diffs keep the old key. |
| `docs/question-import.md` | Mapping rows for Fill and Short text: "`unify_letter_variants` empty or `true` turns the three letter rules (`unifyAlef`, `unifyTaaMarbuta`, `unifyAlefMaqsura`) on, `false` turns them off; the other normalisation rules are always on for imported questions (change them in the editor)". |
| `docs/claude-design-prompt.md` | §4 line for `#/admin/question/:id`: add "fill-in and text short answers show an «تطبيع الإجابة» group of eight rule checkboxes (all on by default)". §5 rule 13: append "each rule can be switched off per question". |
| `docs/prototype.md` | Admin bullet (line 42): add "Fill-in and text short answers have per-rule answer-normalisation checkboxes, all on by default." |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `api/Elmanhg.Domain/Questions/Schemas/AnswerNormalization.cs` | `sealed record` | `namespace Elmanhg.Domain.Questions.Schemas;` `public sealed record AnswerNormalization(bool StripTashkeel = true, bool StripTatweel = true, bool UnifyAlef = true, bool UnifyTaaMarbuta = true, bool UnifyAlefMaqsura = true, bool ConvertDigits = true, bool CollapseWhitespace = true, bool FoldCase = true)` with body `{ public static AnswerNormalization Default { get; } = new(); }`. STJ fills missing JSON properties from the parameter defaults. The serialised key order is the parameter order. |
| A2 | `api/Elmanhg.Domain/Questions/Grading/AnswerNormalizer.cs` (rewrite) | `public static class` | `public static string Normalize(string? text, AnswerNormalization rules)`. Steps: (1) null or empty → `string.Empty`; (2) `var composed = DropUnpairedSurrogates(text).Normalize(NormalizationForm.FormC);` (3) loop over `composed` with `StringBuilder builder` and `bool pendingSpace`: `if (IsDropped(character, rules)) continue;` if `char.IsWhiteSpace(character)` then, when `rules.CollapseWhitespace`, set `pendingSpace = builder.Length > 0` and `continue`, otherwise `builder.Append(character)` and `continue`; if `pendingSpace`, append `' '` and reset it; `var mapped = ArabicCharacters.Map(character, rules);` then `builder.Append(rules.FoldCase ? char.ToLowerInvariant(mapped) : mapped)`; (4) `return builder.ToString().Trim();`. `private static bool IsDropped(char character, AnswerNormalization rules) => ArabicCharacters.IsInvisibleControl(character) \|\| (rules.StripTashkeel && ArabicCharacters.IsTashkeel(character)) \|\| (rules.StripTatweel && character == ArabicCharacters.Tatweel);` `private static string DropUnpairedSurrogates(string text)`: if there is no `char.IsSurrogate` character, return `text`; otherwise rebuild, keeping a high+low pair together and skipping any lone surrogate. Add one WHY comment on it: `// string.Normalize throws on a lone surrogate; a pasted answer must never fail grading.` |
| A3 | `api/Elmanhg.Domain/Questions/Grading/ArabicCharacters.cs` | `internal static class` | `// PRD §6.2 character classes; Unicode code points are fixed invariants.` `internal const char` escapes: `Tatweel='ـ'`, `FathatanFirst='ً'`, `DiacriticLast='ٟ'`, `SuperscriptAlef='ٰ'`, `QuranicMarkFirst='ۖ'`, `QuranicMarkLast='ۭ'`, `ArabicIndicZero='٠'`, `ArabicIndicNine='٩'`, `ExtendedZero='۰'`, `ExtendedNine='۹'`, `Alef='ا'`, `AlefHamzaAbove='أ'`, `AlefHamzaBelow='إ'`, `AlefMadda='آ'`, `AlefWasla='ٱ'`, `TaaMarbuta='ة'`, `Haa='ه'`, `AlefMaqsura='ى'`, `Yaa='ي'`, `FarsiYeh='ی'`, `ArabicComma='،'`, `ArabicLetterMark='؜'`, `ZeroWidthSpace='​'`, `RightToLeftMark='‏'`, `LeftToRightEmbedding='‪'`, `RightToLeftOverride='‮'`, `WordJoiner='⁠'`, `LeftToRightIsolate='⁦'`, `PopDirectionalIsolate='⁩'`, `ByteOrderMark='﻿'`. `internal static bool IsTashkeel(char character) => character is (>= FathatanFirst and <= DiacriticLast) or SuperscriptAlef or (>= QuranicMarkFirst and <= QuranicMarkLast);` `internal static bool IsInvisibleControl(char character) => character is ArabicLetterMark or (>= ZeroWidthSpace and <= RightToLeftMark) or (>= LeftToRightEmbedding and <= RightToLeftOverride) or WordJoiner or (>= LeftToRightIsolate and <= PopDirectionalIsolate) or ByteOrderMark;` `internal static char Map(char character, AnswerNormalization rules) => character switch { >= ArabicIndicZero and <= ArabicIndicNine when rules.ConvertDigits => (char)('0' + (character - ArabicIndicZero)), >= ExtendedZero and <= ExtendedNine when rules.ConvertDigits => (char)('0' + (character - ExtendedZero)), ArabicComma => ',', FarsiYeh => Yaa, AlefHamzaAbove or AlefHamzaBelow or AlefMadda or AlefWasla when rules.UnifyAlef => Alef, TaaMarbuta when rules.UnifyTaaMarbuta => Haa, AlefMaqsura when rules.UnifyAlefMaqsura => Yaa, _ => character };` |
| M1 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddAnswerNormalizationRules.cs` (+ `.Designer.cs`) | EF migration | Generate with `dotnet tool restore && dotnet ef migrations add AddAnswerNormalizationRules -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. The model snapshot must not change; if the tool rewrites `AppDbContextModelSnapshot.cs`, it must be identical. Add four `public const string` fields to the partial class: `UpgradeGradingSpecsSql`, `UpgradeRevisionSnapshotsSql`, `DowngradeGradingSpecsSql` and `DowngradeRevisionSnapshotsSql` (SQL below). They must contain **no `{` or `}` characters**, because the tests run them through `ExecuteSqlRawAsync`. `Up`: `migrationBuilder.Sql(UpgradeGradingSpecsSql); migrationBuilder.Sql(UpgradeRevisionSnapshotsSql);`. `Down`: the two downgrade constants. |
| W1 | `web/src/features/questions/components/NormalizationRulesField.tsx` | component | `export function NormalizationRulesField()`. Uses `useTranslation('questions')`. Renders `<fieldset className="flex flex-col gap-1">`, then `<legend className="mb-2 text-caption text-text-muted">{t('editor.normalization.legend')}</legend>`, then `<p className="text-caption text-text-muted">{t('editor.normalization.hint')}</p>`, then `<div className="grid md:grid-cols-2 md:gap-x-4">` containing `normalizationRules.map((rule) => <CheckboxField<QuestionValues> key={rule} name={`normalization.${rule}`} label={t(`editor.normalization.${rule}`)} />)`. |

**Migration SQL (exact):**
```sql
-- UpgradeGradingSpecsSql
UPDATE "Questions" SET "GradingSpec" = ("GradingSpec" - 'unifyLetterVariants') || jsonb_build_object('normalization', jsonb_build_object('stripTashkeel', true, 'stripTatweel', true, 'unifyAlef', COALESCE(("GradingSpec" ->> 'unifyLetterVariants')::boolean, true), 'unifyTaaMarbuta', COALESCE(("GradingSpec" ->> 'unifyLetterVariants')::boolean, true), 'unifyAlefMaqsura', COALESCE(("GradingSpec" ->> 'unifyLetterVariants')::boolean, true), 'convertDigits', true, 'collapseWhitespace', true, 'foldCase', true)) WHERE "GradingSpec" ? 'unifyLetterVariants';
-- UpgradeRevisionSnapshotsSql
UPDATE "QuestionRevisions" SET "Snapshot" = jsonb_set("Snapshot", ARRAY['gradingSpec'], (("Snapshot" -> 'gradingSpec') - 'unifyLetterVariants') || jsonb_build_object('normalization', jsonb_build_object('stripTashkeel', true, 'stripTatweel', true, 'unifyAlef', COALESCE(("Snapshot" -> 'gradingSpec' ->> 'unifyLetterVariants')::boolean, true), 'unifyTaaMarbuta', COALESCE(("Snapshot" -> 'gradingSpec' ->> 'unifyLetterVariants')::boolean, true), 'unifyAlefMaqsura', COALESCE(("Snapshot" -> 'gradingSpec' ->> 'unifyLetterVariants')::boolean, true), 'convertDigits', true, 'collapseWhitespace', true, 'foldCase', true))) WHERE ("Snapshot" -> 'gradingSpec') ? 'unifyLetterVariants';
-- DowngradeGradingSpecsSql
UPDATE "Questions" SET "GradingSpec" = ("GradingSpec" - 'normalization') || jsonb_build_object('unifyLetterVariants', COALESCE(("GradingSpec" -> 'normalization' ->> 'unifyAlef')::boolean, true)) WHERE "GradingSpec" ? 'normalization';
-- DowngradeRevisionSnapshotsSql
UPDATE "QuestionRevisions" SET "Snapshot" = jsonb_set("Snapshot", ARRAY['gradingSpec'], (("Snapshot" -> 'gradingSpec') - 'normalization') || jsonb_build_object('unifyLetterVariants', COALESCE(("Snapshot" -> 'gradingSpec' -> 'normalization' ->> 'unifyAlef')::boolean, true))) WHERE ("Snapshot" -> 'gradingSpec') ? 'normalization';
```

## Error codes
None new. A malformed `normalization` returns the existing `QUESTION_GRADING_SPEC_INVALID` (422) from `FillQuestionRules.Validate` or `ShortQuestionRules.Validate`, through `QuestionSchemaReader.TryRead`.

## Domain behaviour
- There is no entity change: no state transitions, no `UpdationDate` change, and no new `BusinessRuleViolationException`. The graders remain pure functions.
- `AnswerNormalizer.Normalize` is idempotent.
- An answer that normalises to empty never matches (existing `Matches` guard, unchanged).
- The same rules apply to the student answer and to every accepted answer.

## API surface
There are no new or changed routes. Changed behaviour on existing endpoints:
- `POST /api/questions` and `PUT /api/questions/{id}`, plus `PUT …/resubmit`: they accept `gradingSpec.normalization` and store all 8 rules canonically. Policy: `Content.Manage`, unchanged.
- `POST /api/questions/grade-draft`: grades with the draft's rules.
- `POST /api/questions/import` (and its check step): builds `normalization` from `unify_letter_variants` (D9).

## Test plan
Assertion library: FluentAssertions (as already pinned). `Default` means `AnswerNormalization.Default`; `LettersOff` means `new AnswerNormalization(UnifyAlef: false, UnifyTaaMarbuta: false, UnifyAlefMaqsura: false)`.

**Modify (existing tests; only the listed edits):**
| # | Test class | Test method | Edit |
|---|-----------|-------------|------|
| X1 | `AnswerNormalizerTests` | all 8 existing methods | `unifyLetterVariants: true` → `AnswerNormalization.Default`; `unifyLetterVariants: false` (in `Normalize_UnifyOff_KeepsLetterVariants`) → `LettersOff`. Expected values are unchanged. |
| X2 | `TextGraderTests` | `GradeFill_SpellingVariantWithUnifyOn_Matches`, `GradeFill_SpellingVariantWithUnifyOff_DoesNotMatch` and helpers `Capital`, `Text` | `Capital(AnswerNormalization normalization)` → `new FillGradingSpec([...], normalization)`. The on test passes `AnswerNormalization.Default`; the off test passes `new AnswerNormalization(UnifyTaaMarbuta: false)`. `Text(accepted)` → `new ShortGradingSpec(null, null, null, [accepted], AnswerNormalization.Default)`. |
| X3 | `FillQuestionRulesTests` | `Normalize_MissingUnifyFlag_DefaultsTrueTrimsAndFollowsBodyOrder` → rename to `Normalize_MissingNormalization_WritesAllRulesOnTrimsAndFollowsBodyOrder` | Expected: `{"blanks":[{"id":"b","acceptedAnswers":["y"]},{"id":"a","acceptedAnswers":["x"]}],"normalization":{"stripTashkeel":true,"stripTatweel":true,"unifyAlef":true,"unifyTaaMarbuta":true,"unifyAlefMaqsura":true,"convertDigits":true,"collapseWhitespace":true,"foldCase":true}}` |
| X4 | `ShortQuestionRulesTests` | `Normalize_Text_TrimsAnswersAndDefaultsUnifyTrue` → rename to `Normalize_Text_TrimsAnswersAndDefaultsAllRulesOn` | Expected `{"acceptedAnswers":["ماء"],"normalization":{…all 8 true…}}` (DeepEquals). |
| X5 | `QuestionImportParserTests` | `ParseAsync_ValidFillRow_ReturnsBlanksFromFilledColumns` | The last line becomes `fields.GradingSpec.GetProperty("normalization").GetProperty("unifyAlef").GetBoolean().Should().BeTrue();` |
| X6 | `AppDbContextTests` | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | Append the 13th entry `_AddAnswerNormalizationRules`. |
| X7 | `questionValues.test.ts` | `requestCases` Fill row; `roundTripCases` Fill and Short-text rows | Add `const allRulesOn = { stripTashkeel: true, stripTatweel: true, unifyAlef: true, unifyTaaMarbuta: true, unifyAlefMaqsura: true, convertDigits: true, collapseWhitespace: true, foldCase: true };`. Fill request expected: `{ blanks: [...], normalization: allRulesOn }`. Fill round-trip: `normalization: { ...allRulesOn, unifyAlef: false }` replaces `unifyLetterVariants: false`. Short-text round-trip: `normalization: allRulesOn` replaces `unifyLetterVariants: true`. |

**New — api unit (`api/Elmanhg.Tests/Domain/Questions/Grading/`):**
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| N1 | `AnswerNormalizerTests` | `Normalize_StripTashkeelOff_KeepsTashkeel` | `"مَاء"`, `new(StripTashkeel: false)` → `"مَاء"` |
| N2 | 〃 | `Normalize_StripTatweelOff_KeepsTatweel` | `"مـاء"`, `new(StripTatweel: false)` → `"مـاء"` |
| N3 | 〃 | `Normalize_UnifyAlefOff_KeepsAlefFormsButUnifiesTaaMarbuta` | `"أسامة"`, `new(UnifyAlef: false)` → `"أسامه"` |
| N4 | 〃 | `Normalize_UnifyTaaMarbutaOff_KeepsTaaMarbutaButUnifiesAlef` | `"أسامة"`, `new(UnifyTaaMarbuta: false)` → `"اسامة"` |
| N5 | 〃 | `Normalize_UnifyAlefMaqsuraOff_KeepsAlefMaqsura` | `"مصطفى"`, `new(UnifyAlefMaqsura: false)` → `"مصطفى"` |
| N6 | 〃 | `Normalize_ConvertDigitsOff_KeepsArabicIndicDigits` | `"٢٠ ۳"`, `new(ConvertDigits: false)` → `"٢٠ ۳"` |
| N7 | 〃 | `Normalize_CollapseWhitespaceOff_KeepsInnerWhitespaceAndTrims` | `"  a \t b  "`, `new(CollapseWhitespace: false)` → `"a \t b"` |
| N8 | 〃 | `Normalize_FoldCaseOff_KeepsCase` | `"Newton"`, `new(FoldCase: false)` → `"Newton"` |
| N9 | 〃 | `Normalize_DecomposedHamzaOrMadda_ComposesWithUnifyAlefOff` [Theory] | `("أ", "أ")`, `("إ", "إ")`, `("آ", "آ")` with `new(UnifyAlef: false)` → the expected value |
| N10 | 〃 | `Normalize_InvisibleControl_IsRemoved` [Theory] | For each of `؜ ​ ‌ ‍ ‎ ‏ ‫ ‮ ⁠ ⁦ ⁩ ﻿`: `$"م{c}اء"` with `Default` → `"ماء"` |
| N11 | 〃 | `Normalize_ArabicComma_MapsToComma` | `"أ، ب"`, `Default` → `"ا, ب"` |
| N12 | 〃 | `Normalize_PersianYeh_MapsToYaaWithMaqsuraOff` | `"علی"`, `new(UnifyAlefMaqsura: false)` → `"علي"` |
| N13 | 〃 | `Normalize_UnpairedSurrogate_IsDropped` | `"a\uD800b"`, `Default` → `"ab"`, no exception |
| N14 | 〃 | `Normalize_AllRulesOff_AppliesOnlyAlwaysOnSteps` | `" Aـَ٢‏ "` with all 8 false → `"Aـَ٢"` |
| N15 | `TextGraderTests` | `GradeShort_NumericWithExponent_ReturnsZero` | `Numeric(9.8m, 0.1m, Absolute)`, `"9.8e0"` → `0m` |
| N16 | 〃 | `GradeShort_NumericWithArabicThousandsSeparator_ReturnsOne` | `Numeric(1000m, 0m, Absolute)`, `"١٬٠٠٠"` → `1m` |
| N17 | 〃 | `GradeShort_NumericWithRightToLeftMark_ReturnsOne` | `Numeric(9.8m, 0.1m, Absolute)`, `"‏٩٫٧٥"` → `1m` |
| N18 | 〃 | `GradeShort_NumericWithArabicComma_ReturnsOne` | `Numeric(9.8m, 0m, Absolute)`, `"٩،٨"` → `1m` |
| N19 | 〃 | `GradeShort_NumericIgnoresLetterRules_ParsesArabicDigits` | the spec carries a `Normalization` with every rule off (`new ShortGradingSpec(9.8m, 0.1m, Absolute, null, new AnswerNormalization(false, false, false, false, false, false, false, false))`), `"٩٫٨"` → `1m` |
| N20 | 〃 | `GradeFill_NullNormalization_UsesDefaultRules` | `new FillGradingSpec([new FillBlankAnswers("1", ["القاهرة"])], null)`, answer `"القاهره"` → `1m` |
| N21 | 〃 | `GradeShort_TextNullNormalization_UsesDefaultRules` | `new ShortGradingSpec(null, null, null, ["القاهرة"], null)`, `"القاهره"` → `1m` |
| N22 | 〃 | `GradeShort_TextFoldCaseOff_DoesNotMatchOtherCase` | accepted `"newton"`, `new(FoldCase: false)`, `"Newton"` → `0m` |
| N23 | `QuestionGraderTests` | `Grade_FillWithPartialNormalizationJson_AppliesRuleOff` | spec `{"blanks":[{"id":"1","acceptedAnswers":["القاهرة"]}],"normalization":{"unifyTaaMarbuta":false}}`, answer `{"blanks":[{"id":"1","text":"القاهره"}]}` → `Outcome == Incorrect`; the same spec with the answer `"القاهرة"` is covered by N24 |
| N24 | 〃 | `Grade_FillWithPartialNormalizationJson_KeepsOtherRulesOn` | the same spec, answer `"القاهرَة"` (with tashkeel) → `Correct` |
| N25 | `EgyptianSpellingVariantsTests` (new file) | `GradeShort_EgyptianVariantOfAcceptedAnswer_ReturnsOne` [Theory] | Spec `new ShortGradingSpec(null, null, null, [accepted], AnswerNormalization.Default)` → `1m`. Rows (answer, accepted): `("القاهره","القاهرة")`, `("اسكندريه","إسكندرية")`, `("مصطفي","مصطفى")`, `("الوادى","الوادي")`, `("احمد","أحمد")`, `("ابراهيم","إبراهيم")`, `("القران","القرآن")`, `("العلم","ٱلعلم")`, `("مدرسه","مَدْرَسَة")`, `("جمـــال","جمال")`, `("2024","٢٠٢٤")`, `("  نهر   النيل ","نهر النيل")`, `("رئيس","رئيس")`, `("علی","علي")`, `("احمد, محمد","أحمد، محمد")`, `("‏ماء","ماء")`, `("NaCl","nacl")` |
| N26 | 〃 | `GradeShort_DifferentWord_ReturnsZero` [Theory] | The same spec → `0m`. Rows: `("القاهر","القاهرة")`, `("عل","على")`, `("مسئول","مسؤول")` (hamza seats not unified, see Scope/Out), `("هواء","ماء")`, `("١٣","12")` |

**New — api unit (`api/Elmanhg.Tests/Application/Features/Questions/Shared/`):**
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| N27 | `FillQuestionRulesTests` | `Normalize_PartialNormalization_WritesEveryRuleWithMissingOn` | spec `…,"normalization":{"unifyAlef":false}` → the canonical string equals the X3 blanks plus `"normalization"` with `unifyAlef:false` and the other 7 true, in record order (`Should().Be`) |
| N28 | 〃 | `Validate_NormalizationNotAnObject_ReturnsQuestionGradingSpecInvalid` | `"normalization":"off"` → contains `ErrorCodes.QuestionGradingSpecInvalid` |
| N29 | 〃 | `Validate_NormalizationRuleNotBoolean_ReturnsQuestionGradingSpecInvalid` | `"normalization":{"unifyAlef":"no"}` → contains `ErrorCodes.QuestionGradingSpecInvalid` |
| N30 | `ShortQuestionRulesTests` | `Normalize_TextWithRulesOff_KeepsChosenRules` | `{"acceptedAnswers":["ماء"],"normalization":{"foldCase":false,"convertDigits":false}}` → DeepEquals, with those two false and the other 6 true |
| N31 | 〃 | `Normalize_NumericWithNormalization_DropsNormalization` | numeric body plus `"normalization":{"foldCase":false}` → `{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}` |
| N32 | `QuestionImportParserTests` | `ParseAsync_FillRowUnifyFalse_TurnsOffOnlyLetterRules` | Fill sheet with `unify_letter_variants`=`false` → `normalization.unifyAlef`, `unifyTaaMarbuta` and `unifyAlefMaqsura` are false; `stripTashkeel` and `foldCase` are true |
| N33 | 〃 | `ParseAsync_TextShortRowUnifyFalse_TurnsOffOnlyLetterRules` | Short text sheet (`answer_kind`=`text`, `accepted_answers`=`ماء`, `unify_letter_variants`=`false`) → the same assertions |

**New — api integration:**
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| N34 | `QuestionsEndpointTests` | `Post_FillWithPartialNormalization_StoresEveryRule` | POST Fill `{"blanks":[{"id":"1","acceptedAnswers":["20"]}],"normalization":{"unifyAlef":false}}` → 200. `QuestionTestData.ReadQuestionAsync` gives `GradingSpec` whose `normalization` DeepEquals all-true except `unifyAlef:false`. The revision v1 snapshot's `gradingSpec` DeepEquals the same. |
| N35 | `QuestionGradeDraftEndpointTests` | `Post_FillWithTaaMarbutaRuleOff_GradesVariantIncorrect` | Draft Fill, spec `normalization:{"unifyTaaMarbuta":false}`, accepted `القاهرة`, answer `القاهره` → 200, `outcome == "Incorrect"` |
| N36 | 〃 | `Post_NormalizationNotAnObject_Returns422QuestionGradingSpecInvalid` | `"normalization":"off"` → 422 and the code contains `QUESTION_GRADING_SPEC_INVALID` |
| N37 | `AnswerNormalizationRulesMigrationTests` (new, `Integration/Persistence/`) | `UpgradeSql_LegacyFillUnifyFalse_WritesLetterRulesOffOthersOn` | Seed with `QuestionTestData.SeedQuestionAsync`. Use `ExecuteSqlAsync` (interpolated, parameterised) to set `"Type"='Fill'`, `"GradingSpec"={legacy}::jsonb` with legacy `{"blanks":[{"id":"1","acceptedAnswers":["20"]}],"unifyLetterVariants":false}`, and to set the revision snapshot's `gradingSpec` with `jsonb_set(…, ARRAY['gradingSpec'], {legacy}::jsonb)`. Run `ExecuteSqlRawAsync(AddAnswerNormalizationRules.UpgradeGradingSpecsSql)` and `…UpgradeRevisionSnapshotsSql`. Both the spec and the snapshot `gradingSpec` DeepEqual `{"blanks":[…],"normalization":{3 letter rules false, 5 true}}` and have no `unifyLetterVariants`. |
| N38 | 〃 | `UpgradeSql_LegacyTextShortUnifyTrue_WritesAllRulesOn` | Same approach with `"Type"='Short'` and spec `{"acceptedAnswers":["ماء"],"unifyLetterVariants":true}` → `normalization` all true |
| N39 | 〃 | `UpgradeSql_NumericShortSpec_IsUnchanged` | Spec `{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}` → after both upgrades it DeepEquals the input |

**New — web (`web/src/features/questions/`):**
| # | File | Test | Asserts |
|---|------|------|---------|
| N40 | `api/questionValues.test.ts` | `it('turns every normalisation rule on for a new question')` | `emptyQuestionValues('Fill').normalization` toEqual `allRulesOn` |
| N41 | 〃 | `it('turns missing normalisation rules on when reading a stored spec')` | A Fill detail with `normalization: { foldCase: false }` → `toQuestionValues(...).normalization` toEqual `{ ...allRulesOn, foldCase: false }` |
| N42 | 〃 | `it('turns every rule on when a stored spec has no normalisation')` | A Short text detail with `gradingSpec: { acceptedAnswers: ['ماء'] }` → `normalization` toEqual `allRulesOn` |
| N43 | `pages/QuestionEditorPage.test.tsx` | `it('shows the stored normalisation rules and saves a changed rule')` | Stored Fill question (`stem '<p>v = [[1]] m/s</p>'`, `body {blanks:[{id:'1'}]}`, spec `normalization {...allRulesOn, unifyAlef:false}`). The checkbox `Treat أ إ آ ٱ as ا` is not checked; `Ignore diacritics (tashkeel)` is checked. Click `Ignore Latin letter case`, then Save → `Question saved.`; `bodies[0].gradingSpec` toEqual `{ blanks:[{id:'1',acceptedAnswers:['20']}], normalization:{...allRulesOn, unifyAlef:false, foldCase:false} }` |
| N44 | 〃 | `it('shows the normalisation rules only for a text short answer')` | Stored Short numeric question → `queryByRole('group', { name: 'Answer normalisation' })` is null. Select `Text` in `Answer type` → the group is visible with 8 checkboxes. |
| N45 | 〃 | `it('labels the normalisation rules in Arabic')` | The Fill question from N43 opened with `openEditor('ar')` → `getByRole('group', { name: 'تطبيع الإجابة' })` and `getByRole('checkbox', { name: 'تجاهل التشكيل' })` |

## Definition of done
- [ ] There is exactly one normaliser: `AnswerNormalizer.Normalize(string?, AnswerNormalization)`. The `bool` overload is gone, and no other normaliser exists in the repo.
- [ ] `AnswerNormalization` has the 8 rules, all default `true`, and `Default` is all on.
- [ ] The always-on steps run in the D6 order: surrogates, NFC, invisible controls, ، → `,`, ی → ي, trim.
- [ ] Each of the 8 toggles works independently (N1–N8, N3/N4).
- [ ] The code-point constants are `\uXXXX` escapes in `ArabicCharacters`. `AnswerNormalizer.cs` and `ArabicCharacters.cs` are each at most about 100 lines.
- [ ] Numeric parsing rejects exponents, strips ٬, and ignores per-question rules. `docs/question-schemas.md` says so.
- [ ] Fill and Short-text canonical specs write all 8 `normalization` rules. Numeric specs write none.
- [ ] Migration `AddAnswerNormalizationRules` rewrites the questions and revision snapshots. The model snapshot is unchanged. Down reverses it. N37–N39 pass.
- [ ] Import: `unify_letter_variants` drives the three letter rules only. Both resx help strings are updated.
- [ ] Web editor: the "Answer normalisation" group appears for Fill and Short text only, the values round-trip, and the `editor.blanks.unify` key is deleted from en and ar.
- [ ] No new error codes and no endpoint changes. `api/openapi/v1.json` and the Orval client show no drift.
- [ ] Docs are updated in the same change: `PRD.md` §6.2, `question-schemas.md`, `question-import.md`, `claude-design-prompt.md` §4/§5 and `prototype.md`.
- [ ] Every test in the Test plan exists with that name. Only X1–X7 modify existing tests.
- [ ] `dotnet test api/ -c Release` passes with `appsettings.json` moved aside. `npm --prefix web run typecheck`, `lint`, `format:check` and `test` pass.
- [ ] The #151 normaliser items (NFC, bidi and zero-width controls, ، and ٬, Persian ی, exponent) are closed by this change. The other #151 items are untouched.
