# Testing convention — kmp

<!-- Framework, where tests live, naming pattern (file + method), what every test must assert, what a vacuous test looks like here. The planner writes the Test plan from this; the reviewer scores tests against it. -->

## Framework

| Level | Tool | Source set |
|---|---|---|
| Unit (domain, mappers, ViewModels, repositories) | `kotlin.test` + `kotlinx-coroutines-test` (`runTest`) | `commonTest` |
| Flows | Turbine | `commonTest` |
| Mocks | Hand-written fakes (default), Mokkery 3.x (compiler plugin, all targets) | `commonTest` |
| Mocks (JVM only) | MockK | `androidHostTest` / `jvmTest` only |
| HTTP | Ktor `MockEngine` + JSON fixtures | `commonTest` |
| DB | SQLDelight in-memory driver via `expect fun testDriver()` | `commonTest` + platform actuals |
| Compose UI | `org.jetbrains.compose.ui:ui-test` `runComposeUiTest` | `commonTest` (runs on jvm, iosSimulatorArm64, wasmJs) |
| Screenshot | Roborazzi (or Paparazzi, Android) + ComposablePreviewScanner | `androidHostTest`/`jvmTest` |
| DI | Koin `verify()` | `jvmTest` |
| Coverage | Kover | — |
## Location and naming

- Mirror the main package: `features/invoices/ui/InvoicesViewModel.kt` → `commonTest/.../features/invoices/ui/InvoicesViewModelTest.kt`.
- Fakes in `commonTest/.../fakes/` (`FakeInvoicesRepository`). Fixtures in `commonTest/resources/`.
- Method names in backticks describing behavior: `` fun `emits error when repository fails`() ``.
## Every test must

- Assert observable output: emitted `UiState`, returned `Result`, rendered node text/semantics — not private calls.
- Use `runTest` with a `TestDispatcher`; `Dispatchers.setMain(StandardTestDispatcher())` in `@BeforeTest`, `Dispatchers.resetMain()` in `@AfterTest`.
- Consume flows with Turbine `test { … }` and end with `cancelAndIgnoreRemainingEvents()` or `awaitComplete()`.
- Find Compose nodes in this priority: `onNodeWithText` / `onNodeWithContentDescription` → semantics matchers (`hasClickAction()`, role) → `onNodeWithTag` (last resort).
- Test stateless `XScreen(state, callbacks)`, not the `Route` with a real ViewModel.
## Never

- MockK or any JVM-only library in `commonTest`.
- `Thread.sleep`, real `delay` waits, `runBlocking` in tests, hardcoded `Dispatchers.IO` in the class under test.
- Real network or a real `HttpClient` engine.
- `@Ignore`, commenting out a test, or deleting one to pass a pipeline.
- Editing or deleting an existing test the plan's `### Tests` table does not list as `modify`/`delete`.
- Recording screenshots outside the pinned CI image; recording in a feature commit.
- Vacuous tests: no assertion, `assertTrue(true)`, asserting a fake returned its canned value.

## What to test at which level

| Code | Level | Must cover |
|---|---|---|
| Use case / validator / mapper | Unit | valid + each failure; unknown enum handling |
| Repository | Unit (`MockEngine`) | success, 401, 422 field errors, 500, timeout → sealed `ApiError`; `CancellationException` rethrown |
| ViewModel | Unit (Turbine) | initial loading → data, error, empty, retry, every `UiState` transition, one-shot effects |
| SQLDelight queries | Unit (in-memory driver) | insert/select/update, flow emits on change |
| Screen | Compose UI (`runComposeUiTest`) | loading, data, empty, error + retry click, RTL (`LocalLayoutDirection provides Rtl`) renders |
| A11y | Compose UI | `assertHasClickAction`, `assertContentDescriptionEquals`, `assertHeightIsAtLeast(48.dp)` on actions |
| Screen visuals | Screenshot | light/dark × en/ar × 390 dp phone |
| Koin modules | `jvmTest` | `verify()` passes |

## Skeletons

```kotlin
class InvoicesViewModelTest {
  private val repo = FakeInvoicesRepository()   // or Mokkery: mock<InvoicesRepository> { everySuspend { list() } returns Result.success(listOf(fake)) }

  @BeforeTest fun setUp() = Dispatchers.setMain(StandardTestDispatcher())
  @AfterTest fun tearDown() = Dispatchers.resetMain()

  @Test fun `emits loading then data`() = runTest {
    repo.result = Result.success(listOf(fakeInvoice))
    val vm = InvoicesViewModel(repo)
    vm.state.test {
      assertTrue(awaitItem().isLoading)
      advanceUntilIdle()
      assertEquals(1, awaitItem().items.size)
      cancelAndIgnoreRemainingEvents()
    }
  }
}
```

```kotlin
@OptIn(ExperimentalTestApi::class)
class InvoicesScreenTest {
  @Test fun `error state shows retry and calls it`() = runComposeUiTest {
    var retried = false
    setContent { AppTheme { InvoicesScreen(InvoicesUiState(isLoading = false, error = UiText.Generic), onRetry = { retried = true }) } }
    onNodeWithText("Retry").assertIsDisplayed().assertHasClickAction().performClick()
    assertTrue(retried)
  }
}
```

```kotlin
class InvoicesApiTest {
  @Test fun `maps 422 to validation error`() = runTest {
    val engine = MockEngine { respond(fixture("invoice_422.json"), HttpStatusCode.UnprocessableEntity, headersOf(HttpHeaders.ContentType, "application/json")) }
    val result = InvoicesRepositoryImpl(InvoicesApi(testClient(engine))).create(draft)
    assertIs<ApiError.Validation>(result.exceptionOrNull())
  }
}
```
Import `runComposeUiTest` from the package the installed CMP version documents (verify against CMP 1.12 docs); `fixture()`/`testClient()` are team helpers in `commonTest`.

## Coverage

- Command: `./gradlew koverXmlReport koverVerify`.
- Gate: 80 % lines on `features/*/domain`, `features/*/data`, and ViewModels. Exclude generated API/models, `Tokens.kt`, `*Preview*`.

## Flakiness

- Inject `CoroutineDispatcher` and `kotlinx.datetime.Clock`; use `advanceTimeBy`/`advanceUntilIdle`, never real time.
- Turbine `awaitItem()` with explicit expectations; no `expectMostRecentItem()` to hide intermediate states.
- A test that flakes twice is quarantined by a human with a linked ticket — never by the implementer.

## Test integrity

- Never edit, skip, or delete a test to make it pass. The orchestrator diffs test files after every implement/rework/fix; unlisted changes are a blocking "test integrity" finding.
- Test looks wrong → stop and write `BLOCKED: <test> — <why>` in the report.
