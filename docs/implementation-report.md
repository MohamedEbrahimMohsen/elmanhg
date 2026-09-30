# Implementation report

The result of the autopilot run that built the Elmanhg board (https://github.com/users/MohamedEbrahimMohsen/projects/1) story by story, from 2026-09-27 to 2026-10-01. It covers what was merged, the issues the run opened, what is still faked, how to run the system, the decisions taken on the dev's behalf, and the known gaps.

Sources: `PROGRESS.md` (Finished stories), `.process/<issue>-<slug>/`, and GitHub (`gh pr list`, `gh issue list`). Every PR and issue number below was checked against GitHub.

## 1. Summary

| | Count |
|---|---|
| Stories on the board | 60 (E1–E17, including #171, which the run added on the dev's instruction) |
| Merged | 60 |
| In progress | 0 |
| Skipped, failed or blocked | 0 |
| Issues opened by the run | 59: 55 `deferred`, 3 `dev-decision`, 1 `story` (#171) |
| CodeRabbit | reviewed 18 PRs: 32 comments fixed in the PR, 4 fixed in the next story (#117 → #118), 8 rejected or deferred. It skipped 37 PRs (over 100 files), was rate-limited on 3 and had no comments on 2. |

Other PRs outside the stories: #129 (docs, prototype, tooling), #130 (pipeline setup), #149 (cloud-session pipeline).

## 2. Stories

Status legend:
- **merged**: merged on green CI, with no open follow-up issue.
- **merged-with-debt**: merged on green CI, with an open `deferred` or `dev-decision` follow-up issue.

Review rounds count the fresh-reviewer rounds; extra work after review is in brackets. CodeRabbit is "fixed / rejected"; "skipped" means CodeRabbit skipped the PR (too many files), which the dev ruled counts as no comments.

| Story | Title | Status | PR | Review rounds | CodeRabbit | Issues opened |
|---|---|---|---|---|---|---|
| #54 | [E1.S1] Bootstrap backend | merged-with-debt | #131 | 2 | skipped | #132 |
| #55 | [E1.S2] Bootstrap frontend | merged-with-debt | #133 | 2 | skipped | #134, #135 |
| #56 | [E1.S3] Authentication with phone OTP and email | merged-with-debt | #136 | 2 | skipped | #137 |
| #57 | [E1.S4] Role-based authorisation and teacher subject scoping | merged-with-debt | #138 | 1 | 0 / 1 (deferred to #139) | #139 |
| #58 | [E1.S5] Audit log | merged | #140 | 2 (+2 CI-fix rounds) | 2 / 0 | — |
| #171 | [E1.S6] OTP delivery channels | merged-with-debt | #184 | 1 (+hardening) | skipped | #185 |
| #60 | [E2.S1] Subject and unit CRUD | merged-with-debt | #141 | 1 | skipped | #142 |
| #61 | [E2.S2] Lesson authoring | merged-with-debt | #143 | 2 | skipped | #144 |
| #62 | [E2.S3] Lesson lifecycle (+ reorder and delete) | merged-with-debt | #145 | 1 | 1 / 0 | #146 |
| #64 | [E3.S1] Question aggregate | merged-with-debt | #147 | 2 | skipped | #148 |
| #65 | [E3.S2] Admin question editor, preview, test grader | merged-with-debt | #150 | 2 | skipped | #151 |
| #66 | [E3.S3] Bulk question import | merged-with-debt | #152 | 2 | skipped | #153 |
| #67 | [E3.S4] Servable rule | merged-with-debt | #154 | 1 | 1 / 1 | #155, #156 |
| #68 | [E3.S5] Teacher validation queue | merged-with-debt | #157 | 1 | skipped | #158 |
| #70 | [E4.S1] Arabic answer normalisation | merged-with-debt | #159 | 2 | 1 / 0 (2 fix cycles) | #160 |
| #71 | [E4.S2] Graders for mcq, multi-select, true/false | merged-with-debt | #161 | 1 | rate-limited | #162 |
| #72 | [E4.S3] Graders for fill-in-the-blank and short answer | merged-with-debt | #163 | 2 | 1 / 0 | #164 |
| #74 | [E5.S1] Attempt log and session model | merged-with-debt | #165 | 2 | 3 / 0 | #166 |
| #75 | [E5.S2] Adaptive question selection | merged-with-debt | #167 | 1 | rate-limited | #168 |
| #76 | [E5.S3] Quiz screen with immediate feedback | merged-with-debt | #169 | 2 | 1 / 0 | #170 |
| #77 | [E5.S4] Mastery calculation and headline counter | merged-with-debt | #172 | 1 | 2 / 0 | #173 |
| #78 | [E5.S5] Progress page | merged-with-debt | #174 | 2 | 1 / 0 | #175 |
| #80 | [E6.S1] Exam blueprint authoring | merged-with-debt | #176 | 2 | skipped | #177 |
| #81 | [E6.S2] Unit exam generation and sitting | merged-with-debt | #178 | 2 (+1 test fix) | skipped | #179 |
| #82 | [E6.S3] Multi-unit exam builder | merged-with-debt | #180 | 1 (+doc fix) | skipped | #181 |
| #83 | [E6.S4] Retakes and best score | merged-with-debt | #182 | 1 | no comments | #183 |
| #85 | [E7.S1] Subject, unit and lesson browsing | merged-with-debt | #194 | 2 | skipped | #195 |
| #86 | [E7.S2] Landing page and onboarding | merged-with-debt | #196 | 1 | skipped | #197 |
| #87 | [E7.S3] Free tier limits | merged-with-debt | #198 | 1 (+doc fix) | skipped | #199 |
| #89 | [E8.S1] AI service skeleton (Python FastAPI) | merged-with-debt | #200 | 2 (+perf fix) | 6 / 1 | #201 |
| #90 | [E8.S2] Lesson content retrieval | merged-with-debt | #203 | 1 (+PRD fix) | skipped | #204 |
| #91 | [E8.S3] Avatar chat with context bundles | merged-with-debt | #210 | 2 (+main merge) | skipped | #211 |
| #92 | [E8.S4] Conversation logging | merged-with-debt | #214 | 1 (+main merge) | skipped | #215 |
| #94 | [E9.S1] Thread creation with attached context and quota | merged-with-debt | #206 | 1 (+hardening) | skipped | #207 |
| #95 | [E9.S2] Teacher inbox, claiming and text replies | merged-with-debt | #208 | 1 | skipped | #209 |
| #96 | [E9.S3] Voice replies with transcription | merged-with-debt | #216 | 2 (+main merge) | skipped | #217 |
| #97 | [E9.S4] SLA timers, reminders and follow-up rules | merged-with-debt | #221 | 1 (+hardening, main merge) | skipped | #222 |
| #99 | [E10.S1] Plan catalogue and subscription state | merged-with-debt | #186 | 1 | skipped | #187 |
| #100 | [E10.S2] Paymob checkout integration | merged-with-debt | #188 | 2 | 2 / 0 | #189 |
| #101 | [E10.S3] Webhook-driven entitlement | merged-with-debt | #190 | 1 (+doc fix) | skipped | #191 |
| #102 | [E10.S4] Refunds and payment log | merged-with-debt | #192 | 2 | skipped | #193 |
| #104 | [E11.S1] Dashboard metrics queries | merged | #226 | 2 | skipped | — |
| #105 | [E11.S2] Dashboard UI | merged-with-debt | #230 | 2 | 2 / 0 | #231 |
| #106 | [E11.S3] Student and teacher administration | merged-with-debt | #239 | 1 | skipped | #240 |
| #107 | [E11.S4] Teacher personal stats card | merged | #238 | 1 | no actionable comments | — |
| #109 | [E12.S1] Append-only training records | merged-with-debt | #225 | 2 | rate-limited | #229 |
| #110 | [E12.S2] JSONL export | merged-with-debt | #234 | 2 | skipped | #235 |
| #112 | [E13.S1] Hosting and environments | merged-with-debt | #202 | 2 (+main merge) | 1 / 0 | #205 |
| #113 | [E13.S2] Observability | merged-with-debt | #212 | 3 (+main merge) | skipped | #213 |
| #114 | [E13.S3] Performance targets | merged-with-debt | #219 | 2 (+CodeRabbit, CI fix) | 4 / 5 | #220 |
| #115 | [E13.S4] Security hardening | merged-with-debt | #243 | 1 (+Trivy Caddy bump) | 3 / 0 | #247 |
| #117 | [E14.S1] Essay question authoring with rubric | merged | #218 | 1 | 4 comments, triaged after the merge (orchestrator error) and fixed in #118 (PR #227) / 0 | — |
| #118 | [E14.S2] LLM essay grader | merged-with-debt | #227 | 2 | skipped | #228 |
| #119 | [E14.S3] Student essay input | merged-with-debt | #232 | 2 | skipped | #233 |
| #121 | [E15.S1] Math step input component | merged-with-debt | #223 | 1 | 1 / 0 | #224 |
| #122 | [E15.S2] CAS final answer check | merged-with-debt | #236 | 2 (+round-3 security fix, main merge) | skipped | #237 |
| #123 | [E15.S3] LLM step grading | merged-with-debt | #244 | 1 (+main merge) | skipped | #245 |
| #125 | [E16.S1] Admin diagram authoring tool | merged-with-debt | #241 | 2 (+2 main merges) | skipped | #242 |
| #126 | [E16.S2] Student canvas and grading | merged-with-debt | #246 | 2 (+2 main merges) | skipped | #248 |
| #128 | [E17.S1] Review queue and override | merged-with-debt | #249 | 2 (+main merge) | skipped | #250 |

Per-story plans, reviews, CodeRabbit triage and metrics are in `.process/<issue>-<slug>/`.

## 3. Issues opened during the run

All 59 were opened by the run between 2026-09-27 and 2026-09-30. Each follow-up issue names its story and PR. None of the `deferred` issues is closed yet, although later stories resolved some of their items (see the note at the end of this section).

### `story` (1)

| Issue | State | Title |
|---|---|---|
| #171 | closed (merged in #184) | [E1.S6] OTP delivery channels: WhatsApp (Meta), Email (Resend), SMS (disabled). Added on the dev's instruction of 2026-09-28. |

### `dev-decision` (3)

| Issue | State | Title |
|---|---|---|
| #135 | closed, confirmed by the dev | Confirm mobile tab bar: 3 items + More |
| #155 | closed, confirmed by the dev | Confirm: question retirement is final |
| #222 | **open, awaiting the dev** | Follow-ups from #97: SLA pause/refund/escalation decisions, hub token expiry, backplane |

### `deferred` (55, all open)

| Issue | Story | Title |
|---|---|---|
| #132 | #54 | Deferred from #54 backend bootstrap |
| #134 | #55 | Deferred from #55: visual screenshot tests |
| #137 | #56 | Deferred from #56 authentication |
| #139 | #57 | Map unique-violation to 409 centrally |
| #142 | #60 | Deferred from #60 subjects and units |
| #144 | #61 | Deferred from #61 lesson authoring |
| #146 | #62 | Deferred from #62 lesson lifecycle |
| #148 | #64 | Deferred from #64 question aggregate |
| #151 | #65 | Arabic normaliser edge cases, grader and editor nits |
| #153 | #66 | Import load-time bound, magic-byte check, test and doc nits |
| #156 | #67 | Rate-limit anonymous servable-count, single-flight fill, nits |
| #158 | #68 | xmin concurrency token on Question, backfill edges, 44px checkbox, dead session retry |
| #160 | #70 | Full normaliser idempotence, escape test literals, migration cast |
| #162 | #71 | Localizer resource assembly, Arabic default in code, small doc/test gaps |
| #164 | #72 | Strengthen T31 tolerance test, split TextGrader |
| #166 | #74 | Deterministic truncation test, replay createdAt assert, options message assert |
| #168 | #75 | Pin the 0.8 correctness boundary in selection tests |
| #170 | #76 | Quiz focus management, difficulty chip |
| #173 | #77 | Invalidation UI test, bar motion, doc/test nits |
| #175 | #78 | Scope/generator tests, dead kind code, empty-state wording |
| #177 | #80 | Row-error i18n key, JSON guard tests, empty-body doc |
| #179 | #81 | Lesson-open gate, expired-exam 409, race toast, CI flake |
| #181 | #82 | N+1 candidate draw, stale start error, nits |
| #183 | #83 | Split ExamsController, query-key prefix, gated attempts query |
| #185 | #171 | Provider go-live steps (Meta, Resend, telecom), test and doc nits |
| #187 | #99 | RawWebhook audit redaction and renew semantics, UI nits |
| #189 | #100 | Paymob live check, fake-gateway env lock, entitlement refresh |
| #191 | #101 | Paymob live check, webhook rate limit, grace-cancel wording, renewal day drift |
| #193 | #102 | Refund persistence after gateway success (priority), pending refunds, partial refunds |
| #195 | #85 | Layout test runner, nav link height, records-once test |
| #197 | #86 | Forwarded headers for rate limits, test-mode funnel event, nits |
| #199 | #87 | Next-lesson integration tests, Postman order, resume/opening tests |
| #201 | #89 | Live Claude smoke/eval, token prices, wrong-token test |
| #204 | #90 | OpenAI live check and Arabic eval, test gaps, rebuild race |
| #205 | #112 | First live deploy, off-site backups, placeholder-secret boot guard |
| #207 | #94 | S3 adapter in #96, request-size limit, UI and Postman nits |
| #209 | #95 | Out-of-app reply notification, claim edge cases, UI nits |
| #211 | #91 | Live eval run, exam-window filter test, avatar concurrency cap |
| #213 | #113 | AGPL confirmation, alert receivers, uptime monitor, Sentry |
| #215 | #92 | Retention/erasure decision, conversation load size, nits |
| #217 | #96 | Dialect WER eval, live Whisper/S3 check, S3 disposal, orphan media sweep |
| #220 | #114 | Lesson page p75 2.96 s over 2 s budget, CDN, full load run |
| #224 | #121 | Math step input |
| #228 | #118 | LLM essay grader |
| #229 | #109 | Training records |
| #231 | #105 | Dashboard UI |
| #233 | #119 | Student essay input |
| #235 | #110 | JSONL export |
| #237 | #122 | CAS final answer check |
| #240 | #106 | User administration |
| #242 | #125 | Diagram authoring |
| #245 | #123 | LLM step grading |
| #247 | #115 | Security hardening |
| #248 | #126 | Student canvas |
| #250 | #128 | Review queue and override |

Items already resolved by later stories, as those issues record: #187's items in #101; #207's S3 adapter in #96 (#217); #179's lesson-open gate in #85 (behind `Exams:RequireAllLessonsOpened`, off by default); #224's missing MathSteps type in #122; #237's CAS items in #123; the SMS adapter line of #137 in #171. The forwarded-headers item (#137, #197) is done in #112 (`docs/deployment.md` §11), and the placeholder-secret boot guard (#205) in #115 (`docs/deployment.md` §3). Tick those lines before closing the issues.

### Awaiting a dev decision

| Issue | Decision needed | What the run assumed |
|---|---|---|
| #222 | SLA clock pause (nights, weekends, holidays); refund or credit on a breach; escalation or reassignment; WhatsApp/email reminders | never pauses; no refund; alert only (`AskTeacherSlaBreached`); in-app reminders only |
| #213 | Grafana, Loki and Tempo are AGPL | acceptable, because they run unmodified as separate operator containers |
| #215 (also #229, #235) | Retention period and student erasure for avatar conversations and training records | kept indefinitely in v1, no purge or delete path; the privacy checklist is in `docs/training-data.md` |
| #209 | Tell the student about a teacher reply over WhatsApp or email | in-app only; WhatsApp needs a separately approved Meta template |
| #247 | Password policy | Identity defaults in `IdentityOptions` (8 characters, a digit) |
| #231 | Dashboard default period | the UI defaults to 14 days (prototype); the API default is 30 |
| #199 | Progress page for Free students (plan decision D12) | stays open to Free students |
| #132 | MediatR commercial licence | none bought; MediatR logs a licence warning |
| #137, #247 | Email confirmation, password reset, per-device sign-out | not in any story; sign-out ends sessions on every device |

### Go-live keys and hosting

These need credentials, accounts or a host that the run did not have. None can be done from code.

| Issue | What is needed |
|---|---|
| #185 | Meta WhatsApp authentication template and token; Resend verified domain and key; choice of Egyptian SMS telecom; a live smoke test of each adapter |
| #189, #191, #193 | A Paymob merchant account: the "verify before go-live" list in `docs/paymob.md` §5, then a test-mode payment and refund |
| #201, #211, #228, #245 | An Anthropic key: live avatar eval (22 cases), live essay-grading and math step-grading evals, confirm the model id `claude-sonnet-5` and the token prices |
| #204 | An OpenAI key: live check of `text-embedding-3-small` and the Arabic retrieval eval |
| #217 | An OpenAI key and recorded teacher clips: live Whisper check and the Egyptian-dialect WER eval; a live R2/S3 bucket check |
| #240 | The Resend key and `InvitationEmail__AcceptInviteUrl` for invitation emails |
| #205 | A VPS, domain, DNS, SSH key and the `DEPLOY_*` GitHub Environment secrets for the first deploy; an off-host bucket for backups |
| #213 | Real Alertmanager receivers, an external uptime monitor and dead-man's switch, optionally a Sentry DSN |
| #220 | The live domain for a CDN; staging hardware for the full 50-student load run |
| #247 | Repo-admin settings for Dependabot and CodeQL; a live HSTS/CSP check on the real domain; the `ai` container memory limit (host sizing) |

## 4. Fakes and how to switch each to real

Note: on hosts, `deploy/api.env.example` already sets `AiService__Provider=Http`, so the API always calls the ai service; the fakes that remain on a host are inside the ai service (its `ELMANHG_AI_*_PROVIDER` settings). `AiService:Provider=Fake` is the local and test default only.

Every external provider sits behind an interface with a fake or local implementation, selected by config. Fakes are the default, so development, tests and CI need no keys. Each real adapter is built and tested against a stubbed HTTP handler, but none has been run against the live provider. All values below are placeholders; set the real ones in the host's `api.env` / `ai.env` (`docs/deployment.md` §3–§4), never in the repo.

| Provider | Fake (where it lives) | Real adapter | Config keys | Switch steps |
|---|---|---|---|---|
| **Paymob** payments | `api/Elmanhg.Infrastructure/Payments/FakePaymentGateway.cs`, plus the web fake checkout page (`web/src/features/subscription/pages/FakeCheckoutPage.tsx`). Works in Development; elsewhere only with `Payments__AllowFakePayments=true`; Production always refuses it. | `Payments/Paymob/PaymobPaymentGateway.cs` (Intention API, HMAC webhook, refunds) | `Payments__Provider=Paymob`, `Payments__Paymob__SecretKey`, `__PublicKey`, `__HmacSecret`, `__IntegrationIds__0`, `__IntegrationIds__1`, `__RedirectionUrl`, `__NotificationUrl` | `docs/paymob.md` §7: create the card and wallet integrations, copy the keys and HMAC secret, set the keys, then work through §5 with a test-mode payment before taking money (#189, #191, #193) |
| **OTP: WhatsApp** (enabled) | `api/Elmanhg.Infrastructure/OtpDelivery/FakeOtpChannel.cs`: logs the code to the console in Development only, and a warning without the code elsewhere | `OtpDelivery/WhatsApp/MetaWhatsAppOtpChannel.cs` (Meta Cloud API template message) | `OtpDelivery__WhatsApp__Provider=Meta`, `__PhoneNumberId`, `__AccessToken`, `__TemplateName`, optionally `__CopyCodeButton=false` | `docs/otp-delivery.md` §5: Meta app and WhatsApp Business Account, an approved Arabic AUTHENTICATION template, a permanent system-user token, then the keys (#185) |
| **OTP: Email** (enabled) | same `FakeOtpChannel` | `OtpDelivery/Email/ResendEmailOtpChannel.cs` | `OtpDelivery__Email__Provider=Resend`, `__ApiKey`, `__FromAddress` | `docs/otp-delivery.md` §6: verify the sending domain in Resend, create a sending-only key, set the keys. A staging host needs at least this channel, because the fake cannot deliver codes there |
| **OTP: SMS** (built, disabled) | same `FakeOtpChannel` | `OtpDelivery/Sms/HttpSmsOtpChannel.cs` (generic HTTP gateway) | `OtpDelivery__Sms__Enabled=true`, `__Provider=Http`, `__Url`, `__ContentType`, `__BodyTemplate`, optionally `__AuthHeaderName` / `__AuthHeaderValue`; `OtpDelivery__DefaultPhoneChannel=Sms` to prefer it | `docs/otp-delivery.md` §7: pick the telecom, fill in its URL and body template. With WhatsApp on, SMS is only the fallback |
| **Invitation email** | `api/Elmanhg.Infrastructure/Invitations/FakeInvitationEmailSender.cs` (sends nothing; the dialog still shows the link) | `Invitations/ResendInvitationEmailSender.cs` | follows `OtpDelivery__Email__Provider=Resend`; also `InvitationEmail__AcceptInviteUrl=https://<SITE_ADDRESS>/accept-invite` | switch on Resend for OTP email and set the URL; the API refuses to start with Resend on and the URL missing or not `https` (#240) |
| **Claude API**: avatar chat, essay grading, math step grading | .NET: `api/Elmanhg.Infrastructure/AiService/FakeAiServiceClient.cs`, `FakeAiEssayGradingClient.cs` (full points, confidence 0.9), `FakeAiMathStepGradingClient.cs` (2 points per step), all selected by `AiService:Provider=Fake` and refusing in Production. Python: `ai/src/elmanhg_ai/clients/fake_model.py` | .NET `HttpAi*Client.cs` → the ai service → `ai/src/elmanhg_ai/clients/anthropic_model.py` | API: `AiService__Provider=Http`, `AiService__ServiceToken` (same value as `ELMANHG_AI_SERVICE_TOKEN`); the compose `ai` profile. ai: `ELMANHG_AI_LLM_PROVIDER=anthropic`, `ELMANHG_AI_ANTHROPIC_API_KEY`, `ELMANHG_AI_CHAT_MODEL`, `ELMANHG_AI_ESSAY_GRADING_MODEL`, `ELMANHG_AI_MATH_STEP_GRADING_MODEL` (all default `claude-sonnet-5`) | `docs/ai-service.md`, "Go live with Claude": set the keys, confirm the model id and prices, check `/health/ready`, then run the three evals (#201, #211, #228, #245) |
| **OpenAI embeddings** (lesson retrieval) | .NET `FakeEmbeddingVectors.cs`; Python `clients/fake_embedding.py` (deterministic lexical vectors) | `clients/openai_embedding.py` (`text-embedding-3-small`) | `ELMANHG_AI_EMBEDDING_PROVIDER=openai`, `ELMANHG_AI_OPENAI_API_KEY`, keep `ELMANHG_AI_EMBEDDING_DIMENSIONS=1536`; API `AiService__Provider=Http` | `docs/ai-service.md`, "Go live with OpenAI embeddings": set the keys, then as admin `POST /api/content-index/rebuild` so every Published lesson is re-embedded (#204) |
| **OpenAI Whisper** (voice-reply transcription) | .NET `FakeAiTranscriptionClient.cs`; Python `clients/fake_transcription.py` | `clients/openai_transcription.py` (`whisper-1`) | `ELMANHG_AI_TRANSCRIPTION_PROVIDER=openai`, `ELMANHG_AI_OPENAI_API_KEY` (same key); API `AiService__Provider=Http`, `AskTeacher__TranscriptionSweepEnabled=true` | `docs/ai-service.md`, "Go live with Whisper"; then the dialect eval once clips exist (#217) |
| **CAS final answer check** (MathSteps) | .NET `FakeAiMathCheckClient.cs` (string comparison) when `AiService:Provider=Fake` | the ai service runs SymPy locally; no external key | `AiService__Provider=Http`; `ELMANHG_AI_CAS_*` tuning | switches with the same `AiService__Provider=Http` |
| **Object storage** | `api/Elmanhg.Infrastructure/Storage/LocalDiskFileStorage.cs` (`App_Data/media`, the `api-media` volume on a host) | `Storage/S3FileStorage.cs` (Cloudflare R2 or AWS S3, private bucket, read through `/api/media`) | `FileStorage__Provider=S3`, `FileStorage__S3ServiceUrl` (empty for AWS), `__S3Region` (`auto` for R2), `__S3BucketName`, `__S3AccessKeyId`, `__S3SecretAccessKey`, `__S3ForcePathStyle` | `docs/deployment.md` §4, Object storage: create a private bucket and a key limited to it, stop the API, copy `App_Data/media` into the bucket with the same keys, set the keys, start the API. Turn on bucket versioning (#217) |
| **Hosting** | none live: the stack runs locally through `deploy/smoke-test.sh` | Docker Compose on a VPS (`deploy/docker-compose.prod.yml`), Caddy edge with automatic TLS, images pushed to GHCR by the `images` workflow on every push to main | host `.env` (`SITE_ADDRESS`, `IMAGE_TAG`, `POSTGRES_*`, `COMPOSE_PROFILES`), `api.env`, `ai.env`; GitHub Environment secrets `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `DEPLOY_KNOWN_HOSTS`, `DEPLOY_PATH` | `docs/deployment.md` §6–§7: provision the VPS and DNS, copy `deploy/`, fill in the env files (generate the secrets with the commands in §3), `bash deploy.sh sha-<7>` or run the `deploy` workflow. Set up the nightly backup cron and an off-host backup copy (#205) |
| **Observability receivers** | Alertmanager's default receiver has no integrations: alerts show in Grafana and Alertmanager but go nowhere | a host `alertmanager.yml` with email (for example Resend SMTP) or webhook receivers | `ALERTMANAGER_CONFIG_FILE`, `GRAFANA_ADMIN_PASSWORD`, `OTLP_ENDPOINT=http://otel-collector:4317`, `COMPOSE_PROFILES` with `observability`; optionally `Observability__OtlpHeaders` / `ELMANHG_AI_OTLP_HEADERS` for a SaaS backend | `docs/observability.md` §9: copy `deploy/observability/alertmanager/alertmanager.example.yml`, uncomment a receiver, route `severity="critical"` to it, restart `alertmanager`. Point an external uptime monitor at `https://<site>/api/health` (§10) (#213) |

Outside Development the API refuses to start while any secret still holds a `change-me` placeholder (`docs/deployment.md` §3), and the ai service refuses a `change-me` service token in production.

## 5. Running the system locally

### Prerequisites

| Tool | Version | Source |
|---|---|---|
| .NET SDK | 10.0.401 (`global.json`) | api-ci |
| Docker (Docker Desktop on Windows) | with Compose v2 | Postgres (`pgvector/pgvector:pg17`), Testcontainers in `dotnet test`, the smoke test |
| Node.js | 24 (`web/.nvmrc`) | web-ci |
| uv | 0.12.17, which provisions Python 3.13 (`ai/.python-version`) | ai-ci |
| Bash | Git Bash on Windows | `deploy/*.sh` |

If another project already uses port 5432, set `POSTGRES_PORT` (for example 55432) in `.env` and change the port in its connection string.

### Commands

The "Verified by" column says where each command is exercised: **CI** means a workflow in `.github/workflows/` runs it on every PR that touches that stack and on main, and every story merged with it green (main was green on all five workflows at `66b18ae`, and on every workflow that ran for the last story's merge at `0ca08ae`); **local** means the README's local-development step, which CI does not run. The table lists the main command of each check, not every step: the workflow files are the full list (for example coverage thresholds, `dotnet list package --vulnerable`, npm audit, readiness waits, generated-file drift and route-tree checks).

| Part | Command | Verified by |
|---|---|---|
| Config | `cp .env.example .env` and `cp api/Elmanhg.Api/appsettings.example.json api/Elmanhg.Api/appsettings.json` | local. After a story that adds config keys, copy the new sections again, or startup validation fails |
| Database | `docker compose up -d postgres` | local |
| Migrations | `dotnet tool restore`, then `dotnet ef database update --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api` | local; CI runs `dotnet ef migrations has-pending-model-changes` and `migrations script --idempotent` |
| API | `dotnet run --project api/Elmanhg.Api --launch-profile http`, then `curl http://localhost:5080/health` (→ `Healthy`); docs at `http://localhost:5080/scalar/v1` | local |
| API checks | `dotnet restore api/`, `dotnet build api/ -c Release --no-restore`, `dotnet test api/ -c Release --no-build`, `dotnet list api/ package --vulnerable --include-transitive` | CI (api-ci). CI has no `appsettings.json`; move yours aside to reproduce it |
| AI service | `cd ai`, `uv sync`, `uv run --env-file ../.env uvicorn elmanhg_ai.main:create_app --factory --reload --port 8000`, then `curl http://localhost:8000/health/ready`; or `docker compose --profile ai up -d --build ai` | local |
| AI checks | `uv sync --locked`, `uv run ruff format --check .`, `uv run ruff check .`, `uv run mypy src`, `uv run pytest -m "not eval"` | CI (ai-ci) |
| Web | `cd web`, `cp .env.example .env.local`, `npm ci`, `npm run dev` (http://localhost:5173, `/api` proxied to `http://localhost:5080`) | local |
| Web checks | `npm run typecheck`, `npm run lint`, `npm run format:check`, `npm test -- --run --coverage`, `npm run build`, `npm run perf:budget`, `npm audit --audit-level=high`; drift checks `npm run gen:api` and `npm run gen:tokens` | CI (web-ci). On Windows use `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` instead of `format:check` (CRLF noise) |
| Whole stack | `bash deploy/smoke-test.sh`: builds the three images and runs the production compose stack on `http://localhost:8088` (migrate, serve, backup, restore drill, observability) | CI (images workflow, `deploy-smoke` job) |
| Load test | `bash deploy/load-test.sh` | CI (images workflow, smoke profile; `load-test` workflow on demand) |

Sign in as the admin seeded from `AdminSeed__*` in `.env`. With the fake OTP channel, codes appear in the API console (Development only). With the fake payment gateway, checkout opens a simulated page with success and failure buttons.

To make the API use the ai service, set `AiService__Provider=Http`, `AiService__BaseUrl=http://localhost:8000` and the same token in `AiService__ServiceToken` and `ELMANHG_AI_SERVICE_TOKEN`.

## 6. Dev decisions made during the run

### Given by the dev

| Date | Decision |
|---|---|
| 2026-09-28 | Question retirement is final (#155 closed). Mobile tab bar is 3 items + «المزيد» (#135 closed). |
| 2026-09-28 | Fakes stay the default; every story also builds the real adapter, switched on by config: Paymob, Claude API, OpenAI Whisper, S3-compatible storage (R2 or S3, no MinIO; local disk for dev). |
| 2026-09-28 | OTP delivery (#171): WhatsApp through Meta Cloud API enabled, Email through Resend enabled, SMS through a generic HTTP adapter built but disabled; the channel is chosen by config. |
| 2026-09-28 | Hosting (#112): production Docker Compose, VPS-ready, Caddy TLS, images pushed to GHCR by CI, runbook in `docs/`; no live deploy. |
| 2026-09-28 | v2 epics E14–E17 are in scope. When CodeRabbit skips a PR (too many files or rate limit), that counts as no comments. |
| 2026-09-29 | Independent stories may run in parallel lanes (linked worktrees). |
| 2026-09-30 | Build credential-pending features now; keys come later (applied to the invitation email in #106). |

### Taken at the plan gate (auto-approved), changing product behaviour

Recorded in `.process/<issue>-<slug>/00-acceptance.md`. Each is written into the owning doc.

| Story | Decision |
|---|---|
| #62 | Lesson reorder and delete (PRD §10.1), missing from #61, were added to #62's scope. |
| #67 | A read-time servable filter (`ServableQuestionSpecification`) replaces the planned recalculation job. |
| #78 | The streak is student-wide; weak objectives are mastery-based. |
| #81 | Unit exam sizes follow the PRD (20/40/60); time limit and pass mark are question-weighted; a unit with no blueprint is refused. |
| #85 | The lesson-open exam gate (PRD §7.4) is built behind `Exams:RequireAllLessonsOpened`, off by default. |
| #86 | Onboarding is skippable, with no grade or track step. The anonymous funnel endpoint takes only whitelisted event names and an anonymous id. |
| #87 | The progress page stays open to Free students; the daily limit is soft; resuming is allowed. |
| #94 | Student photos are never public: `/api/media` serves them only to the owning student, a teacher scoped to the subject, or an admin. |
| #97 | SLA defaults: no pause, no refund, no escalation, in-app reminders only (open question #222). |
| #99 | A term is 4 months; prices are placeholders; cancelling keeps access until the period ends. |
| #101 | Renewal while entitled is allowed and extends the period; a duplicate plan purchase extends it; the renewal window is 7 days. |
| #102 | Full refunds only; PRD §17 rule 12 was amended so an admin refund confirmed by Paymob changes entitlement. |
| #104 | PRD §10.3: only Content, Solve rate, Success rate, Validation and Ask a Teacher take the subject filter; Students, Subscribers, Payments and the sign-up funnel have no subject dimension. |
| #106 | PRD §10.4, §11.2 and §17 rule 12: an admin can grant a complimentary Base or Ask a Teacher plan, besides Paymob events. The invitation email is built rather than deferred. |
| #110 | The JSONL export download is admin-only, audited and streamed through the API (no bearer link); exported files are deleted after 7 days. |
| #113 | `POST /api/client-errors` is rate-limited, size-capped and redacted; `/api/health` is public for an uptime monitor. |
| #114 | When the lesson-page budget is missed, the budget stays and a follow-up issue records the numbers (#220). |
| #115 | Refresh-token reuse detection (#137) and the pre-suspension token check (#240) were built, not deferred. The CSP lists every allowed origin, and the media origin is config-driven. |
| #117 | The rubric is a free criteria list per question (answers PRD §19 Q7). |
| #118 | Essays graded below confidence 0.7 go to teacher review; the student UI polls every 2 s. |
| #119 | Essay caps: only Essay answers get a larger raw cap (`Sessions:EssayAnswerMaxLength`, text up to `Content:QuestionEssayAnswerMaxLength` = 20000); every other type keeps the 4000-character raw cap. A concurrency retry reuses the stored grade and never calls the AI again. |
| #122 | Only MathSteps questions call the ai service. An ai outage never loses an answer or blocks an exam submit: the answer is stored and its check is `unchecked`, which #123 turned into pending (retried in the background, then teacher review) instead of a 503. |
| #123 | `stepsWeight` combine rule: normalised = ((100 − w) × F + w × S) ÷ 100, where F is 1 for an equivalent final answer and S is step points ÷ (2 × model steps) (`docs/math-step-grading.md`). |
| #125 | A diagram image is stored as a storage key under the diagram prefix, never an external URL, so no external host can be referenced. |
| #126 | DragDrop partial credit: score 1 when every keyed item is right and no distractor is placed, else max(0, (right − wrong) ÷ K); a keyed item in the wrong zone costs nothing extra (`docs/question-schemas.md`). |

## 7. Known gaps and risks

Taken from the open follow-up issues. The first group matters most before go-live.

| Issue | Gap or risk |
|---|---|
| #193 | **Money consistency:** after Paymob confirms a refund, the local save still uses the request's cancellation token, so a closed tab can leave Paymob refunded but the local payment and subscription unchanged. A `pending` refund is reported as declined; two partial refunds adding up to the full amount leave access granted. |
| #220 | The lesson page p75 cold load is 2.96 s against the 2 s budget (Lighthouse mobile). The CI browser gate is advisory until it is met. No CDN yet, and no full 50-student load run. |
| #233 | The Postman collection cannot run top to bottom: it unassigns the teacher and deletes the subject before later steps, and "Approve essay question" can fail as already approved. Not run by CI (newman). |
| #233 | Graded-but-unapplied essay grades are retried every sweep without a cap and can block all pending grading. |
| #237, #245, #233 | Unchecked or pending math answers and essays waiting for review are resolved by the #128 review queue: essays and math step grades that land in review are accepted or overridden by a teacher. Legacy #122 `unchecked` attempts (dev databases only) are not reviewable (#250). Pending answers do not count toward the free-tier daily quiz limit. The correct answer and explanation are sent for items awaiting review and hidden only in the UI. |
| #222 | The SignalR hub is in-memory: more than one API instance needs a backplane (Redis) or sticky sessions. Sockets are not closed when the JWT expires. |
| #205 | Backups stay on the VPS disk; there is no off-site copy. No live deploy has run yet. |
| #134, #137, #189, #195 | No browser or end-to-end tests: no Playwright, no visual screenshot baselines, no 375 px layout test. |
| #158 | No concurrency token on `Question`: an admin edit committing between load and save can still end Approved on an unseen version (millisecond window). |
| #139 | A PostgreSQL unique violation returns 500 instead of 409 in concurrent inserts (data stays correct). |
| #247 | Expired refresh-token rows are never purged. 17 HIGH image findings from Trivy remain (reported, not blocking). |
| #235, #215 | The training export does not scrub names typed in free text, or numbers such as a phone number in a numeric answer. Avatar conversations are kept indefinitely. |
| #132 | MediatR runs without a commercial licence key and logs a warning. No coverage threshold or `dotnet format` gate in CI. |
| #148, #146, #179 | Known flaky web tests: `RichTextEditor.test.tsx` "inserts an inline formula" and `ExamStartPage.test.tsx` under coverage. Re-run web-ci once before treating a failure as real. |
| #153 | A large sparse spreadsheet near the 5 MB cap can take minutes to load in ClosedXML (admin-only endpoint). |
| #207 | Uploads are fully buffered before the 5 MB check; unsent draft photos and audio stay on storage as orphans (#217). |
| #209 | A teacher unassigned from a subject loses access to threads they had already claimed there. |
