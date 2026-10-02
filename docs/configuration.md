# Configuration page, runtime settings and feature flags

The admin Configuration page shows everything configurable in one place (PRD §10.6). Business settings and feature flags can be changed at runtime; infrastructure is shown read-only.

## 1. What the page shows

- Route `#/admin/configuration`, nav «الإعدادات» / "Configuration" (last admin item, under «المزيد» on mobile). Policy `Configuration.Manage`, Admin only; web capability `configurationManage`.
- One card per settings group, in this order: Feature flags (`Features`), Ask a Teacher (`AskTeacher`), Plan limits (`PlanLimits`), AI grading (`Grading`), Upload limits (`Uploads`). Each setting shows its label, description, current value, a «معدّل»/«افتراضي» badge, the default, when it was last changed, an editor for its type, Save, and Reset to default when it is overridden.
- Then the read-only Infrastructure card (§6).

API:

| Method | Route | Result |
|---|---|---|
| GET | `/api/configuration/settings` | groups with their settings (`RuntimeSettingGroupResult[]`) |
| PUT | `/api/configuration/settings/{key}` body `{ "value": <json> }` | the setting (`RuntimeSettingResult`) |
| POST | `/api/configuration/settings/{key}/reset` | the setting |
| GET | `/api/configuration/infrastructure` | `InfrastructureConfigurationResult` |

Errors: `RUNTIME_SETTING_KEY_REQUIRED` and `RUNTIME_SETTING_VALUE_INVALID` (422), `RUNTIME_SETTING_NOT_FOUND` (404), `ASK_TEACHER_REMINDER_ORDER_INVALID` (400), `RUNTIME_SETTING_MODIFIED_CONCURRENTLY` (409).

## 2. Settings

| Key | Group | Type | Range | Default from |
|---|---|---|---|---|
| `features.examsRequireAllLessonsOpened` | Features | Boolean | – | `Exams:RequireAllLessonsOpened` |
| `features.refundsEnabled` | Features | Boolean | – | `false` (constant, no Options key) |
| `askTeacher.replySlaHours` | AskTeacher | Integer | 1–168 | `Subscriptions:AskTeacherReplySlaHours` |
| `askTeacher.firstReminderAfterHours` | AskTeacher | Integer | 1–167 | `AskTeacher:FirstReminderAfterHours` |
| `askTeacher.secondReminderAfterHours` | AskTeacher | Integer | 1–167 | `AskTeacher:SecondReminderAfterHours` |
| `plans.freeDailyQuizQuestions` | PlanLimits | Integer | 0–1000 | `Subscriptions:FreeDailyQuizQuestions` |
| `plans.freeDailyAvatarMessages` | PlanLimits | Integer | 0–1000 | `Subscriptions:FreeDailyAvatarMessages` |
| `plans.freeOpenLessonsPerUnit` | PlanLimits | Integer | 0–100 | `Subscriptions:FreeOpenLessonsPerUnit` |
| `plans.baseDailyAvatarMessages` | PlanLimits | Integer | 1–10000 | `Subscriptions:BaseDailyAvatarMessages` |
| `plans.askTeacherMonthlyQuestions` | PlanLimits | Integer | 1–1000 | `Subscriptions:AskTeacherMonthlyQuestions` |
| `grading.essayReviewConfidenceThreshold` | Grading | Decimal | 0–1 | `EssayGrading:ReviewConfidenceThreshold` |
| `grading.mathStepReviewConfidenceThreshold` | Grading | Decimal | 0–1 | `MathStepGrading:ReviewConfidenceThreshold` |
| `uploads.askTeacherImageMaxSizeInMb` | Uploads | Integer | 1–9 | `AskTeacher:ImageMaxSizeInMb` |
| `uploads.voiceReplyMaxSizeInMb` | Uploads | Integer | 1–9 | `AskTeacher:VoiceMaxSizeInMb` |
| `uploads.voiceReplyMaxDurationSeconds` | Uploads | Integer | 10–600 | `AskTeacher:VoiceMaxDurationSeconds` |

Cross-setting rule: first reminder < second reminder < reply time (`ASK_TEACHER_REMINDER_ORDER_INVALID`), checked on update and on reset against the candidate values. Upload sizes stop at 9 MB because Kestrel accepts at most a 10 MB request body and the multipart overhead must fit.

## 3. How it works

- **Storage.** Table `RuntimeSettingOverrides`: one row per key that has ever been overridden, `Key` (unique, `IX_RuntimeSettingOverrides_Key`), `Value` (jsonb, nullable) and an xmin concurrency token. A null `Value` means "use the default". The first override inserts the row; Reset sets `Value` to null and never deletes the row, so the audit diff always shows the before and after value.
- **Defaults.** The default of each setting is the deployment value of the Options key in the table above (or the constant shown there), so behaviour is unchanged until an admin overrides it. A stored value that no longer fits its definition is ignored and the default is used.
- **Typed definition.** Each setting is declared once with its type (`Integer`, `Decimal`, `Boolean`, `Choice`, `ChoiceList`), range or allowed values, and an Arabic and English label and description. Values travel as plain JSON: number, `true`/`false`, string, array of strings.
- **Cache.** Every API instance caches the effective values for `RuntimeSettings:CacheSeconds` (default 30, range 1–3600). Update and reset clear the cache on the instance that handled them, so the change takes effect at once there.
- **Startup validation.** The API refuses to start when a configured default is outside its runtime definition (for example `AskTeacher__ImageMaxSizeInMb=12`) or a key is registered twice.
- **Audit.** Update writes `RuntimeSetting.Update` and reset writes `RuntimeSetting.Reset` (resource type `RuntimeSetting`, resource id = the override row). The diff shows `key` and `value` before and after; a null value means the configuration default. Resetting a setting that is not overridden is a no-op with a Success row and no diff ([audit-log.md](audit-log.md)).

## 4. Add a setting

1. Declare the key in a group class that implements `IRuntimeSettingDefinitions` (under `Application/Shared/RuntimeSettings/Definitions`):

   ```csharp
   public static readonly RuntimeSettingKey<int> ReplySlaHours = new("askTeacher.replySlaHours");
   ```

2. Add its definition; the default reads the existing, validated Options value:

   ```csharp
   RuntimeSettingDefinition.ForInteger(ReplySlaHours, RuntimeSettingGroup.AskTeacher, subscriptionsOptions.Value.AskTeacherReplySlaHours, 1, 168, new LocalizedText("…", "Teacher reply time (hours)"), new LocalizedText("…", "How long a teacher has to reply…"))
   ```

   Factories: `ForInteger`, `ForDecimal`, `ForBoolean`, `ForChoice`, `ForChoiceList`. A flag with no deployment value uses a constant default (for example `features.refundsEnabled`, `false`).
3. For a new group, add a member to `RuntimeSettingGroup` (its position is the page order) and register the class: `services.AddSingleton<IRuntimeSettingDefinitions, XRuntimeSettings>()`.
4. Read it where it is used: `await runtimeSettings.GetAsync(XRuntimeSettings.Key, cancellationToken)`, or `GetValuesAsync` for several keys.
5. Put a rule that spans settings in the group's `Constraints`.
6. On the web, add `groups.<Group>.title` and `.description` in `features/configuration/i18n/{ar,en}.json`, and `choices.<value>` labels for Choice values. A new value type is added to `RuntimeSettingType`, `RuntimeSettingValueRules`, `runtimeSettingSchemas.ts` and one editor component.

## 5. Custom sections

Complex values (for example the exam periods of the SLA calendar, #254) are not runtime settings. They get their own aggregate, table and audited CRUD commands under `/api/configuration/<resource>` with the `Configuration.Manage` policy, and a section component that `ConfigurationPage` renders directly under the card of the related group (for example `<ExamPeriodsSection />` after the `SlaCalendar` group card).

## 6. Infrastructure section (read-only)

- **Environment**: the host environment name.
- **Integrations**: `otpWhatsApp`, `otpEmail`, `otpSms`, `invitationEmail`, `payments`, `fileStorage`, `aiService`, each with its provider and mode: Fake (a built-in fake, or a disabled OTP channel, which also uses the fake), Local (local-disk file storage) or Real.
- **AI service**: when `AiService:Provider=Http` the API calls the AI service `GET /v1/configuration` ([ai-service.md](ai-service.md)) and shows its LLM provider, chat, essay and math-step models, embedding and transcription provider and model, and whether its API keys are set. With the Fake provider the status is "not used"; when the call fails or does not answer within `AiService:ConfigurationTimeoutSeconds` (5 s) the status is "unreachable" (logged as a Warning) and the rest of the page still renders.
- **Safety switches**: `Payments:AllowFakePayments`, its effective value (unset: true in Development, false elsewhere), shown on or off. Never editable from the UI or the API; the API refuses to start with it true in Production.
- **Secrets**: the keys checked by the production placeholder guard ([security.md](security.md)), each shown as set or not set. Set means not blank and not a `change-me` placeholder. Only `{ key, isSet }` is returned; secret values never leave the process.

## 7. Not runtime-editable

| Values | Why |
|---|---|
| `Mastery:CorrectThreshold` | Mastery is stored at attempt time; a change would make stored mastery disagree with question selection (PRD §17 rule 6). |
| Prices, `Currency`, `GracePeriodDays`, `RenewalWindowDays` | They move money or access, and in-flight checkouts carry amounts. |
| `Sessions` quiz sizes | The quiz size picker is fixed at 5/10/20. |
| Sweep switches, intervals, batch sizes, retry options, rate limits | Read once by hosted services and rate-limit policies at startup. |
| Text and field lengths, page sizes, cache seconds | Schema and contract caps tied to column widths and AI-service limits. |
| `Content:LessonImageMaxSizeInMb`, `QuestionImportMaxFileSizeInMb` | Admin authoring caps and the import parse-time bound (#153). |
| `AskTeacher:TranscriptionLanguage` | Part of the AI-service contract. |
| All infrastructure | Set by environment variables at deploy time (§6). |

## 8. Known limits

- On more than one API instance, a change can take up to `RuntimeSettings:CacheSeconds` to reach the other instances. Hosting is a single VPS instance today.
- Changing `askTeacher.replySlaHours` applies to questions submitted, and follow-ups opened, after the change; the stored deadline of an open thread is not recomputed. Reminder times of open threads are computed from that stored deadline minus the current reply time, so changing it mid-window shifts their reminders. #254 replaces this with recomputed calendar deadlines.
