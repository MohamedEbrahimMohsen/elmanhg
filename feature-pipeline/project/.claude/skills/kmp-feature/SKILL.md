---
name: kmp-feature
description: Style guide for every change in this repo's Kotlin Multiplatform app (shared + Compose Multiplatform UI). The planner, style-checker, reviewer and OpenCode judge kmp/ code against this file and nothing else. Team default; tune the sections, keep the numbering.
---

# KMP feature handbook

Stack: **Kotlin 2.x · Compose Multiplatform · Ktor client · kotlinx.serialization · Koin · Decompose or Voyager for navigation · moko-resources (AR/EN, RTL)**
  → 2026-09 update: Stack is **Kotlin 2.4 · Compose Multiplatform 1.12 · AGP 9 project layout (`shared` KMP library + `androidApp`/`iosApp`/`webApp`/`desktopApp`) · Ktor 3.6 client · kotlinx.serialization · Koin 4.1+ · Navigation 3 (CMP `navigation3-ui` 1.1.x) for new apps · Compose Multiplatform resources `Res.string` (AR/EN, RTL) · SQLDelight 2 for local DB · Coil 3 for images**. Decompose/Voyager/moko-resources stay only where the repo already uses them; no new usage.
Design: every visual value comes from `.claude/design-system.md` through the `AppTheme` tokens in `shared/ui/theme/`. Every screen comes from a Figma frame.

## 1. Layout

- `shared/src/commonMain/kotlin/<pkg>/features/<feature>/` with `data/` (dto, api, repository), `domain/` (model, use case), `ui/` (screen, component, viewmodel/store).
- Platform code only in `androidMain` / `iosMain` behind `expect`/`actual`, and only when common has no way.
- One public class per file. No file over 250 lines. A composable over 80 lines gets split into private composables.
- Strings/fonts/drawables in `shared/src/commonMain/composeResources/` (`values/strings.xml`, `values-ar/strings.xml`, `font/`, `drawable/`).
- SQLDelight schemas in `shared/src/commonMain/sqldelight/<pkg>/db/*.sq`.
- Design tokens: one generated file (`Tokens.kt` in the theme package). Never hand-edit.
- Tests: `commonTest` (shared logic + Compose UI tests), `androidHostTest`/`jvmTest` for JVM-only tools, `iosTest` for platform actuals.
- Versions only in `gradle/libs.versions.toml`. No version literals in `build.gradle.kts`.

## 2. Naming

- Files match the class. Composables `PascalCase` and are nouns (`OrderList`, not `ShowOrderList`). ViewModels/Stores end in `ViewModel`/`Store`. Use cases are verbs.
- DTOs end in `Dto`; domain models have no suffix. Mappers are extension functions `toDomain()` / `toDto()`.
- String resources in `MR.strings`, both `ar` and `en` always.
  → 2026-09 update: new code uses Compose Multiplatform resources: `Res.string.<key>` via `stringResource(Res.string.x)`, files `composeResources/values/strings.xml` + `values-ar/strings.xml`. `MR.strings` only in modules already on moko-resources.

## 3. Forbidden

- `GlobalScope`. Blocking calls on main. `runBlocking` outside tests.
- Raw `Color(0xFF…)`, raw `.dp`/`.sp` numbers, raw `FontWeight` in a screen. Tokens only.
- `println` in committed code. `// TODO`. Commented-out code.
- `!!`. `Any`. Platform types leaking into common.
- Hard-coded user-visible strings. `Modifier.padding(start=…)` with a literal; use token spacing and `PaddingValues` that respect layout direction.
- Business logic in a composable. Network calls outside a repository.
- `java.*` in `commonMain` (`java.util.Date`, `UUID.randomUUID`, `String.format`). Use `kotlinx-datetime`, `kotlin.uuid.Uuid`, platform formatters via `expect`.
- Gson/Moshi/Jackson/Retrofit/OkHttp APIs in `commonMain`. `kapt` (use KSP). `LiveData` in shared code.
- Swallowing `CancellationException` in `catch (e: Exception)` (rethrow it).
- `collectAsState()` for ViewModel flows (use `collectAsStateWithLifecycle()`).
- `LaunchedEffect(Unit)` to load ViewModel-owned data (load in the ViewModel).
- `absolutePadding`, `Arrangement.Absolute*`, `Alignment.Absolute*` unless mirroring must be disabled (comment why).
- Tokens in `multiplatform-settings`, `NSUserDefaults`, `SharedPreferences`.
- A new `HttpClient` per request. Ktor `Logging` without `sanitizeHeader { it == HttpHeaders.Authorization }` or enabled in release.
- String-based navigation routes (`"invoice/{id}"`).
- Old context receivers syntax `context(Foo)`; Kotlin 2.4 context parameters `context(foo: Foo)` only where the plan says.

## 4. Required

- Every screen renders loading, empty, error, and data from a single immutable `UiState` (`sealed interface` or data class with `Result`), matching the Figma frame for each.
- Repositories return `Result<T>` / a sealed error type, never throw into UI.
- `@Serializable` on every DTO; no reflection-based JSON.
- Koin modules per feature, registered in one place.
- `remember`/`derivedStateOf` for computed UI values; `LaunchedEffect` keyed correctly. No side effects in composition.
- Accessible: `contentDescription` on icon-only actions, minimum 48dp touch targets.
- ViewModel exposes `StateFlow<UiState>`; one-shot effects through `Channel<Effect>(Channel.BUFFERED).receiveAsFlow()`. Screens are stateless: `XScreen(state, callbacks)`; `XRoute` wires the ViewModel.
- UI models `@Immutable`; collections `ImmutableList` (kotlinx.collections.immutable).
- `LazyColumn(items, key = { it.id })` for lists. Never `Column(Modifier.verticalScroll)` for data lists.
- Dispatchers injected (Koin), never `Dispatchers.IO` hardcoded in a class under test.
- `@Preview` for every screen state (loading, empty, error, data) in LTR and RTL.

## 5. Tests

- `commonTest` for use cases, repositories (fake Ktor engine), and state reducers. Compose UI tests for every screen state on Android; iOS covered by shared state tests.
  → 2026-09 update: Compose UI tests run from `commonTest` with `runComposeUiTest` (`org.jetbrains.compose.ui:ui-test`) on jvm, iosSimulatorArm64 and wasmJs; Android device tests via `connectedAndroidTest` only when the plan asks.
- Every use case error path has a test. Every `UiState` transition has a test.
- Turbine for flows; no `Thread.sleep`.
- Full rules: `.claude/conventions/kmp-testing.md`. It wins on test detail.
- Never edit, skip (`@Ignore`), or delete an existing test to make it pass. A wrong test → `BLOCKED: <test> — <why>` in the report.

## 6. DO / DON'T catalog (every DON'T is a blocking review finding)

| # | DON'T | DO |
|---|-------|----|
| 6.1 | `Color(0xFF4B2A8A)` in a composable | `AppTheme.colors.primary` |
| 6.2 | `padding(start = 16.dp)` | `padding(start = AppTheme.spacing.md)` |
| 6.3 | `var loading by mutableStateOf` in a screen | Single `UiState` from the ViewModel/Store |
| 6.4 | Ktor call inside a ViewModel | Repository + use case |
| 6.5 | Skip the empty/error state | Implement every state in the Figma frame |
| 6.6 | `Text("Submit")` | `Text(stringResource(MR.strings.submit))` with both locales |
| 6.6 → 2026-09 update | `MR.strings` in new code | `Text(stringResource(Res.string.submit))` with `values/` and `values-ar/` updated |
| 6.7 | `expect`/`actual` for something common can do | Common implementation |
| 6.8 | `!!` | Explicit null handling or a sealed result |
| 6.9 | `Icons.Filled.ArrowBack` | `Icons.AutoMirrored.Filled.ArrowBack` |
| 6.10 | `vm.state.collectAsState()` | `vm.state.collectAsStateWithLifecycle()` |
| 6.11 | `catch (e: Exception) { … }` around suspend calls | Rethrow `CancellationException`, map the rest to a sealed error |
| 6.12 | `java.util.UUID.randomUUID()` in common | `kotlin.uuid.Uuid.random()` |
| 6.13 | `navController.navigate("invoice/$id")` | `backStack.add(InvoiceDetail(id))` with a `@Serializable` `NavKey` |
| 6.14 | `mockk<Repo>()` in `commonTest` | Hand-written fake or Mokkery `mock<Repo>()` |
| 6.15 | Passing the ViewModel down the tree | Pass state + lambdas |
| 6.16 | `koinInject()` inside a leaf composable | Inject at the `Route` level |
| 6.17 | `MaterialTheme.colorScheme.primary` inline where a semantic token exists | `AppTheme.tokens.color.<role>` |

## 7. New project from scratch

Use only when the plan says "new app" (see `momenta-greenfield-bootstrap`).

- Generate with the KMP wizard at `kmp.jetbrains.com` (or the IDE KMP wizard): targets Android + iOS + Web (wasmJs, required for the Agy E2E step), Desktop optional, "Share UI" on.
- New default structure (AGP 9): `shared` is a KMP library using `com.android.kotlin.multiplatform.library`; apps in `androidApp/`, `iosApp/` (Xcode), `webApp/`, `desktopApp/`. Do not use the old single `composeApp` module for new projects. Mixed native/shared UI → `sharedLogic` + `sharedUI`.
- JDK 21 toolchain; Gradle configuration cache and build cache on.

```
shared/src/commonMain/kotlin/<pkg>/
  app/        App.kt, navigation/ (Nav3), di/AppModule.kt
  core/       designsystem/ (Tokens.kt generated, AppTheme.kt, components/), network/, storage/, result/
  features/<feature>/ data/ (api, dto, repository impl) domain/ (model, repository interface, use case) ui/ (Screen, Route, ViewModel, UiState)
shared/src/commonMain/composeResources/  values/ values-ar/ font/ drawable/
shared/src/commonMain/sqldelight/<pkg>/db/
shared/src/{androidMain,iosMain,wasmJsMain}/   shared/src/commonTest/
gradle/libs.versions.toml
```

Version catalog entries (exact patch from `libs.versions.toml`; verify per §21): Kotlin 2.4.x, Compose Multiplatform 1.12.x, kotlinx-coroutines, kotlinx-serialization-json, kotlinx-datetime, kotlinx-collections-immutable, Ktor 3.6.x (`ktor-client-core`, `-content-negotiation`, `-serialization-kotlinx-json`, `-auth`, `-logging`; engines `-okhttp` Android, `-darwin` iOS, `-js` wasm), Koin 4.1+ (`koin-compose-viewmodel`), `org.jetbrains.androidx.lifecycle:lifecycle-viewmodel-compose`, `org.jetbrains.androidx.navigation3:navigation3-ui` 1.1.x, SQLDelight 2.x, Coil 3, Mokkery, Turbine, kotlin-test.

## 8. State & data fetching (MVVM + UDF)

```kotlin
@Immutable
data class InvoicesUiState(
  val items: ImmutableList<InvoiceUi> = persistentListOf(),
  val isLoading: Boolean = true,
  val error: UiText? = null,
) { val isEmpty get() = !isLoading && error == null && items.isEmpty() }

class InvoicesViewModel(private val repo: InvoicesRepository) : ViewModel() {
  private val _state = MutableStateFlow(InvoicesUiState())
  val state = _state.asStateFlow()
  init { refresh() }
  fun refresh() {
    viewModelScope.launch {
      _state.update { it.copy(isLoading = true, error = null) }
      repo.list().fold(
        onSuccess = { list -> _state.update { it.copy(isLoading = false, items = list.map(::toUi).toImmutableList()) } },
        onFailure = { e -> _state.update { it.copy(isLoading = false, error = e.toUiText()) } },
      )
    }
  }
}

@Composable
fun InvoicesRoute(vm: InvoicesViewModel = koinViewModel()) {
  val state by vm.state.collectAsStateWithLifecycle()
  InvoicesScreen(state, onRetry = vm::refresh)
}
```
- Repositories return `Result<T>`/sealed `AppResult`; never throw into UI; rethrow `CancellationException`.
- Offline cache: SQLDelight query `asFlow().mapToList(dispatcher)`; repository is the single source of truth.
- State updates only via `_state.update { it.copy(...) }`.

## 9. DI (Koin 4.1+)

```kotlin
val invoicesModule = module {
  singleOf(::InvoicesRepositoryImpl) bind InvoicesRepository::class
  viewModelOf(::InvoicesViewModel)
}
```
- Platform bindings via `expect fun platformModule(): Module`.
- A JVM test runs Koin `verify()` on every module.
- One `HttpClient` singleton, closed on shutdown.

## 10. Forms & validation

- Form state lives in the ViewModel (`data class XFormState(val email: String = "", val emailError: UiText? = null, …)`); validation functions are pure in `domain/` and unit-tested.
- `OutlinedTextField(label = { Text(stringResource(Res.string.email)) }, isError = …, supportingText = { error?.let { Text(it.asString()) } })`. Label always visible.
- Server field errors mapped to per-field `UiText`. Focus the first invalid field via `FocusRequester`.
- `KeyboardOptions(keyboardType, imeAction)` set on every field; submit disabled only while submitting.

## 11. Routing (Navigation 3)

```kotlin
@Serializable sealed interface Route : NavKey
@Serializable data object InvoicesList : Route
@Serializable data class InvoiceDetail(val id: String) : Route

private val navConfig = SavedStateConfiguration {
  serializersModule = SerializersModule { polymorphic(NavKey::class) { subclassesOfSealed<Route>() } }
}  // required on iOS and web (no reflection)

@Composable
fun AppNav() {
  val backStack = rememberNavBackStack(navConfig, InvoicesList)
  NavDisplay(backStack = backStack, onBack = { backStack.removeLastOrNull() },
    entryProvider = entryProvider {
      entry<InvoicesList> { InvoicesRoute(onOpen = { backStack.add(InvoiceDetail(it)) }) }
      entry<InvoiceDetail> { key -> InvoiceDetailRoute(key.id) }
    })
}
```
- Auth gating at the root: `authState` flow selects the auth back stack or the main back stack.
- Existing apps on `navigation-compose` typed routes, Decompose or Voyager: stay; migration is its own planned task.
- Nav3 API names move between releases — verify against the installed version's docs before copying.

## 12. API client generation & networking

```kotlin
fun httpClient(engine: HttpClientEngine, tokens: TokenStore, env: Env) = HttpClient(engine) {
  expectSuccess = true
  install(ContentNegotiation) { json(Json { ignoreUnknownKeys = true; explicitNulls = false }) }
  install(HttpTimeout) { requestTimeoutMillis = 15_000 }
  install(Logging) {
    level = if (env.debug) LogLevel.HEADERS else LogLevel.NONE
    sanitizeHeader { it == HttpHeaders.Authorization }
  }
  install(Auth) { bearer {
    loadTokens { tokens.load() }
    refreshTokens { tokens.refresh(client, oldTokens) }
    sendWithoutRequest { it.url.host == env.apiHost }
  } }
  defaultRequest { url(env.baseUrl) }
}
```
- Map `ClientRequestException`/`ServerResponseException`/`IOException` → sealed `ApiError`.
- OpenAPI: generate models (+ Ktor client) into `shared/build/generated` via a Gradle task wired before compilation; CI regenerates and fails on drift. Do not use openapi-generator `kotlin --library multiplatform` without checking its Ktor/serialization versions. Fallback: generated models + thin hand-written `XApi` on Ktor.
- Polymorphic deserialization only over sealed hierarchies.

## 13. Loading / error / empty states

```kotlin
when {
  state.isLoading -> InvoicesSkeleton()
  state.error != null -> ErrorView(state.error, onRetry)
  state.isEmpty -> InvoicesEmpty(onCreate)
  else -> InvoicesList(state.items)
}
```
- Skeleton matches final layout. Error text from `UiText` resources + Retry. Empty = Figma empty state with CTA; "no results for filter" offers "Clear filters".
- One-shot outcomes (snackbar, navigation) via the effect channel.

## 14. Auth token handling

- `expect interface SecureStore`: Android Keystore-backed (DataStore/EncryptedFile + Tink; `androidx.security:security-crypto` is deprecated), iOS Keychain `kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly`.
- Logout: clear `SecureStore` and `client.authProvider<BearerAuthProvider>()?.clearToken()`.
- Certificate pinning when the plan requires it: OkHttp `CertificatePinner` (Android), Darwin `handleChallenge(CertificatePinner…)` (iOS); backup pin mandatory.
- Android manifest: no `android:exported="true"` without intent filter need; `PendingIntent.FLAG_IMMUTABLE`; `cleartextTrafficPermitted="false"`; no `addJavascriptInterface` on untrusted WebViews.

## 15. i18n + RTL (Arabic)

- `stringResource(Res.string.x)`; plurals via `<plurals>` + `pluralStringResource` with Arabic `zero/one/two/few/many/other`.
- ViewModels emit `UiText` (resource + args), resolved in UI; `getString()` (suspend) outside composition.
- RTL from `LocalLayoutDirection`. Compose `start/end` padding is directional; directional icons `Icons.AutoMirrored.*`.
- Previews/tests force RTL: `CompositionLocalProvider(LocalLayoutDirection provides LayoutDirection.Rtl)`.
- Dates/numbers: `kotlinx-datetime` + `expect fun formatMoney(...)`/`formatDate(...)` per platform.

## 16. Accessibility (WCAG 2.2 AA)

- Touch targets ≥ 48 dp: Material 3 components enforce it; custom clickables add `Modifier.minimumInteractiveComponentSize()`.
- `contentDescription` from resources for meaningful icons, `null` for decorative.
- List rows `Modifier.semantics(mergeDescendants = true)`; titles `semantics { heading() }`; custom clickables `Modifier.clickable(role = Role.Button, onClickLabel = …)`; toggles `stateDescription`; async status `liveRegion = LiveRegionMode.Polite`.
- Font sizes in `sp` tokens; no fixed-height text containers.
- Web/desktop: keyboard reachable (`focusable()`, `onKeyEvent`), visible focus indication from tokens.

## 17. Theming from design tokens

```kotlin
object AppTheme { val tokens: AppTokens @Composable get() = LocalAppTokens.current }

@Composable
fun AppTheme(dark: Boolean = isSystemInDarkTheme(), content: @Composable () -> Unit) {
  val tokens = if (dark) Tokens.dark else Tokens.light
  CompositionLocalProvider(LocalAppTokens provides tokens) {
    MaterialTheme(colorScheme = tokens.colorScheme(), typography = tokens.typography(), shapes = tokens.shapes(), content = content)
  }
}
```
- `LocalAppTokens = staticCompositionLocalOf<AppTokens> { error("AppTheme missing") }` carries non-M3 tokens (spacing, elevation, motion).
- Fonts via `composeResources/font` (`Font(Res.font.<arabic_family>)`).
- CI grep fails on `Color\(0x`, `[0-9]+\.dp`, `[0-9]+\.sp` under `features/**` (detekt + `io.nlopez.compose.rules` also run).

## 18. Responsive

- Phone first at 390 dp wide; wider layouts via `BoxWithConstraints`/window size classes with breakpoints from tokens.
- Web (wasmJs) verified at 390 and 1280 in the E2E step.
- No fixed widths on text; `weight()` over hard sizes.

## 19. Performance

- `@Immutable`/`@Stable` UI models; `ImmutableList`. `LazyColumn` with `key` and `contentType`.
- `derivedStateOf` for scroll-derived state; lambda modifiers (`Modifier.offset { }`) for animated values.
- Coil 3 `AsyncImage` with size constraints and placeholder.
- Compose compiler reports enabled in CI; Baseline Profiles for Android release.

## 20. Tooling & CI commands

```bash
./gradlew ktlintCheck detekt
./gradlew :shared:allTests
./gradlew :shared:jvmTest :shared:iosSimulatorArm64Test :shared:wasmJsTest
./gradlew koverXmlReport koverVerify
./gradlew :androidApp:assembleRelease
./gradlew :webApp:wasmJsBrowserDistribution          # Agy E2E target
xcodebuild -project iosApp/iosApp.xcodeproj -scheme iosApp -sdk iphonesimulator build   # macOS runner only
```
- `allWarningsAsErrors = true` in CI; `explicitApi()` for library modules.
- `.editorconfig`: `ktlint_code_style = ktlint_official`, `ktlint_function_naming_ignore_when_annotated_with = Composable`.
- Gradle task names depend on the plugin setup — run `./gradlew tasks --all | grep -i <name>` if a task is missing; never invent one.

## 21. Dependencies (verify before adding)

- Add a library only if the plan names it with group:artifact:version.
- Before adding: `curl -s "https://search.maven.org/solrsearch/select?q=g:<group>+AND+a:<artifact>&rows=1&wt=json" | jq '.response.docs[0].latestVersion'` returns a version (or Google Maven for `androidx.*`); the artifact publishes the KMP targets we use.
- Versions only in `libs.versions.toml`; no dynamic versions (`1.+`); dependency locking/verification metadata updated in the same commit.
- Never guess an artifact. Not found → `BLOCKED: dependency <group:artifact> not found`.

## 22. Web build for E2E (Agy)

- The Agy browser runs against `:webApp:wasmJsBrowserDistribution` output at 390 and 1280.
- Before handing off, open the build and confirm the accessibility tree exposes labels/roles (Playwright snapshot shows button names). If it does not, scenarios are BLOCKED, not FAIL.
- Key nodes carry `Modifier.testTag("invoice-save")` as a secondary hook; text/role remain primary locators.
- Native-only behavior (permissions, biometrics, deep links) is marked `web-verifiable: no` in the plan.

## 23. LLM mistakes to avoid

| Mistake | Correct |
|---|---|
| Single `composeApp` module on AGP 9 | `shared` + app modules, `com.android.kotlin.multiplatform.library` |
| `kapt`, `kotlin-android-extensions`, `LiveData` | KSP, `StateFlow` |
| `GlobalScope`, `runBlocking`, swallowed `CancellationException` | `viewModelScope`, rethrow |
| Gson/Moshi/Retrofit in `commonMain` | kotlinx.serialization + Ktor |
| `java.util.*` in `commonMain` | `kotlinx-datetime`, `kotlin.uuid.Uuid` |
| `R.string`/`MR.strings` in new shared UI | `Res.string` |
| String nav routes; missing polymorphic serializer on iOS/web | `@Serializable` `NavKey` + `SavedStateConfiguration` |
| `Icons.Filled.ArrowBack` | `Icons.AutoMirrored.Filled.ArrowBack` |
| MockK in `commonTest` | Fakes or Mokkery |
| `Thread.sleep`/`delay` waits in tests; missing `Dispatchers.setMain` | `runTest` + `advanceUntilIdle`/`advanceTimeBy` |
| Tokens in `multiplatform-settings` | `SecureStore` (Keystore/Keychain) |
| `LaunchedEffect(Unit) { vm.load() }` | Load in ViewModel `init`/intent |
| Only the data state rendered | Loading/empty/error per §13 |

## 24. Definition of done

- [ ] `./gradlew ktlintCheck detekt` and `./gradlew :shared:allTests` exit 0.
- [ ] `./gradlew koverVerify` passes thresholds in `.claude/conventions/kmp-testing.md`.
- [ ] Generated API/tokens produce no diff after regeneration.
- [ ] Every screen renders loading, empty, error, data matching Figma frames; previews exist for each in LTR and RTL.
- [ ] No `Color(0x…)`, literal `dp`/`sp` in `features/**` (grep from §17 clean).
- [ ] Every new string in `values/` and `values-ar/`.
- [ ] Touch targets ≥ 48 dp; icon-only actions have `contentDescription`.
- [ ] No new dependency outside the plan; catalog + lock/verification updated if one was added.
- [ ] No existing test edited/`@Ignore`d/deleted unless the plan's `### Tests` table lists it.
- [ ] Report contains commands, output tails, exit codes; stuck → `BLOCKED: <reason>`.
