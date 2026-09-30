# Deployment and environments

How Elmanhg runs on a server: one Docker Compose stack per environment, one environment per host. Everything the host needs is in `deploy/`. The code arrives as images built by CI.

## 1. Overview

| Service | Image | Role | Public? | Volume | Health check |
|---|---|---|---|---|---|
| `web` | `elmanhg-web` (Caddy 2.10 as uid 10001 + the built SPA) | Terminates TLS, serves the SPA, proxies `/api/*` to `api:8080` | yes: 80, 443 (TCP + UDP) | `caddy-data`, `caddy-config` | `wget` on the internal `:2080/healthz` site |
| `api` | `elmanhg-api` | ASP.NET Core API | no (behind `web`) | `api-media` (`/app/App_Data/media`), `api-data-protection` | `curl http://127.0.0.1:8080/health` |
| `migrate` | `elmanhg-api` | One-shot: applies pending EF migrations, then exits | no | none | exit code 0 |
| `postgres` | `pgvector/pgvector:pg17` | PostgreSQL 17 with pgvector | no | `postgres-data` | `pg_isready` |
| `ai` (profile `ai`) | `elmanhg-ai` | Python AI service | no | none | `/health/ready` |
| `otel-collector` (profile `observability`) | `otel/opentelemetry-collector-contrib` | Receives OTLP from `api` and `ai`, tails every container's log file, redacts PII, ships to the three stores | no | `otel-collector-data` | none |
| `prometheus` (profile `observability`) | `prom/prometheus` | Metrics store (35 d), alert rules, blackbox scrapes | no | `prometheus-data` | `wget /-/ready` |
| `alertmanager` (profile `observability`) | `prom/alertmanager` | Routes alerts; reads the host file `alertmanager.yml` | no | `alertmanager-data` | `wget /-/ready` |
| `blackbox` (profile `observability`) | `prom/blackbox-exporter` | HTTP probes of edge, api and ai | no | none | `wget /-/healthy` |
| `loki` (profile `observability`) | `grafana/loki` | Log store (14 d) | no | `loki-data` | none (no probe tool in the image) |
| `tempo` (profile `observability`) | `grafana/tempo` | Trace store (7 d) | no | `tempo-data` | none (no probe tool in the image) |
| `grafana` (profile `observability`) | `grafana/grafana` | Dashboards | `127.0.0.1:${GRAFANA_PORT}` only (reach it with `ssh -L`) | `grafana-data` | `wget /api/health` |

Traffic: browser → Caddy `:443` → `/api/*` → `api:8080`; every other path → the SPA (`index.html` fallback). The API calls `ai:8000` with the shared service token. Postgres and the AI service are never public. `/api/health` is public (Caddy rewrites it to the API's `/health`, body `Healthy` or `Unhealthy` only) for an external uptime monitor. The observability stack is described in [docs/observability.md](observability.md).

Object storage is available from #96: a managed S3-compatible service (Cloudflare R2 or AWS S3), switched on with `FileStorage__Provider=S3` and the S3 keys in `api.env` (section 4, Object storage). It is never a container in this stack. The default is `Local`: local dev, CI and a host without S3 keys keep media in the `api-media` volume.

## 2. Environments

| | dev | staging | production |
|---|---|---|---|
| Where it runs | the developer's machine: `docker compose up -d postgres`, `dotnet run`, `npm run dev` | its own VPS, `deploy/docker-compose.prod.yml` | its own VPS, `deploy/docker-compose.prod.yml` |
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Staging` | `Production` |
| `ELMANHG_AI_ENV` | `development` | `production` | `production` |
| How it deploys | not deployed | `deploy` workflow or `bash deploy.sh <tag>` on the host | `deploy` workflow (with a required reviewer) or `bash deploy.sh <tag>` |
| Providers | fakes (OTP, payments, AI) | real OTP channel; Paymob test mode or the fake payment gateway; fake or real AI | all real |
| `Payments__AllowFakePayments` | not needed (Development allows the fake) | `true` only while staging runs `Payments__Provider=Fake` | ignored: Production always refuses the fake |

- Staging needs a real OTP channel (Email through Resend is enough). The fake OTP channel logs codes in Development only, so nobody can sign in to a staging host with it ([docs/otp-delivery.md](otp-delivery.md) §6).
- Staging exposes nothing extra: `/scalar` and `/openapi` are mapped outside Production, but Caddy proxies only `/api/*`.
- One environment per host: Caddy binds 80 and 443, so staging and production never share a machine.

## 3. Configuration and secrets

Three files sit next to `docker-compose.prod.yml` on the host, four with the `observability` profile. None is committed; the committed example files hold their shapes. Make each one `chmod 600`, owned by the deploy user.

| File | Read by | Holds |
|---|---|---|
| `.env` | Docker Compose (interpolation) | image registry and tag, site address, ports, network subnet, environment names, Postgres credentials, the env-file paths |
| `api.env` | the `api` and `migrate` containers | every API `Section__Key`: JWT, OTP, admin seed, OTP delivery, payments, AI client, content retrieval |
| `ai.env` | the `ai` container | every `ELMANHG_AI_*` |
| `alertmanager.yml` (profile `observability`) | the `alertmanager` container | alert receivers (email, webhook); copied from `observability/alertmanager/alertmanager.example.yml` ([docs/observability.md](observability.md), Alert delivery) |

The API never sees the AI provider keys (Anthropic, OpenAI), and the AI service never sees the JWT key.

Precedence inside the API container, highest first:
1. Compose `environment:` (computed values: connection string, trusted proxy network, AI base URL, file storage).
2. `api.env`.
3. The baked `/app/appsettings.json`, which is the committed `api/Elmanhg.Api/appsettings.example.json` (shapes and safe defaults, no secrets).

Never committed: `.gitignore` covers `.env`, `/deploy/api.env`, `/deploy/ai.env`, `/deploy/alertmanager.yml`, `/deploy/backups/` and `/deploy/.smoke/`. The root `.dockerignore` keeps a developer's `appsettings.json` and `.env` out of the API image.

Generating secrets:

| Secret | Command | Note |
|---|---|---|
| `POSTGRES_PASSWORD` | `openssl rand -hex 32` | hex only: it is embedded in the connection string |
| `CoreJwt__Key` | `openssl rand -base64 48` | |
| `CoreOtp__Secret` | `openssl rand -hex 32` | |
| `AiService__ServiceToken` = `ELMANHG_AI_SERVICE_TOKEN` | `openssl rand -hex 32` | the same value in both files |
| `TrainingData__StudentIdHashKey` | `openssl rand -hex 32` | set once; never rotate |

- Placeholder guard ([docs/security.md](security.md) §6): outside Development the API refuses to start (and to migrate) while any of `CoreJwt__Key`, `CoreOtp__Secret`, `AdminSeed__Password`, `TrainingData__StudentIdHashKey`, `AiService__ServiceToken`, `Payments__Paymob__SecretKey`, `Payments__Paymob__HmacSecret`, `OtpDelivery__WhatsApp__AccessToken`, `OtpDelivery__Email__ApiKey`, `OtpDelivery__Sms__AuthHeaderValue` or `FileStorage__S3SecretAccessKey` still starts with `change-me`, the connection string's parsed `Password` starts with `change-me`, or `CoreOtp__Secret` is empty. The error names the keys, never the values. The ai service refuses a `change-me` `ELMANHG_AI_SERVICE_TOKEN` when `ELMANHG_AI_ENV=production`. The smoke and load tests replace the example secrets with random ones (`replace_example_secrets` in `deploy/lib.sh`).

Rotation:

| Secret | Steps | Effect |
|---|---|---|
| JWT key | change `CoreJwt__Key` in `api.env`, then `docker compose -f docker-compose.prod.yml up -d api` | every access token becomes invalid, so everyone signs in again |
| AI service token | change both `api.env` and `ai.env`, then `docker compose -f docker-compose.prod.yml up -d` | none if both change together |
| Training hash key | never rotate | a new key gives every student a new pseudonym and splits their training history |
| Database password | first `ALTER USER elmanhg PASSWORD '<new>';` through `docker compose -f docker-compose.prod.yml exec postgres psql -U elmanhg`, then change `POSTGRES_PASSWORD` in `.env` and `up -d` | `POSTGRES_PASSWORD` only seeds a new data volume, so the database must change first |

A story that adds an options section holding a secret or a per-host value must add it to `deploy/api.env.example` and to this page. Otherwise the baked example default applies.

## 4. Environment variable reference

### Compose `.env`

| Variable | Required | Default | Notes |
|---|---|---|---|
| `COMPOSE_PROJECT_NAME` | yes | folder name | `elmanhg-prod` or `elmanhg-staging` |
| `COMPOSE_PROFILES` | no | empty | `ai` starts the AI service; `observability` starts the telemetry stack (`ai,observability` for both) |
| `IMAGE_REGISTRY` | no | `ghcr.io/mohamedebrahimmohsen` | |
| `IMAGE_TAG` | yes | none | `sha-<7>`; `deploy.sh` rewrites it |
| `SITE_ADDRESS` | yes | none | public host name; Caddy gets its certificate automatically. `:80` means plain HTTP (smoke test only) |
| `HTTP_PORT` / `HTTPS_PORT` | no | `80` / `443` | host ports |
| `DOCKER_SUBNET` | no | `172.30.0.0/24` | compose network; also the API's trusted proxy network (section 11) |
| `ASPNETCORE_ENVIRONMENT` | yes | none | `Staging` or `Production` |
| `ELMANHG_AI_ENV` | no | `production` | |
| `POSTGRES_DB` / `POSTGRES_USER` | yes | none | `elmanhg` |
| `POSTGRES_PASSWORD` | yes | none | hex (section 3) |
| `API_ENV_FILE` / `AI_ENV_FILE` | no | `api.env` / `ai.env` | paths relative to the compose file |
| `BACKUP_DIR` / `BACKUP_RETENTION_DAYS` | no | `backups` / `14` | read by `backup.sh` from the shell, not from `.env` |
| `OTLP_ENDPOINT` | no | empty | OTLP/gRPC endpoint for `api` and `ai` traces and metrics; empty exports nothing. With the profile: `http://otel-collector:4317` |
| `GRAFANA_ADMIN_PASSWORD` | with the profile | none | `openssl rand -hex 16`; `deploy.sh` refuses the profile without it |
| `GRAFANA_PORT` | no | `3000` | loopback port Grafana listens on |
| `ALERTMANAGER_CONFIG_FILE` | no | `alertmanager.yml` | path of the Alertmanager config, relative to the compose file; `deploy.sh` refuses the profile when it is missing |
| `MEDIA_ORIGIN` | no | empty | extra origin for images and audio in the SPA's Content-Security-Policy (Caddy). Leave empty while media is served from `/api/media` (the default, also with `S3`); set it (e.g. `https://media.example.com`) only if `FileStorage__PublicBaseUrl` points at another origin ([docs/security.md](security.md) §4) |

### API identity and seed (`api.env`)

| Variable | Required | Default | Notes |
|---|---|---|---|
| `CoreJwt__Issuer` / `CoreJwt__Audience` | yes | empty | `Elmanhg` / `Elmanhg.Audience` |
| `CoreJwt__Key` | yes | empty | secret |
| `CoreJwt__ExpirationHours` / `CoreJwt__RefreshTokenExpirationDays` | no | `1` / `7` | |
| `CoreOtp__Secret` | yes | empty | secret; HMAC key of OTP codes |
| `TrainingData__StudentIdHashKey` | yes (every environment except Development and Testing) | empty | secret; HMAC key of student ids in training records; the API refuses to start without it outside Development and Testing |
| `AdminSeed__Email` / `AdminSeed__Password` / `AdminSeed__DisplayName` | first start | empty | the admin is created on the first start when missing; an empty email skips the seed |
| `CoreLogging__Trace__Cluster` | no | `Local` | log label, for example `production` |
| `LoadTestSeed__Key` / `LoadTestSeed__StudentCount` / `LoadTestSeed__StudentPassword` | load-test stacks only | `loadtest` / `60` / empty | read only by `--SeedLoadTestAndExit=true`, which `deploy/load-test.sh` runs and which refuses Production ([docs/performance.md](performance.md) §5); never set them on a real host |

### OTP delivery (`api.env`, [docs/otp-delivery.md](otp-delivery.md))

| Variable | Default | Secret |
|---|---|---|
| `OtpDelivery__DefaultPhoneChannel` | `WhatsApp` | no |
| `OtpDelivery__CountryCallingCode` | `20` | no |
| `OtpDelivery__AttemptTimeoutSeconds` / `OtpDelivery__TotalTimeoutSeconds` | `10` / `30` | no |
| `OtpDelivery__WhatsApp__Enabled` / `__Provider` / `__BaseUrl` / `__ApiVersion` | `true` / `Fake` / `https://graph.facebook.com` / `v23.0` | no |
| `OtpDelivery__WhatsApp__PhoneNumberId` / `__TemplateName` / `__LanguageCode` / `__CopyCodeButton` | empty / empty / `ar` / `true` | no |
| `OtpDelivery__WhatsApp__AccessToken` | empty | **yes** |
| `OtpDelivery__Email__Enabled` / `__Provider` / `__BaseUrl` | `true` / `Fake` / `https://api.resend.com` | no |
| `OtpDelivery__Email__FromAddress` / `__Subject` | empty / Arabic default | no |
| `OtpDelivery__Email__ApiKey` | empty | **yes** |
| `OtpDelivery__Sms__Enabled` / `__Provider` / `__Url` / `__ContentType` | `false` / `Fake` / empty / `application/json` | no |
| `OtpDelivery__Sms__AuthHeaderName` / `__BodyTemplate` / `__MessageTemplate` | empty / empty / Arabic default | no |
| `OtpDelivery__Sms__AuthHeaderValue` | empty | **yes** |

### Users and invitations (`api.env`, [docs/user-administration.md](user-administration.md))

| Variable | Default | Secret |
|---|---|---|
| `Users__ListMaxPageSize` / `Users__SearchMaxLength` | `100` / `256` | no |
| `Users__ActiveStatusCacheSeconds` | `30` | no; 0 to 300, how long a suspension can take to reach another API instance; 0 checks the database on every request |
| `InvitationEmail__AcceptInviteUrl` | empty | no; `https://<SITE_ADDRESS>/accept-invite`, required when `OtpDelivery__Email__Provider=Resend` |
| `InvitationEmail__Subject` | Arabic default | no |

The invitation email is sent through the same Resend account as the email OTP (`OtpDelivery__Email__ApiKey` and `__FromAddress`). With the fake email provider no invitation email leaves the API, and the admin shares the link shown after the invite.

### Payments (`api.env`, [docs/paymob.md](paymob.md))

| Variable | Default | Secret |
|---|---|---|
| `Payments__Provider` | `Fake` | no |
| `Payments__AllowFakePayments` | `false` | no; lets the fake run outside Development, ignored in Production |
| `Payments__FakeCheckoutPath` | `/student/fake-checkout` | no |
| `Payments__AttemptTimeoutSeconds` / `Payments__TotalTimeoutSeconds` | `10` / `30` | no |
| `Payments__Paymob__BaseUrl` / `__CheckoutUrl` / `__BillingCountry` | Paymob Egypt / `EG` | no |
| `Payments__Paymob__PublicKey` / `__IntegrationIds__0` / `__IntegrationIds__1` | empty | no |
| `Payments__Paymob__RedirectionUrl` | empty | no; `https://<SITE_ADDRESS>/student/checkout-result` |
| `Payments__Paymob__NotificationUrl` | empty | no; `https://<SITE_ADDRESS>/api/payments/paymob/webhook` |
| `Payments__Paymob__SecretKey` / `__HmacSecret` | empty | **yes** |

### AI client (`api.env`, [docs/ai-service.md](ai-service.md))

| Variable | Default | Notes |
|---|---|---|
| `AiService__Provider` | `Fake` | `Http` calls the `ai` service |
| `AiService__ServiceToken` | empty | secret; same value as `ELMANHG_AI_SERVICE_TOKEN` |
| `AiService__AttemptTimeoutSeconds` / `AiService__TotalTimeoutSeconds` | `45` / `50` | |
| `AiService__BaseUrl` | set by compose | `http://ai:8000` |

### Content retrieval (`api.env`, [docs/content-retrieval.md](content-retrieval.md))

None is a secret; the baked defaults suit staging and production. Validated at startup, so a value out of range stops the API.

| Variable | Default | Notes |
|---|---|---|
| `ContentRetrieval__IndexSweepEnabled` | `true` | the background sweep that embeds Published lessons through the `ai` service |
| `ContentRetrieval__IndexSweepIntervalSeconds` / `ContentRetrieval__IndexSweepBatchSize` | `30` / `20` | 5 to 86400 / 1 to 500 |
| `ContentRetrieval__ChunkMaxCharacters` | `1500` | 200 to 6000 |
| `ContentRetrieval__EmbeddingBatchSize` | `32` | 1 to 64; at most `ELMANHG_AI_EMBEDDING_MAX_TEXTS` |
| `ContentRetrieval__DefaultTopK` / `ContentRetrieval__MaxTopK` | `5` / `20` | 1 to 50; the default must not exceed the max |
| `ContentRetrieval__QueryMaxLength` | `2000` | 1 to 4000 |

The sweep calls the embeddings endpoint, so with `AiService__Provider=Http` the `ai` profile must be on (`COMPOSE_PROFILES=ai`); otherwise failed lessons are logged as warnings and retried by later sweeps.

### AI Avatar (`api.env`, [docs/avatar.md](avatar.md))

None is a secret; the baked defaults suit staging and production. Validated at startup, so a value out of range stops the API.

| Variable | Default | Notes |
|---|---|---|
| `Avatar__MessageMaxLength` | `2000` | 1 to 4000; at most `ELMANHG_AI_CHAT_MAX_MESSAGE_CHARS` |
| `Avatar__HistoryTurnMaxLength` | `4000` | 1 to 4000; each stored turn is cut to this when sent back as history |
| `Avatar__MaxHistoryMessages` | `10` | 0 to 20, even; at most `ELMANHG_AI_CHAT_MAX_HISTORY_MESSAGES` |
| `Avatar__ContextFieldMaxLength` | `8000` | 500 to 8000; keeps the context bundle under `ELMANHG_AI_CHAT_MAX_CONTEXT_CHARS` |
| `Avatar__AdminConversationsMaxPageSize` | `100` | 1 to 200 |
| `Avatar__ConversationSearchMaxLength` | `200` | 1 to 500 |

The daily message limits (Free 5, Base 50) are `Subscriptions__FreeDailyAvatarMessages` and `Subscriptions__BaseDailyAvatarMessages`.

### Rate limits (`api.env`, [docs/security.md](security.md))

Per-IP limits for anonymous endpoints and per-student limits for the costly student actions; each answers 429 `TOO_MANY_REQUESTS` over the limit. The limits are in memory per API instance. The auth, analytics and client-error limits keep their own sections (`Auth__*`, `Analytics__*`, `ClientErrors__*`).

| Variable | Default | Notes |
|---|---|---|
| `RateLimiting__AuthRefreshPermitLimit` / `RateLimiting__AuthRefreshWindowSeconds` | `60` / `60` | `POST /api/auth/refresh`, per IP |
| `RateLimiting__PublicReadPermitLimit` / `RateLimiting__PublicReadWindowSeconds` | `300` / `60` | `GET /api/questions/servable-count` and `GET /api/plans`, one shared bucket per IP |
| `RateLimiting__PaymentWebhookPermitLimit` / `RateLimiting__PaymentWebhookWindowSeconds` | `300` / `60` | the Paymob webhook, per IP |
| `RateLimiting__AvatarMessagePermitLimit` / `RateLimiting__AvatarMessageWindowSeconds` | `20` / `60` | `POST /api/avatar/messages`, per student |
| `RateLimiting__AskTeacherSubmissionPermitLimit` / `RateLimiting__AskTeacherSubmissionWindowSeconds` | `10` / `600` | Ask a Teacher create and follow-up, per student |
| `RateLimiting__StudentConcurrentRequestLimit` | `1` | requests in flight per student on the Avatar and on Ask a Teacher submissions |
| `Auth__RefreshTokenReuseGraceSeconds` | `10` | a rotated refresh cookie sent again within this window gets a new token (tabs refreshing together); after it, the replay revokes the whole sign-in |

### Ask a Teacher (`api.env`, [docs/ask-teacher.md](ask-teacher.md))

None is a secret; the baked defaults suit staging and production. Validated at startup, so a value out of range stops the API.

| Variable | Default | Notes |
|---|---|---|
| `AskTeacher__QuestionTextMaxLength` | `2000` | 1 to 20000 |
| `AskTeacher__ImageMaxSizeInMb` | `5` | 1 to 20 |
| `AskTeacher__ThreadListMaxPageSize` | `50` | 1 to 100 |
| `AskTeacher__ReplyTextMaxLength` | `4000` | 1 to 20000 |
| `AskTeacher__VoiceMaxSizeInMb` / `AskTeacher__VoiceMaxDurationSeconds` | `5` / `180` | 1 to 25 / 10 to 600 |
| `AskTeacher__TranscriptionLanguage` | `ar` | two lower-case letters |
| `AskTeacher__TranscriptionSweepEnabled` | `true` | the voice transcription worker |
| `AskTeacher__TranscriptionSweepIntervalSeconds` / `AskTeacher__TranscriptionSweepBatchSize` | `5` / `5` | 1 to 3600 / 1 to 100 |
| `AskTeacher__TranscriptionMaxAttempts` / `AskTeacher__TranscriptionRetryBaseDelaySeconds` | `4` / `15` | 1 to 10 / 1 to 3600 (retries at 15, 30, 60 s) |
| `AskTeacher__SlaSweepEnabled` | `true` | the `ask-teacher-sla` reminder and breach worker |
| `AskTeacher__SlaSweepIntervalSeconds` / `AskTeacher__SlaSweepBatchSize` | `60` / `50` | 1 to 3600 / 1 to 500 |
| `AskTeacher__FirstReminderAfterHours` / `AskTeacher__SecondReminderAfterHours` | `12` / `20` | 1 to 168 each, hours into the reply window; startup fails unless first < second < `Subscriptions__AskTeacherReplySlaHours` |
| `AskTeacher__ReminderListMaxCount` | `20` | 1 to 100, the «تذكيرات» card on the teacher inbox |
| `AiService__TranscriptionTimeoutSeconds` | `150` | 1 to 600; above the AI service's worst case (about 121 s) |
| `AiService__EssayGradingTimeoutSeconds` | `100` | 1 to 600; above the AI service's essay-grading worst case (about 91 s) |
| `AiService__MathCheckTimeoutSeconds` | `15` | 1 to 120; the math final-answer check's attempt and total timeout (no POST retry). A timeout or outage grades the answer «unchecked» for teacher review ([docs/math-cas.md](math-cas.md)) |
| `Sessions__MathStepsAnswerMaxLength` / `Sessions__MathStepsMaxCount` / `Sessions__MathStepMaxLength` / `Sessions__MathFinalAnswerMaxLength` | `24000` / `20` / `500` / `200` | raw JSON cap and caps on a math-with-steps answer (422 `ATTEMPT_ANSWER_TOO_LONG`) |
| `EssayGrading__SweepEnabled` / `EssayGrading__ReviewConfidenceThreshold` | `true` / `0.7` | the essay-grading worker, and the confidence below which a teacher reviews the grade ([docs/essay-grading.md](essay-grading.md)) |
| `MathStepGrading__SweepEnabled` / `MathStepGrading__ReviewConfidenceThreshold` / `MathStepGrading__CheckPermitLimit` | `true` / `0.7` / `10` | the math-step-grading worker, the confidence below which a teacher reviews a step grade, and the quiz math checks per student per minute ([docs/math-step-grading.md](math-step-grading.md)) |
| `TrainingExports__SweepEnabled` / `TrainingExports__SweepIntervalSeconds` / `TrainingExports__SweepBatchSize` | `true` / `15` / `2` | the `training-export` worker that writes JSONL files ([docs/training-data.md](training-data.md), Export); 1 to 3600 / 1 to 20 |
| `TrainingExports__MaxAttempts` / `TrainingExports__RetryBaseDelaySeconds` | `3` / `60` | 1 to 10 / 1 to 3600; after the last failure the export is `Failed` |
| `TrainingExports__RunLeaseMinutes` | `30` | 1 to 1440; a started run holds its export for this long, so no other replica picks it up; a run that crashed is retried after it |
| `TrainingExports__ReadBatchSize` / `TrainingExports__MaxRangeDays` / `TrainingExports__ListMaxPageSize` | `500` / `366` / `50` | 10 to 5000 rows per page read / 1 to 3660 / 1 to 100 |
| `TrainingExports__RetentionDays` | `7` | 1 to 365; completed export files are deleted after this many days |
| `TrainingExports__RetentionSweepEnabled` / `TrainingExports__RetentionSweepIntervalSeconds` / `TrainingExports__RetentionSweepBatchSize` | `true` / `3600` / `20` | the `training-export-retention` worker; 1 to 86400 / 1 to 100 |
| `Subscriptions__AskTeacherMonthlyQuestions` / `Subscriptions__AskTeacherReplySlaHours` | `20` / `24` | the add-on's monthly quota and reply SLA ([docs/subscriptions.md](subscriptions.md)) |

Question photos and teachers' voice replies are stored under `teacher-threads/` (in the `api-media` volume with `Local`, in the bucket with `S3`) and are private: the API serves them only to the owning student, a teacher of the subject or an admin. Caddy proxies all of `/api/*` to the API, so never serve `/api/media` straight from the volume or the bucket at the edge. With `Local`, the media backup (section 9) includes them. Voice replies are transcribed by the API's background worker through the AI service, so with `AiService__Provider=Http` the `ai` profile must be on; otherwise the drafts fail after their retries and the teacher types the text.

Training export files are stored under `training-exports/` (the `api-media` volume with `Local`, the bucket with `S3`) and are private: `/api/media` never serves them, and only an admin downloads them through `GET /api/training-exports/{id}/file`. The retention worker deletes them after `TrainingExports__RetentionDays`. The worker writes each file to the container's temp directory first, so the API container needs temp space for the largest export.

Caddy proxies the SignalR hub `/api/hubs/notifications` (WebSockets) with the existing `/api/*` rule; the access log already redacts the `access_token` query value.

### Dashboard (`api.env`, [docs/dashboard.md](dashboard.md))

None is a secret; the baked defaults suit staging and production. Validated at startup, so a value out of range, an unknown time zone or a default range above the maximum stops the API.

| Variable | Default | Notes |
|---|---|---|
| `Dashboard__TimeZone` | `Africa/Cairo` | IANA time zone id for day boundaries (`Cairo` alone is rejected) |
| `Dashboard__CacheSeconds` | `60` | 0 to 3600; per-card in-memory cache, `0` disables it |
| `Dashboard__DefaultRangeDays` / `Dashboard__MaxRangeDays` | `30` / `366` | 1 to 3650 each; the default must not exceed the max |
| `Dashboard__RecentWeekDays` | `7` | 1 to 31, the "new this week" window |
| `Dashboard__RecentMonthDays` | `30` | 1 to 366, the MAU and "churned this month" window |

### Object storage (`api.env`, R2 or S3)

| Variable | Default | Notes |
|---|---|---|
| `FileStorage__Provider` | `Local` | `S3` to use the bucket |
| `FileStorage__S3ServiceUrl` | empty | the S3 endpoint, an absolute `https` URL, e.g. `https://<account>.r2.cloudflarestorage.com` for R2; empty for AWS S3 (the region picks the endpoint) |
| `FileStorage__S3Region` | `auto` | `auto` for R2; the bucket's region (e.g. `eu-central-1`) for AWS |
| `FileStorage__S3BucketName` | empty | required for `S3` |
| `FileStorage__S3AccessKeyId` / `FileStorage__S3SecretAccessKey` | empty | secrets; required for `S3`. Give the key read and write access to this bucket only |
| `FileStorage__S3ForcePathStyle` | `true` | path-style addressing, which R2 and MinIO expect |

- The API checks these at startup, so a missing key stops it.
- The bucket stays **fully private**: no public access, no public bucket URL and no CORS rules. Every read goes through the API (`/api/media/...`), which checks access for private media and streams public lesson images with a one-year cache header. There are no presigned URLs.
- Switching an existing host from `Local` to `S3`: stop the API, copy `App_Data/media` from the `api-media` volume into the bucket with the same keys (for example `aws s3 sync` or `rclone copy`), set the variables, then `up -d`. Stored URLs (`/api/media/...`) do not change, so no data migration is needed.
- Turn on bucket versioning where the provider supports it; it is the recovery path for deleted or overwritten objects.

### AI service (`ai.env`, [docs/ai-service.md](ai-service.md))

| Variable | Default | Notes |
|---|---|---|
| `ELMANHG_AI_SERVICE_TOKEN` | required | secret, at least 32 characters |
| `ELMANHG_AI_LLM_PROVIDER` | `fake` | `anthropic` to go live |
| `ELMANHG_AI_ANTHROPIC_API_KEY` | unset | secret; required for `anthropic` |
| `ELMANHG_AI_CHAT_MODEL` | `claude-sonnet-5` | |
| `ELMANHG_AI_LOG_LEVEL` / `ELMANHG_AI_LOG_FORMAT` | `INFO` / `json` | |
| `ELMANHG_AI_CHAT_PROMPT_VERSION` | `v2` | the production Avatar prompt; `v1` is kept for history |
| `ELMANHG_AI_CHAT_MAX_SOURCES` / `ELMANHG_AI_CHAT_MAX_SOURCE_CHARS` | `20` / `8000` | retrieved lesson chunks per message / per chunk |
| `ELMANHG_AI_CHAT_MAX_TOKENS` / `_MAX_HISTORY_MESSAGES` / `_MAX_MESSAGE_CHARS` / `_MAX_CONTEXT_CHARS` | `1024` / `20` / `4000` / `60000` | |
| `ELMANHG_AI_MODEL_TIMEOUT_SECONDS` / `ELMANHG_AI_MODEL_MAX_RETRIES` | `20` / `1` | |
| `ELMANHG_AI_MODEL_INPUT_USD_PER_MILLION_TOKENS` / `_OUTPUT_USD_PER_MILLION_TOKENS` | `3` / `15` | cost logging and the `costUsd` stored per reply |
| `ELMANHG_AI_EMBEDDING_PROVIDER` | `fake` | `openai` to go live |
| `ELMANHG_AI_OPENAI_API_KEY` | unset | secret; required for `openai` |
| `ELMANHG_AI_EMBEDDING_MODEL` | `text-embedding-3-small` | |
| `ELMANHG_AI_EMBEDDING_DIMENSIONS` | `1536` | keep `1536`: it is the width of the API's `vector(1536)` column |
| `ELMANHG_AI_EMBEDDING_MAX_TEXTS` / `ELMANHG_AI_EMBEDDING_MAX_TEXT_CHARS` | `64` / `8000` | per call / per text |
| `ELMANHG_AI_EMBEDDING_USD_PER_MILLION_TOKENS` | `0.02` | cost logging only |
| `ELMANHG_AI_TRANSCRIPTION_PROVIDER` | `fake` | `openai` (Whisper) to go live; needs `ELMANHG_AI_OPENAI_API_KEY` |
| `ELMANHG_AI_TRANSCRIPTION_MODEL` | `whisper-1` | |
| `ELMANHG_AI_TRANSCRIPTION_TIMEOUT_SECONDS` | `60` | per call; with one retry it stays under `AiService__TranscriptionTimeoutSeconds` |
| `ELMANHG_AI_TRANSCRIPTION_MAX_AUDIO_BYTES` / `ELMANHG_AI_TRANSCRIPTION_MAX_DURATION_SECONDS` | `10485760` / `600` | per recording |
| `ELMANHG_AI_TRANSCRIPTION_USD_PER_MINUTE` | `0.006` | cost logging only |
| `ELMANHG_AI_ESSAY_GRADING_MODEL` | `claude-sonnet-5` | essay grading uses the Claude provider and key above |
| `ELMANHG_AI_ESSAY_GRADING_TIMEOUT_SECONDS` | `45` | per call; with one retry (about 91 s) it stays under `AiService__EssayGradingTimeoutSeconds` |
| `ELMANHG_AI_MATH_STEP_GRADING_MODEL` / `ELMANHG_AI_MATH_STEP_GRADING_TIMEOUT_SECONDS` | `claude-sonnet-5` / `45` | math step grading uses the Claude provider and key above; with one retry it stays under `AiService__MathStepGradingTimeoutSeconds` (100) |
| `ELMANHG_AI_CAS_MAX_EXPANSION_TERMS` | `500` | the CAS rejects an answer whose expansion would exceed this many terms as unreadable ([docs/math-cas.md](math-cas.md)) |
| `ELMANHG_AI_CAS_TIMEOUT_SECONDS` | `5` | hard timeout of one SymPy check; on a timeout only the offending worker slot is killed and restarted in the background, and the other slots are untouched ([docs/math-cas.md](math-cas.md)) |
| `ELMANHG_AI_CAS_WORKERS` / `ELMANHG_AI_CAS_WORKER_MEMORY_MB` | `2` / `1024` | CAS worker processes and their address-space cap (POSIX) |
| `ELMANHG_AI_ENV` | set by compose | from `.env` |

Switching the embedding provider or model (for example `fake` to `openai`) needs a re-index: after `up -d`, call `POST /api/content-index/rebuild` as an admin (Postman, `ContentRetrieval` folder). Until each lesson is re-embedded by the sweep, its search returns no matches, because old-model chunks are never compared with a new-model query ([docs/content-retrieval.md](content-retrieval.md), Rebuild).

### Observability (`api.env`, [docs/observability.md](observability.md))

| Variable | Default | Notes |
|---|---|---|
| `Observability__OtlpHeaders` | empty | secret; `key=value` pairs for a SaaS OTLP endpoint (for example `authorization=Bearer <token>`) |
| `Observability__TraceSampleRatio` | `1.0` | share of new traces kept (0 to 1); child spans follow the caller's decision |
| `Observability__MetricExportIntervalSeconds` | `30` | 5 to 3600 |
| `ClientErrors__PermitLimit` / `ClientErrors__WindowSeconds` | `30` / `60` | browser error reports allowed per IP per window |
| `ClientErrors__MessageMaxLength` / `ErrorNameMaxLength` / `StackMaxLength` / `PathMaxLength` | `500` / `100` / `4000` / `300` | size caps; the web app truncates to the same values |

The AI service's telemetry keys are in [docs/ai-service.md](ai-service.md), Configuration.

### Set by compose (do not put these in `api.env`)

| Variable | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | from `.env` |
| `ConnectionStrings__DbConnectionString` | `Host=postgres;…` built from the `POSTGRES_*` values |
| `ReverseProxy__TrustedNetworks__0` | `DOCKER_SUBNET` |
| `FileStorage__LocalRootPath` / `FileStorage__PublicBaseUrl` | `/app/App_Data/media` / `/api/media` |
| `Observability__OtlpEndpoint` / `ELMANHG_AI_OTLP_ENDPOINT` | `OTLP_ENDPOINT` from `.env` |
| `Observability__ServiceVersion` / `ELMANHG_AI_SERVICE_VERSION` | `IMAGE_TAG` |
| `CoreLogging__Console__Format` | `Json` (one JSON object per log line) |
| `OTEL_SEMCONV_STABILITY_OPT_IN` (ai) | `http` |

`FileStorage__Provider` is not set by compose, so `api.env` can switch it to `S3`; the baked default is `Local`.

Every other key in `appsettings.example.json` can be overridden in `api.env` as `Section__Key`.

## 5. Images and CI

- The `images` workflow (`.github/workflows/images.yml`) runs `deploy-smoke` on every pull request that touches `api/`, `web/`, `ai/`, `deploy/`, `global.json` or `.dockerignore`. It builds the three images and runs `deploy/smoke-test.sh`.
- On every push to main (and on a manual run on main), after the smoke test passes, it pushes `ghcr.io/<owner>/elmanhg-api`, `elmanhg-web` and `elmanhg-ai`, each tagged `sha-<7>` and `main`. Only that job has `packages: write`.
- Images are linux/amd64 only.
- Every base and third-party image is pinned as `name:tag@sha256:<digest>`: each `FROM` (and the `uv` `COPY --from`) in `api/`, `web/` and `ai/` `Dockerfile`, and `postgres` and every observability image in `docker-compose.prod.yml`. The tag is for readers; the digest is what Docker pulls. To bump one, run `docker buildx imagetools inspect <name>:<tag>`, copy the top-level `Digest:` line (the multi-platform index, not a per-platform manifest), replace the old digest, and let the `images` workflow smoke-test the change in a pull request. Never write a digest by hand.
- Main runs never cancel each other (one concurrency group per commit), so every main commit gets a full set of images. Pull request runs cancel the older run on the same branch.
- Deploy `sha-<7>` tags, never `main`, so a rollback names an exact build.
- After the smoke test, the `deploy-smoke` job installs Trivy 0.74.0 (SHA-256-verified) and scans the three `local/elmanhg-*:smoke` images for vulnerabilities with a fix available (`--ignore-unfixed`): a CRITICAL one fails the job, HIGH ones are reported only. Fix a failure by bumping the base image digest as above ([docs/security.md](security.md) §7).
- The `security` workflow (`.github/workflows/security.yml`) runs gitleaks 8.30.1 (SHA-256-verified) over the full git history on every pull request and push to main, and a weekly (Monday 04:17 UTC) or manual audit of the .NET, npm and Python dependencies ([docs/security.md](security.md) §6–§7).
- If a GHCR package is private, log the host in once: `docker login ghcr.io -u <user>` with a personal access token that has `read:packages`.

## 6. First-time host setup

1. VPS: Ubuntu 24.04, at least 2 vCPU and 4 GB RAM (8 GB recommended with the `observability` profile, which needs about 1.5 GB). Install Docker Engine with the Compose v2.24+ plugin.
2. DNS: an A (and AAAA) record for the site name pointing at the host. Firewall: allow 22, 80 and 443 only.
3. `sudo mkdir -p /opt/elmanhg && sudo chown deploy:deploy /opt/elmanhg`.
4. Copy `deploy/docker-compose.prod.yml`, `Caddyfile`, `lib.sh`, `deploy.sh`, `backup.sh`, `restore.sh` and the `observability/` folder there. Create `.env`, `api.env` and `ai.env` from the examples, fill in the secrets (section 3), and `chmod 600 .env api.env ai.env`. With the `observability` profile, also `cp observability/alertmanager/alertmanager.example.yml alertmanager.yml && chmod 600 alertmanager.yml`, set `GRAFANA_ADMIN_PASSWORD` and `OTLP_ENDPOINT=http://otel-collector:4317` in `.env` ([docs/observability.md](observability.md), Turning it on).
5. `docker login ghcr.io` (section 5), if the packages are private.
6. `bash deploy.sh sha-<7>`.
7. Open `https://<site>` and sign in as the seeded admin.
8. Install the nightly backup cron (section 9).

## 7. Deploying

### Deploy from GitHub

Run the `deploy` workflow (Actions → deploy → Run workflow) with the environment and a `sha-<7>` tag. It checks the secrets and the tag, copies the `deploy/` files (including `observability/`) to the host over SSH and runs `deploy.sh` there. Inputs reach the scripts only through `env:`.

Each GitHub Environment (`staging`, `production`) holds these secrets:

| Secret | Value |
|---|---|
| `DEPLOY_HOST` | host name or IP |
| `DEPLOY_USER` | the deploy user, a member of the `docker` group |
| `DEPLOY_SSH_KEY` | private key of a key pair authorised for that user |
| `DEPLOY_KNOWN_HOSTS` | output of `ssh-keyscan <host>`, **after** you compare its fingerprint (`ssh-keygen -lf <file>`) with the one the host shows on its own console (`ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub`). A plain keyscan trusts whatever answers on the first try. |
| `DEPLOY_PATH` | `/opt/elmanhg` |

Give `production` a required reviewer, so every production deploy waits for approval.

### Manually on the host

`cd /opt/elmanhg && bash deploy.sh sha-<7>`.

### What `deploy.sh` does

1. Accepts only `sha-<7-40 hex>` or `main`, refuses the `observability` profile without `GRAFANA_ADMIN_PASSWORD` or the Alertmanager config file, and reads the previous tag from `.env`.
2. Writes the new tag into `.env` and pulls the images.
3. Takes a backup when Postgres is running (skipped on the first deploy).
4. `docker compose run --rm migrate`: applies pending migrations while the old `api` and `web` keep serving. A failure stops the script here, before any container is replaced.
5. `docker compose up -d --remove-orphans`: replaces the containers. `migrate` runs again inside `up` (0 pending migrations) and `api` starts only after it succeeds.
6. Waits for `api` (up to 300 s), checks the migrate exit code, then waits for `web`, `ai` and, when they run, `prometheus`, `alertmanager`, `blackbox` and `grafana`.
7. Prunes dangling images and prints `Deployed <tag> (previous: <tag>)`.
8. On any failure it prints the rollback command, `docker compose ps -a` and the last 100 log lines of `migrate` and `api`.

The workflow has not run against a live host yet: there is no VPS, domain or SSH key. Until the secrets exist it stops at "Check deploy secrets" with a clear error.

## 8. Database migrations

- The `migrate` service runs the API image with `--MigrateAndExit=true`: it applies pending EF Core migrations and exits before the admin seed and the HTTP pipeline. It uses the same retry strategy as the API.
- Migrations are forward-only. Re-running `migrate` on an up-to-date database is a no-op (the smoke test does it twice).
- `deploy.sh` runs `migrate` on its own (`docker compose run --rm migrate`) before `up -d`. The `depends_on: migrate: service_completed_successfully` guard in the compose file stays as a second check.
- A failed migration: `deploy.sh` stops before `up -d`, so the old `api` and `web` containers keep serving. `.env` already holds the new tag, so run `bash deploy.sh <previous tag>` to reset `IMAGE_TAG` (the site does not need it to stay up). A migration that failed inside its transaction has changed nothing. Restore the pre-deploy dump only if a migration was partly applied.
- Rollback: `CONFIRM_LIVE_RESTORE=yes bash restore.sh backups/postgres-<pre-deploy stamp>.dump elmanhg`, then `bash deploy.sh <previous tag>`.
- A migration must stay compatible with the previous API image for the whole migration run and the API restart: the old API serves against the new schema until `up -d` replaces it. There is no zero-downtime deploy.

## 9. Backups and restore

- `bash backup.sh` writes `backups/postgres-<UTC stamp>.dump` (custom format, checked with `pg_restore --list`) and, while the API runs, `backups/media-<stamp>.tar.gz`. Files older than `BACKUP_RETENTION_DAYS` (14) are deleted. The directory is created with mode 700 and the files with 600.
- Nightly cron (as the deploy user): `15 2 * * * cd /opt/elmanhg && bash backup.sh >> backups/backup.log 2>&1`.
- Off-site copy is not automated yet: backups stay on the VPS disk. Copy `backups/` off the host with your provider's tool (object storage or another machine) until this is automated.
- Monthly restore drill: `bash restore.sh backups/postgres-<stamp>.dump elmanhg_restore_drill`. It restores into a scratch database, checks that it has as many migrations as the live one, and drops it (`KEEP_RESTORE_DB=1` keeps it). CI runs the same drill on every pull request.
- Live restore: `CONFIRM_LIVE_RESTORE=yes bash restore.sh backups/postgres-<stamp>.dump elmanhg`. It stops `api`, restores in one transaction (`--clean --if-exists`), starts `api` and waits for it to be healthy.
- Media restore: `docker compose -f docker-compose.prod.yml exec -T api tar -xzf - -C /app/App_Data < backups/media-<stamp>.tar.gz`.
- With `FileStorage__Provider=S3`, media lives in the managed provider (R2 or S3), outside these backups: `backup.sh` still archives the local volume, but the bucket's objects are not in it. Rely on the provider's durability and turn on bucket versioning (section 4, Object storage).

## 10. Health checks

| Service | Command | Interval | Healthy means |
|---|---|---|---|
| `postgres` | `pg_isready` | 5 s | accepts connections |
| `api` | `curl -fsS http://127.0.0.1:8080/health` | 10 s (30 s start period) | the API runs and reaches the database |
| `web` | `wget -qO- http://127.0.0.1:2080/healthz` | 10 s | Caddy runs its config |
| `ai` | `/health/ready` through Python `urllib` | 10 s | the AI service is ready |
| `prometheus` / `alertmanager` | `wget -qO- http://127.0.0.1:<port>/-/ready` | 10 s | ready to serve |
| `blackbox` | `wget -qO- http://127.0.0.1:9115/-/healthy` | 10 s | the exporter runs |
| `grafana` | `wget -qO- http://127.0.0.1:3000/api/health` | 10 s | Grafana and its database are up |

`/api/health` is public (rewritten to `/health`) so an external uptime monitor can poll it; the body is only `Healthy` or `Unhealthy`. Useful commands: `docker compose -f docker-compose.prod.yml ps` and `docker compose -f docker-compose.prod.yml logs -f api`. Container logs rotate at 10 MB × 5 files. Monitoring, dashboards and alerting are described in [docs/observability.md](observability.md).

## 11. Reverse proxy and client IPs

Caddy terminates TLS and, with no `trusted_proxies` setting, replaces any client-sent `X-Forwarded-For` with the real peer address. The API trusts forwarded headers (`X-Forwarded-For`, `X-Forwarded-Proto`; never `X-Forwarded-Host`) only from loopback and from `DOCKER_SUBNET`, passed as `ReverseProxy__TrustedNetworks__0`. So the per-IP rate limits (auth, analytics) see the real client (#137, #197), and an internet client cannot spoof its address. Compose passes the same variable to both the network and the API, so changing `DOCKER_SUBNET` moves both together.

`ReverseProxy:TrustedNetworks` is a list of CIDR networks. It is empty by default, and an entry that is not a CIDR network stops the API at startup.

Caddy also strips the client-sent `CF-Connecting-IP`, `CF-IPCountry`, `CF-IPCity`, `CF-Region`, `CF-Region-Code` and `CF-Timezone` headers before proxying to the API, because the API's request log reads them as the client location and no Cloudflare sits in front. Revisit this if a CDN is added ([#220](https://github.com/MohamedEbrahimMohsen/elmanhg/issues/220)).

## 12. Run the production stack locally

`bash deploy/smoke-test.sh` (Docker Desktop and Git Bash on Windows work) builds the three images, validates the Caddyfile, starts the stack with plain HTTP on `http://localhost:8088`, checks the SPA, the `/api` proxy, the cache headers, the security headers (CSP, HSTS, anti-framing, no `Server`), that client-sent `CF-*` headers are stripped, the public `/api/health` and `POST /api/client-errors`, runs `migrate` a second time, takes a backup and runs the restore drill. With the `observability` profile (the default) it also validates every observability config (`promtool check config` and `test rules`, `amtool check-config`, `otelcol validate`) and waits until metrics, traces, logs linked by trace id, log redaction, probes, alert rules and the three dashboards arrive ([docs/observability.md](observability.md), Running it locally). It removes the stack and its volumes afterwards.

| Knob | Default | Effect |
|---|---|---|
| `SMOKE_HTTP_PORT` / `SMOKE_HTTPS_PORT` | `8088` / `8443` | host ports |
| `SMOKE_PROJECT_NAME` | `elmanhg-smoke` | compose project name, when several stacks share one Docker |
| `SMOKE_DOCKER_SUBNET` | `172.30.250.0/24` | compose network |
| `SMOKE_SKIP_BUILD` | unset | `1` reuses the `local/elmanhg-*:<SMOKE_IMAGE_TAG>` images |
| `SMOKE_KEEP` | unset | `1` keeps `deploy/.smoke/` (the generated env files and the backup) |
| `SMOKE_IMAGE_TAG` | `smoke` | tag of the `local/elmanhg-*` images, when several checkouts share one Docker |
| `SMOKE_OBSERVABILITY` | `1` | `0` skips the observability profile and its assertions (Docker Desktop may not expose `/var/lib/docker/containers`; CI on Linux is authoritative) |
| `SMOKE_GRAFANA_PORT` | `3300` | loopback port for Grafana during the smoke test |

`bash deploy/load-test.sh` runs the same stack with seeded load-test data, then the pinned k6 API load and the throttled lesson-page browser run against Caddy, and saves the k6 summaries and the EXPLAIN plans in `deploy/.loadtest-results`. It fails when an API budget or the quiz-transition budget is exceeded; the lesson-page budget is advisory (a `::warning::`) until [#220](https://github.com/MohamedEbrahimMohsen/elmanhg/issues/220) meets it. Its knobs, profiles and CI placement are in [docs/performance.md](performance.md) §5.

## 13. Not done yet

| Item | Status |
|---|---|
| First live deploy | needs a VPS, a domain, DNS and the deploy secrets |
| Off-site backup copy | needs an off-host bucket and credentials; backups stay on the VPS disk |
| Staging OTP | needs real Resend (or WhatsApp) keys, because the fake logs codes in Development only |
| Object storage | built (#96): `FileStorage__Provider=S3` with an R2 or S3 bucket; not yet checked against a live bucket (needs credentials) |
| Voice transcription | built (#96): `ELMANHG_AI_TRANSCRIPTION_PROVIDER=openai`; not yet checked against Whisper (needs a key), and the Egyptian-dialect evaluation waits for recorded clips |
| Observability (#113) | done ([docs/observability.md](observability.md)); an external uptime monitor, a vendor error tracker (Sentry) and live alert receivers are deferred |
| Performance (#114) | done ([docs/performance.md](performance.md)); the lesson p75 budget is missed (2.96 s locally, advisory in CI) and tracked in [#220](https://github.com/MohamedEbrahimMohsen/elmanhg/issues/220); CDN deferred until the live domain |
| Security headers (#115) | done ([docs/security.md](security.md)); HSTS takes effect once the live domain serves HTTPS |
